using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Host.Bootstrap;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.ServiceProcess;

namespace Servy.Host
{
    /// <summary>
    /// Implements the primary Servy Host background Windows service: the only Servy process that runs as Local System
    /// and opens <c>Servy.db</c> on behalf of the service wrappers, which talk to it over a local named pipe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pipe is created with a protected DACL (<see cref="ServyHostPipeSecurity"/>): Local System and Administrators,
    /// plus Read and Write for the accounts the installed services run under, and for the account a running service process
    /// still uses after its service was reinstalled under another one, until that process exits. Network logons are
    /// allowed. The pipe is local only: a client on another computer cannot open it (<c>PIPE_REJECT_REMOTE_CLIENTS</c>),
    /// and the host also refuses any request from one.
    /// </para>
    /// <para>
    /// Being able to connect is not being allowed to read anything. Every request about a service is answered only when
    /// the caller is the process the Service Control Manager reports for that service, or Local System or an elevated
    /// administrator. So a service account can read only its own service's configuration, can never read another
    /// service's configuration or secrets, and can write nothing but its own service's runtime state: the PID, the
    /// active log paths, the previous stop timeout and the restart attempts counter. The configuration itself is never
    /// written through the pipe.
    /// </para>
    /// </remarks>
    public partial class Service : ServiceBase
    {
        #region Private Fields

        private readonly INamedPipesService _namedPipesService;
        private readonly SecureData? _secureData;
        private IServyLogger? _logger;
        private readonly IServiceBootstrapEnvironment? _bootstrapEnvironment;
        private CancellationTokenSource? _cancellationSource;
        private readonly IServiceRepository? _serviceRepository;
        private readonly IWindowsServiceApi _windowsServiceApi;
        private readonly IPipeCallerIdentifier _callerIdentifier;
        private volatile bool _disposed = false;
        private readonly IAppDbContext? _dbContext;
        private readonly ProtectedKeyProvider? _protectedKeyProvider;
        private Task[]? _listenerTasks;

        /// <summary>The DACL every new server instance is created with; rebuilt by <see cref="RefreshPipeSecurityAsync"/>.</summary>
        private PipeSecurity? _pipeSecurity;

        /// <summary>Cancel the server instances that are waiting for a client, so the next ones get the current DACL.</summary>
        private readonly HashSet<CancellationTokenSource> _waitingInstanceCts = new HashSet<CancellationTokenSource>();

        /// <summary>The requests being handled, so <see cref="OnStop"/> can wait for them.</summary>
        private readonly ConcurrentDictionary<Task, bool> _activeHandlers = new ConcurrentDictionary<Task, bool>();

        private readonly object _securityLock = new object();

        /// <summary>Whether a retry for accounts that could not be resolved, or a recheck of an account a running service process still uses, is waiting; guarded by <see cref="_securityLock"/>.</summary>
        private bool _unresolvedRetryPending;

        /// <summary>The retries spent since the last refresh that was not itself a retry; guarded by <see cref="_securityLock"/>.</summary>
        private int _unresolvedRetryAttempts;

        /// <summary>The number of the last DACL rebuild that started reading the services; guarded by <see cref="_securityLock"/>.</summary>
        private long _refreshStarted;

        /// <summary>The number of the DACL rebuild whose result is published in <see cref="_pipeSecurity"/>; guarded by <see cref="_securityLock"/>.</summary>
        private long _refreshPublished;

        #endregion

        #region Test Seams

        /// <summary>
        /// Gets or sets the check that admits a service action into <see cref="ProcessRequestAsync"/>'s switch. Tests replace
        /// it to admit an action that has no case, which is the only way to reach the switch's default.
        /// </summary>
        internal Func<string?, bool> IsKnownServiceActionCheck { get; set; } = IsKnownServiceAction;

        /// <summary>
        /// Gets or sets the name of the pipe the service listens on; tests use a unique name.
        /// </summary>
        internal string PipeName { get; set; } = AppConfig.ServyHostNamedPipeName;

        /// <summary>
        /// Gets or sets the clock that stamps the restart attempts counter.
        /// </summary>
        internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the time a connected client has to send its request and read the answer.
        /// </summary>
        internal int RequestTimeoutMs { get; set; } = AppConfig.ServyHostDefaultRequestTimeoutMs;

        /// <summary>
        /// Gets or sets the factory that creates a server instance of the pipe with the given security.
        /// </summary>
        internal Func<string, PipeSecurity, NamedPipeServerStream> ServerStreamFactory { get; set; } = CreateServerStream;

        /// <summary>
        /// Gets or sets the delay before the DACL is rebuilt again when an account could not be resolved, or when a running
        /// service process still uses an account its service no longer names.
        /// </summary>
        internal int UnresolvedAccountRetryDelayMs { get; set; } = AppConfig.ServyHostUnresolvedAccountRetryDelayMs;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class using default production dependencies.
        /// </summary>
        [ExcludeFromCodeCoverage]
        public Service() : this(
            new EventLogLogger(AppConfig.EventSource),
            new NamedPipesService()
          )
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class with core dependencies and default bootstrap environment.
        /// </summary>
        /// <param name="logger">The logger instance to use for service logging.</param>
        /// <param name="namedPipesService">The named pipe service instance to use for asynchronous IPC communications.</param>
        [ExcludeFromCodeCoverage]
        public Service(
            IServyLogger logger,
            INamedPipesService namedPipesService
            )
            : this(logger, namedPipesService, new ServiceBootstrapEnvironment())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class with explicitly injected dependencies for unit
        /// and integration testing.
        /// </summary>
        /// <param name="logger">The logger instance to use for service logging.</param>
        /// <param name="namedPipesService">The named pipe service instance to use for asynchronous IPC communications.</param>
        /// <param name="serviceRepository">The service repository instance to use for database operations.</param>
        /// <param name="windowsServiceApi">The Service Control Manager API used to authorize callers.</param>
        /// <param name="callerIdentifier">Identifies the client of a connection.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
        internal Service(
            IServyLogger logger,
            INamedPipesService namedPipesService,
            IServiceRepository serviceRepository,
            IWindowsServiceApi windowsServiceApi,
            IPipeCallerIdentifier callerIdentifier
            )
        {
            ServiceName = AppConfig.ServyHostServiceName;
            AutoLog = false;

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _namedPipesService = namedPipesService ?? throw new ArgumentNullException(nameof(namedPipesService));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _windowsServiceApi = windowsServiceApi ?? throw new ArgumentNullException(nameof(windowsServiceApi));
            _callerIdentifier = callerIdentifier ?? throw new ArgumentNullException(nameof(callerIdentifier));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class using the supplied bootstrap environment seam for initialization.
        /// </summary>
        /// <param name="logger">The logger instance to use for service logging.</param>
        /// <param name="namedPipesService">The named pipe service instance to use for asynchronous IPC communications.</param>
        /// <param name="bootstrapEnvironment">The seam over the process-global and startup calls.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/>, <paramref name="namedPipesService"/>, or <paramref name="bootstrapEnvironment"/> is <see langword="null"/>.</exception>
        internal Service(
            IServyLogger logger,
            INamedPipesService namedPipesService,
            IServiceBootstrapEnvironment bootstrapEnvironment
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _namedPipesService = namedPipesService ?? throw new ArgumentNullException(nameof(namedPipesService));
            _bootstrapEnvironment = bootstrapEnvironment ?? throw new ArgumentNullException(nameof(bootstrapEnvironment));

            _bootstrapEnvironment.InitializeLogger(AppConfig.ServyHostLogFileName);

            _windowsServiceApi = bootstrapEnvironment.CreateWindowsServiceApi();
            _callerIdentifier = bootstrapEnvironment.CreateCallerIdentifier();

            try
            {
                ServiceName = AppConfig.ServyHostServiceName;
                _bootstrapEnvironment.EnsureEventSourceExists();

                var config = _bootstrapEnvironment.BuildConfiguration();
                var coreSettings = _bootstrapEnvironment.LoadCoreSettings();
                var connectionString = coreSettings.ConnectionString;
                var aesKeyFilePath = coreSettings.AESKeyFilePath;
                var aesIVFilePath = coreSettings.AESIVFilePath;

                _bootstrapEnvironment.ConfigureLogging(config, instanceLogger: _logger);
                CoreSettingsLoader.WarnAboutIgnoredSettings(config, AppConfig.ServyHostSettingsFileName, _logger);

                var isEventLogEnabled = ConfigParser.ParseBool(config["EnableEventLog"], AppConfig.DefaultEnableEventLog, "EnableEventLog");
                AutoLog = isEventLogEnabled;

                if (!_bootstrapEnvironment.IsSqliteVersionSafe(out var detectedVersion))
                {
                    _bootstrapEnvironment.LogError($"[FATAL] Vulnerable SQLite version detected: {detectedVersion}. " +
                                     $"Minimum required: {AppConfig.MinRequiredSqliteVersion} (CVE-2025-6965 mitigation).");

                    Environment.ExitCode = AppConfig.ServiceSpecificErrorCode;
                    TerminateProcess(Environment.ExitCode);
                    return;
                }

                // Opening the data stack also runs the database migrations, including the import of the legacy
                // recovery\ counters, before any wrapper can ask for them.
                var dataStack = _bootstrapEnvironment.CreateDataStack(connectionString, aesKeyFilePath, aesIVFilePath);

                _dbContext = dataStack.DbContext;
                _protectedKeyProvider = dataStack.ProtectedKeyProvider;
                _secureData = dataStack.SecureData;
                _serviceRepository = dataStack.ServiceRepository;

                // The wrappers now log under logs\service\<ServiceName>\; move what they wrote to logs\ before into
                // logs\service\, where it stays readable by the administrators only.
                _bootstrapEnvironment.MigrateLegacyServiceLog();

                CanShutdown = true;
            }
            catch (Exception ex)
            {
                _bootstrapEnvironment.LogError("Fatal error during service construction.", ex);

                if (Environment.ExitCode == 0)
                {
                    Environment.ExitCode = AppConfig.ServiceSpecificErrorCode;
                }

                TerminateProcess(Environment.ExitCode);
            }
        }

        #endregion

        #region Lifecycle (OnStart / OnStop / Dispose)

        /// <summary>
        /// Executes when a Start command is sent to the service by the Service Control Manager (SCM). Builds the pipe's
        /// DACL and spawns the background Named Pipe listener loop.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                // PROMOTE LOGGER IMMEDIATELY
                // Now every log from this point forward (including validation errors) is prefixed.
                _logger = _logger?.CreateScoped(AppConfig.ServyHostServiceName);

                StartListening();
            }
            catch (Exception ex)
            {
                _logger?.Error("Exception in OnStart.", ex);
                if (ExitCode == 0) ExitCode = AppConfig.ServiceSpecificErrorCode;
                Stop();
            }
        }

        /// <summary>
        /// Builds the pipe's DACL and starts the listener loop in the background.
        /// </summary>
        internal void StartListening()
        {
            _cancellationSource = new CancellationTokenSource();
            var token = _cancellationSource.Token;

            // Build the DACL before the first instance exists, so no instance is ever created without one
            RefreshPipeSecurityAsync(token).GetAwaiter().GetResult();

            // Log ONCE here at the orchestration level
            Logger.Info($"[{AppConfig.ServyHostServiceName}] Servy host named pipe listener started ({AppConfig.ServyHostListenerCount} worker instances)...");

            // Run the IPC listeners asynchronously in background tasks so OnStart returns promptly to SCM. Several
            // instances wait at once, so a burst of services starting together (at boot) never queues behind one.
            // The token is deliberately NOT passed to Task.Run: that would only cancel the SCHEDULING, so a stop
            // arriving while the thread pool is still starting the instances would end them Canceled instead of
            // completed, for no gain - the loop already returns at once on the same token (#7353)
            _listenerTasks = Enumerable.Range(0, AppConfig.ServyHostListenerCount)
                .Select(_ => Task.Run(() => ListenForPipeConnectionsAsync(token)))
                .ToArray();
        }

        /// <summary>
        /// Continuously accepts incoming Named Pipe client connections until cancellation is requested. Each connection
        /// is handled in the background, so a slow client never blocks the next one.
        /// </summary>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous listener loop execution.</returns>
        private async Task ListenForPipeConnectionsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? serverStream = null;
                CancellationTokenSource? waitCts = null;
                try
                {
                    PipeSecurity security;
                    lock (_securityLock)
                    {
                        // A private copy per instance: creating the instance writes the DACL to the pipe through this
                        // object, from several listener threads at once, and the published _pipeSecurity is never handed
                        // to that write, so nothing reading it (CurrentPipeSecurity) can see it mid-write (#7355)
                        security = CopyAccessRules(_pipeSecurity ?? ServyHostPipeSecurity.Create((IEnumerable<System.Security.Principal.SecurityIdentifier>?)null));
                        waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        _waitingInstanceCts.Add(waitCts);
                    }

                    serverStream = ServerStreamFactory(PipeName, security);

                    // Wait asynchronously for a client to connect
                    await serverStream.WaitForConnectionAsync(waitCts.Token);

                    var connected = serverStream;
                    serverStream = null; // ownership moves to the handler
                    TrackHandler(HandleConnectionAsync(connected, ct));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    // The DACL was rebuilt: drop the waiting instance and create the next one with the new DACL
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error accepting Named Pipe connection.", ex);

                    // Do not spin when the pipe cannot be created (for example while another process holds the name)
                    try { await Task.Delay(AppConfig.ScmPollIntervalMs, ct); } catch (OperationCanceledException) { break; }
                }
                finally
                {
                    lock (_securityLock)
                    {
                        if (waitCts != null)
                            _waitingInstanceCts.Remove(waitCts);
                    }

                    waitCts?.Dispose();
                    serverStream?.Dispose();
                }
            }
        }

        /// <summary>
        /// Copies the access rules (the DACL and its protection) of a pipe security into a new, independent object.
        /// </summary>
        /// <param name="source">The security to copy.</param>
        /// <returns>The copy, with its access rules marked as changed so that applying it to a pipe writes them.</returns>
        private static PipeSecurity CopyAccessRules(PipeSecurity source)
        {
            var copy = new PipeSecurity();
            copy.SetSecurityDescriptorBinaryForm(source.GetSecurityDescriptorBinaryForm(), System.Security.AccessControl.AccessControlSections.Access);
            return copy;
        }

        /// <summary>
        /// Remembers a running handler until it completes.
        /// </summary>
        /// <param name="handler">The handler task.</param>
        private void TrackHandler(Task handler)
        {
            _activeHandlers.TryAdd(handler, true);
            _ = handler.ContinueWith(t => _activeHandlers.TryRemove(t, out _), TaskScheduler.Default);
        }

        /// <summary>
        /// Executes when a Stop command is sent to the service by the Service Control Manager (SCM). Cancels active background tasks and flushes loggers.
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                StopListening();
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Exception stopping pipe listener: {ex.Message}");
            }
            finally
            {
                FlushAndShutdownLogger();
                base.OnStop();
            }
        }

        /// <summary>
        /// Stops the listener loop and waits briefly for the requests being handled.
        /// </summary>
        internal void StopListening()
        {
            _cancellationSource?.Cancel();
            if (_listenerTasks != null)
                WaitIgnoringCancellation(_listenerTasks);
            WaitIgnoringCancellation(_activeHandlers.Keys.ToArray());
        }

        /// <summary>
        /// Waits briefly for tasks to finish. A task cancelled by the stop itself ends Canceled, which is what stopping
        /// is for, so cancellations are not errors here; any other failure still propagates.
        /// </summary>
        /// <param name="tasks">The tasks to wait for.</param>
        private static void WaitIgnoringCancellation(Task[] tasks)
        {
            try
            {
                Task.WaitAll(tasks, AppConfig.ServyHostStopWaitMs);
            }
            catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e => e is OperationCanceledException))
            {
                // Stopped before they ran
            }
        }

        /// <summary>
        /// Disposes managed resources used by the service host instance.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _cancellationSource?.Cancel();
                _cancellationSource?.Dispose();

                try { _secureData?.Dispose(); } catch (Exception ex) { _logger?.Warn($"Disposing _secureData failed: {ex.Message}"); }
                try { _protectedKeyProvider?.Dispose(); } catch (Exception ex) { _logger?.Warn($"Disposing _protectedKeyProvider failed: {ex.Message}"); }
                try { _dbContext?.Dispose(); } catch (Exception ex) { _logger?.Warn($"Disposing _dbContext failed: {ex.Message}"); }

                FlushAndShutdownLogger();
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Pipe Security

        /// <summary>
        /// Rebuilds the pipe's DACL from the accounts of the installed services and of their running processes, and
        /// recycles the instance that is waiting for a client, so the next connection is checked against the new DACL.
        /// </summary>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task that completes when the DACL has been rebuilt.</returns>
        internal Task RefreshPipeSecurityAsync(CancellationToken ct) => RefreshPipeSecurityAsync(isRetry: false, ct);

        /// <summary>
        /// Rebuilds the pipe's DACL and, when an account could not be resolved or a running service process still holds an
        /// account its service no longer names, schedules another rebuild.
        /// </summary>
        /// <param name="isRetry">Whether this is a scheduled retry; any other refresh starts a new series of retries.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task that completes when the DACL has been rebuilt.</returns>
        private async Task RefreshPipeSecurityAsync(bool isRetry, CancellationToken ct)
        {
            // Taken before the read, so the number orders the rebuilds by WHEN THEY READ the services rather than by
            // when they finish resolving the accounts, which can take seconds for a domain account (#7365)
            long generation;
            lock (_securityLock)
            {
                generation = ++_refreshStarted;
            }

            IEnumerable<string> accounts;
            IEnumerable<ServiceDto> services;
            try
            {
                services = await _serviceRepository!.GetAllAsync(decrypt: false, ct);
                accounts = ServyExePermissionsHardener.GetServiceAccounts(services);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Keep the previous DACL rather than opening the pipe wider or closing it to every service
                _logger?.Error("Failed to read the service accounts to build the Servy host named pipe DACL.", ex);
                lock (_securityLock)
                {
                    if (_pipeSecurity == null)
                        _pipeSecurity = ServyHostPipeSecurity.Create((IEnumerable<System.Security.Principal.SecurityIdentifier>?)null);
                }
                return;
            }

            var unresolved = 0;
            var grantees = ServyHostPipeSecurity.ResolveGrantees(accounts, account =>
            {
                var sid = ResolveAccount(account);
                if (sid == null) unresolved++;
                return sid;
            }).ToList();

            // A service reinstalled under another account keeps running under the old one until it restarts, and that
            // process still has to report its PID and runtime state, at the latest when it stops (#7330). So the account
            // of every running service process is granted too, for as long as that process runs.
            var lingering = 0;
            foreach (var sid in GetRunningServiceAccounts(services))
            {
                if (grantees.Contains(sid))
                    continue;

                grantees.Add(sid);
                lingering++;
            }

            var security = ServyHostPipeSecurity.Create(grantees);
            CancellationTokenSource[] waiting;
            lock (_securityLock)
            {
                // A rebuild that read the services later has already published, so its DACL is the newer one and this
                // one would drop the accounts only that later read saw. Its ScheduleUnresolvedAccountRetry decision is
                // the newer one too, so this rebuild makes none (#7365)
                if (generation < _refreshPublished)
                    return;

                _refreshPublished = generation;
                _pipeSecurity = security;
                waiting = _waitingInstanceCts.ToArray();
            }

            _logger?.Debug($"Servy host named pipe DACL rebuilt for {accounts.Count()} service account(s).");

            foreach (var cts in waiting)
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
            }

            ScheduleUnresolvedAccountRetry(unresolved, lingering, isRetry);
        }

        /// <summary>
        /// Gets the accounts the running processes of the installed services run under, other than Local System.
        /// </summary>
        /// <param name="services">The installed services.</param>
        /// <returns>The distinct SIDs.</returns>
        private List<System.Security.Principal.SecurityIdentifier> GetRunningServiceAccounts(IEnumerable<ServiceDto> services)
        {
            var localSystem = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null);
            var result = new List<System.Security.Principal.SecurityIdentifier>();
            foreach (var service in services ?? Enumerable.Empty<ServiceDto>())
            {
                if (service == null || string.IsNullOrWhiteSpace(service.Name))
                    continue;

                int pid;
                try
                {
                    pid = _windowsServiceApi.GetServiceProcessId(service.Name!);
                }
                catch (Exception ex)
                {
                    _logger?.Debug($"Could not read the process of service '{service.Name}' to keep its account on the Servy host named pipe: {ex.Message}");
                    continue;
                }

                if (pid <= 0)
                    continue;

                var sid = ResolveProcessAccount(pid);
                if (sid == null || sid.Equals(localSystem) || result.Contains(sid))
                    continue;

                result.Add(sid);
            }

            return result;
        }

        /// <summary>
        /// Rebuilds the DACL again later when an account could not be resolved, for example a domain or gMSA account while
        /// no domain controller is reachable at boot, so its service is not locked out until the next install or restart;
        /// or when a running process still holds an account its service no longer names, so that account is revoked once
        /// the process has exited.
        /// </summary>
        /// <param name="unresolved">The number of accounts the last rebuild could not resolve.</param>
        /// <param name="lingering">The number of accounts granted only because a running service process still uses them.</param>
        /// <param name="isRetry">Whether the last rebuild was itself a retry.</param>
        private void ScheduleUnresolvedAccountRetry(int unresolved, int lingering, bool isRetry)
        {
            int attempt;
            CancellationToken token;
            lock (_securityLock)
            {
                if (!isRetry || unresolved == 0)
                    _unresolvedRetryAttempts = 0;

                // Only while the listener runs: its token is what stops a waiting retry when the host stops. Unresolved
                // accounts are retried a bounded number of times; a lingering account is rechecked until its process exits.
                var listener = _cancellationSource;
                var retryUnresolved = unresolved > 0 && _unresolvedRetryAttempts < AppConfig.ServyHostUnresolvedAccountRetryCount;
                if ((!retryUnresolved && lingering == 0) || listener == null || _unresolvedRetryPending)
                    return;

                try { token = listener.Token; } catch (ObjectDisposedException) { return; }
                _unresolvedRetryPending = true;
                attempt = retryUnresolved ? ++_unresolvedRetryAttempts : _unresolvedRetryAttempts;
            }

            if (unresolved > 0)
                _logger?.Warn($"{unresolved} service account(s) could not be resolved and are not granted access to the Servy host named pipe yet. " +
                    $"Retrying in {UnresolvedAccountRetryDelayMs} ms (attempt {attempt} of {AppConfig.ServyHostUnresolvedAccountRetryCount}).");
            if (lingering > 0)
                _logger?.Info($"{lingering} account(s) keep access to the Servy host named pipe while a service process still runs under them " +
                    $"(the service was reinstalled under another account and has not restarted yet). Rechecking in {UnresolvedAccountRetryDelayMs} ms.");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(UnresolvedAccountRetryDelayMs, token);
                }
                catch (OperationCanceledException)
                {
                    lock (_securityLock) { _unresolvedRetryPending = false; }
                    return;
                }

                lock (_securityLock) { _unresolvedRetryPending = false; }

                try
                {
                    await RefreshPipeSecurityAsync(isRetry: true, token);
                }
                catch (OperationCanceledException)
                {
                    // The host is stopping
                }
                catch (Exception ex)
                {
                    _logger?.Error("Failed to rebuild the Servy host named pipe DACL for the accounts that could not be resolved.", ex);
                }
            });
        }

        /// <summary>
        /// Gets the DACL new server instances are created with.
        /// </summary>
        internal PipeSecurity? CurrentPipeSecurity
        {
            get { lock (_securityLock) { return _pipeSecurity; } }
        }

        /// <summary>
        /// Resolves an account name to its SID.
        /// </summary>
        /// <param name="account">The account name.</param>
        /// <returns>The SID, or <see langword="null"/> when it cannot be resolved.</returns>
        internal Func<string, System.Security.Principal.SecurityIdentifier?> ResolveAccount { get; set; } = AccountSidResolver.Resolve;

        /// <summary>
        /// Gets or sets the function that reads the account a running process runs under; tests replace it.
        /// </summary>
        internal Func<int, System.Security.Principal.SecurityIdentifier?> ResolveProcessAccount { get; set; } = ProcessAccount.TryGetUser;

        /// <summary>
        /// Creates a server instance of the pipe that only clients on this computer can open (<c>PIPE_REJECT_REMOTE_CLIENTS</c>),
        /// with the given security, and applies that security to the pipe itself.
        /// </summary>
        /// <param name="pipeName">The pipe name.</param>
        /// <param name="security">The DACL of the instance.</param>
        /// <returns>The server instance.</returns>
        /// <remarks>
        /// Every instance of a pipe name shares ONE security descriptor: the one the first instance was created with.
        /// The security passed to <c>CreateNamedPipe</c> for any later instance is ignored, and the host always keeps
        /// instances open, so a DACL rebuilt by <see cref="RefreshPipeSecurityAsync"/> would never reach the pipe until
        /// the host restarted: an account granted after the host started (a service installed or moved to
        /// <c>NT AUTHORITY\NetworkService</c>, a local, domain or gMSA account) was refused with "Access to the path is
        /// denied", and a revoked account kept its access. Writing the DACL through the new instance's handle replaces
        /// the shared descriptor, which is why the instance is opened with WRITE_DAC (see <see cref="LocalPipeServer.Create"/>).
        /// </remarks>
        [ExcludeFromCodeCoverage]
        private static NamedPipeServerStream CreateServerStream(string pipeName, PipeSecurity security)
            => LocalPipeServer.Create(pipeName, security);

        #endregion

        #region IPC Request Handling

        /// <summary>
        /// Handles one connection: reads the request, identifies the caller, answers, and closes the pipe.
        /// </summary>
        /// <param name="pipe">The connected server pipe stream; disposed by this method.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous request processing.</returns>
        private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
        {
            using (pipe)
            using (var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            // A pending pipe read does not observe its token on every runtime (.NET Framework starts the overlapped
            // read and never cancels it); closing the pipe aborts it everywhere.
            using (requestCts.Token.Register(() => DisposeQuietly(pipe)))
            {
                requestCts.CancelAfter(RequestTimeoutMs);
                try
                {
                    var request = await _namedPipesService.ReadAsync<IpcRequestDto>(pipe, requestCts.Token);
                    if (request == null) return;

                    var caller = _callerIdentifier.Identify(pipe);
                    var response = await ProcessRequestAsync(request, caller, requestCts.Token);

                    await _namedPipesService.WriteAsync(pipe, response, requestCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger?.Warn("A Servy host named pipe client did not complete its request in time; the connection was closed.");
                }
                catch (OperationCanceledException)
                {
                    // Shutting down
                }
                catch (Exception) when (requestCts.IsCancellationRequested)
                {
                    // The pipe was closed under the pending read or write by the timeout or the shutdown
                    if (!ct.IsCancellationRequested)
                        _logger?.Warn("A Servy host named pipe client did not complete its request in time; the connection was closed.");
                }
                catch (Exception ex)
                {
                    _logger?.Error("Exception in HandleConnectionAsync.", ex);
                }
            }
        }

        /// <summary>
        /// Disposes a pipe, ignoring the failures of a pipe that is already broken or closed.
        /// </summary>
        /// <param name="pipe">The pipe.</param>
        private static void DisposeQuietly(Stream pipe)
        {
            try { pipe.Dispose(); } catch (Exception) { /* already broken or closed */ }
        }

        /// <summary>
        /// Authorizes and executes one request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="caller">The client that sent it.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The response to send back; never <see langword="null"/>.</returns>
        internal async Task<IpcResponseDto> ProcessRequestAsync(IpcRequestDto request, PipeCaller caller, CancellationToken ct)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (caller == null) throw new ArgumentNullException(nameof(caller));

            try
            {
                // Local only: a remote client's process ID is a process on another computer, so neither the service
                // authorization nor anything else here applies to it
                if (caller.IsRemote)
                    return Deny(request, caller, "the client is on another computer; the Servy host pipe is local only");

                if (request.Action == AppConfig.ServyHostRefreshPipeAccessAction)
                {
                    if (!caller.IsAdministrator)
                        return Deny(request, caller, "only an administrator can refresh the pipe access");

                    await RefreshPipeSecurityAsync(ct);
                    return new IpcResponseDto { Success = true };
                }

                if (!IsKnownServiceActionCheck(request.Action))
                    return Fail($"Unknown IPC action: {request.Action}");

                if (string.IsNullOrWhiteSpace(request.ServiceName))
                    return Fail("A service name is required.");

                var serviceName = request.ServiceName!;
                if (!IsAuthorized(serviceName, caller))
                    return Deny(request, caller, $"process {caller.ProcessId} is not the process of service '{serviceName}'");

                switch (request.Action)
                {
                    case AppConfig.ServyHostGetByNameAction:
                        {
                            var serviceDto = await _serviceRepository!.GetByNameAsync(serviceName, decrypt: true, ct);
                            if (serviceDto != null)
                            {
                                // The wrapper never needs the account's password; never send it over the pipe
                                serviceDto.Password = null;
                            }
                            _logger?.Debug($"IPC GetByName request for '{serviceName}': {(serviceDto != null ? "Found" : "Not Found")}");
                            return new IpcResponseDto { Success = true, Data = serviceDto };
                        }
                    case AppConfig.ServyHostUpdateRuntimeStateAction:
                        {
                            if (request.RuntimeState == null)
                                return Fail("The runtime state is required.");

                            var updated = await _serviceRepository!.UpdateRuntimeStateAsync(serviceName, request.RuntimeState, ct);
                            _logger?.Debug($"IPC UpdateRuntimeState request for '{serviceName}': {updated} row(s) updated.");
                            return new IpcResponseDto { Success = true, UpdateData = updated };
                        }
                    case AppConfig.ServyHostGetRestartAttemptsAction:
                        {
                            var attempts = await _serviceRepository!.GetRestartAttemptsAsync(serviceName, ct);
                            return new IpcResponseDto { Success = true, RestartAttempts = attempts ?? new RestartAttemptsDto() };
                        }
                    case AppConfig.ServyHostUpdateRestartAttemptsAction:
                        {
                            if (!request.RestartAttempts.HasValue || request.RestartAttempts.Value < 0)
                                return Fail("A non-negative restart attempts counter is required.");

                            var updated = await _serviceRepository!.UpdateRestartAttemptsAsync(serviceName, request.RestartAttempts.Value, UtcNow(), ct);
                            return new IpcResponseDto { Success = true, UpdateData = updated };
                        }
                    default:
                        // Unreachable while every action IsKnownServiceAction admits has a case above. An action added
                        // there without one fails here instead of running another action's handler.
                        return Fail($"Unknown IPC action: {request.Action}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Error($"IPC request '{request.Action}' for '{request.ServiceName}' failed.", ex);
                return Fail($"The request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether an action is one of the per-service actions.
        /// </summary>
        /// <param name="action">The requested action.</param>
        /// <returns><see langword="true"/> for the per-service actions.</returns>
        private static bool IsKnownServiceAction(string? action)
            => action == AppConfig.ServyHostGetByNameAction
               || action == AppConfig.ServyHostUpdateRuntimeStateAction
               || action == AppConfig.ServyHostGetRestartAttemptsAction
               || action == AppConfig.ServyHostUpdateRestartAttemptsAction;

        /// <summary>
        /// Determines whether a caller may read or write a service's data: an administrator (or Local System), or the
        /// process the Service Control Manager runs the service in.
        /// </summary>
        /// <param name="serviceName">The service the request is about.</param>
        /// <param name="caller">The client.</param>
        /// <returns><see langword="true"/> when the caller is authorized.</returns>
        private bool IsAuthorized(string serviceName, PipeCaller caller)
        {
            if (caller.IsAdministrator)
                return true;

            if (caller.ProcessId <= 0)
                return false;

            return _windowsServiceApi.GetServiceProcessId(serviceName) == caller.ProcessId;
        }

        /// <summary>
        /// Builds a refusal and logs it.
        /// </summary>
        /// <param name="request">The refused request.</param>
        /// <param name="caller">The client.</param>
        /// <param name="reason">Why it was refused.</param>
        /// <returns>A failed response.</returns>
        private IpcResponseDto Deny(IpcRequestDto request, PipeCaller caller, string reason)
        {
            _logger?.Warn($"Refused IPC request '{request.Action}' for '{request.ServiceName}' from process {caller.ProcessId}: {reason}.");
            return Fail("Access denied.");
        }

        /// <summary>
        /// Builds a failed response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>A failed response.</returns>
        private static IpcResponseDto Fail(string message) => new IpcResponseDto { Success = false, ErrorMessage = message };

        #endregion

        #region Teardown

        /// <summary>
        /// Safely flushes and shuts down active loggers within a strict timeout period.
        /// </summary>
        private void FlushAndShutdownLogger()
        {
            IServyLogger? toDispose = Interlocked.Exchange(ref _logger, null);

            try
            {
                var flushTask = Task.Run(() =>
                {
                    try { Logger.Shutdown(); } catch { }
                    try { toDispose?.Dispose(); } catch { }
                });

                if (!flushTask.Wait(AppConfig.LoggerFlushTimeoutMs))
                {
                    _ = flushTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch
            {
                // Fail-silent per contract
            }
        }

        /// <summary>
        /// Terminates the host process with the specified exit code.
        /// </summary>
        /// <param name="exitCode">The exit code to report to the operating system.</param>
        [ExcludeFromCodeCoverage]
        protected virtual void TerminateProcess(int exitCode)
        {
            Environment.Exit(exitCode);
        }

        #endregion
    }
}
