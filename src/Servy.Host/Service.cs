using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Host.Bootstrap;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.ServiceProcess;

namespace Servy.Host
{
    /// <summary>
    /// Implements the primary Servy Host background Windows service that provides secure IPC configuration and state management.
    /// </summary>
    public partial class Service : ServiceBase
    {
        #region Private Fields

        private readonly INamedPipesService _namedPipesService;
        private readonly SecureData? _secureData;
        private IServyLogger? _logger;
        private readonly IServiceBootstrapEnvironment? _bootstrapEnvironment;
        private CancellationTokenSource? _cancellationSource;
        private readonly IServiceRepository? _serviceRepository;
        private volatile bool _disposed = false;
        private readonly IAppDbContext? _dbContext;
        private readonly ProtectedKeyProvider? _protectedKeyProvider;
        private Task? _listenerTask;

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
        /// Initializes a new instance of the <see cref="Service"/> class with an explicitly injected service repository for unit and integration testing.
        /// </summary>
        /// <param name="logger">The logger instance to use for service logging.</param>
        /// <param name="namedPipesService">The named pipe service instance to use for asynchronous IPC communications.</param>
        /// <param name="serviceRepository">The service repository instance to use for database operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/>, <paramref name="namedPipesService"/>, or <paramref name="serviceRepository"/> is <see langword="null"/>.</exception>
        internal Service(
            IServyLogger logger,
            INamedPipesService namedPipesService,
            IServiceRepository serviceRepository
            )
        {
            ServiceName = AppConfig.EventSource;
            AutoLog = false;

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _namedPipesService = namedPipesService ?? throw new ArgumentNullException(nameof(namedPipesService));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
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

            _bootstrapEnvironment.InitializeLogger("Servy.Host.log");

            try
            {
                ServiceName = AppConfig.EventSource;
                _bootstrapEnvironment.EnsureEventSourceExists();

                var config = _bootstrapEnvironment.BuildConfiguration();
                var coreSettings = _bootstrapEnvironment.LoadCoreSettings();
                var connectionString = coreSettings.ConnectionString;
                var aesKeyFilePath = coreSettings.AESKeyFilePath;
                var aesIVFilePath = coreSettings.AESIVFilePath;

                _bootstrapEnvironment.ConfigureLogging(config, instanceLogger: _logger);
                CoreSettingsLoader.WarnAboutIgnoredSettings(config, "appsettings.host.json", _logger);

                var isEventLogEnabled = ConfigParser.ParseBool(config["EnableEventLog"], AppConfig.DefaultEnableEventLog, "EnableEventLog");
                AutoLog = isEventLogEnabled;

                if (!_bootstrapEnvironment.IsSqliteVersionSafe(out var detectedVersion))
                {
                    _bootstrapEnvironment.LogError($"[FATAL] Vulnerable SQLite version detected: {detectedVersion}. " +
                                     $"Minimum required: {AppConfig.MinRequiredSqliteVersion} (CVE-2025-6965 mitigation).");

                    Environment.ExitCode = AppConfig.ServiceSpecificErrorCode;
                    TerminateProcess(Environment.ExitCode);
                }

                var dataStack = _bootstrapEnvironment.CreateDataStack(connectionString, aesKeyFilePath, aesIVFilePath);

                _dbContext = dataStack.DbContext;
                _protectedKeyProvider = dataStack.ProtectedKeyProvider;
                _secureData = dataStack.SecureData;
                _serviceRepository = dataStack.ServiceRepository;

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
        /// Executes when a Start command is sent to the service by the Service Control Manager (SCM). Spawns the background Named Pipe listener loop.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                _cancellationSource = new CancellationTokenSource();
                var token = _cancellationSource.Token;

                // Run IPC listener asynchronously in a background task so OnStart returns promptly to SCM
                _listenerTask = Task.Run(() => ListenForPipeConnectionsAsync(token), token);
            }
            catch (Exception ex)
            {
                _logger?.Error("Exception in OnStart.", ex);
                if (ExitCode == 0) ExitCode = AppConfig.ServiceSpecificErrorCode;
                Stop();
            }
        }

        /// <summary>
        /// Continuously accepts incoming Named Pipe client connections asynchronously until cancellation is requested.
        /// </summary>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous listener loop execution.</returns>
        private async Task ListenForPipeConnectionsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using (var serverStream = new NamedPipeServerStream(
                        AppConfig.ServyHostNamedPipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        // Wait asynchronously for a client to connect
                        await serverStream.WaitForConnectionAsync(ct);

                        if (serverStream.IsConnected)
                        {
                            await HandleClientAsync(serverStream, ct);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error accepting Named Pipe connection.", ex);
                }
            }
        }

        /// <summary>
        /// Executes when a Stop command is sent to the service by the Service Control Manager (SCM). Cancels active background tasks and flushes loggers.
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                _cancellationSource?.Cancel();
                _listenerTask?.Wait(2000);
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

        #region IPC Request Handling

        /// <summary>
        /// Asynchronously processes an individual client IPC request received over the Named Pipe stream.
        /// </summary>
        /// <param name="pipe">The connected server pipe stream.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous request processing.</returns>
        private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
        {
            try
            {
                var request = await _namedPipesService.ReadAsync<IpcRequestDto>(pipe, ct);
                if (request == null) return;

                IpcResponseDto response = new IpcResponseDto();

                switch (request.Action)
                {
                    case AppConfig.ServyHostGetByNameAction:
                        {
                            if (!string.IsNullOrEmpty(request.ServiceName))
                            {
                                var serviceDto = await _serviceRepository!.GetByNameAsync(request.ServiceName!, decrypt: true);
                                response.Success = serviceDto != null;
                                response.Data = serviceDto;
                                _logger?.Debug($"IPC GetByName request for '{request.ServiceName}': {(response.Success ? "Found" : "Not Found")}");
                            }
                            break;
                        }
                    case AppConfig.ServyHostUpdateAction:
                        {
                            if (request.Data != null)
                            {
                                var res = await _serviceRepository!.UpdateAsync(
                                    request.Data,
                                    preserveExistingRuntimeState: false,
                                    preserveExistingCredentials: true);
                                response.UpdateData = res;
                                response.Success = true;
                                _logger?.Debug($"IPC Update request for '{request.Data.Name}': Success");
                            }
                            break;
                        }
                    default:
                        {
                            response.Success = false;
                            response.ErrorMessage = $"Unknown IPC action: {request.Action}";
                            break;
                        }
                }

                await _namedPipesService.WriteAsync(pipe, response, ct);
            }
            catch (Exception ex)
            {
                _logger?.Error("Exception in HandleClientAsync.", ex);
            }
        }

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
