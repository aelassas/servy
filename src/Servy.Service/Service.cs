using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static Servy.Core.Native.NativeMethods;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service
{
    public partial class Service : ServiceBase, IDisposable
    {
        #region Enums

        /// <summary>
        /// Specifies the reason the service teardown sequence was initiated.
        /// </summary>
        private enum TeardownReason
        {
            /// <summary>
            /// The service was stopped manually via the Service Control Manager or a stop command.
            /// </summary>
            Stop,

            /// <summary>
            /// The system is preparing to shut down. This provides an early notification 
            /// with an extended timeout window before the standard shutdown begins.
            /// </summary>
            PreShutdown,

            /// <summary>
            /// The system is shutting down. This is the standard final notification 
            /// and typically has a very short timeout window.
            /// </summary>
            Shutdown,
        }

        #endregion

        #region Constants

        /// <summary>
        /// The command-line argument used to signal that the service is running in a test context.
        /// When this flag is present in the startup arguments, certain environment-specific 
        /// initializations (like Win32 handle reflection) are bypassed.
        /// </summary>
        public const string TestModeFlag = "servy_test";

        /// <summary>
        /// The namespace in the assembly where the embedded service resources are located.
        /// </summary>
        private const string ResourcesNamespace = "Servy.Service.Resources";

        /// <summary>
        /// The base file name (without extension) of the embedded Servy Restarter executable.
        /// </summary>
        private const string ServyRestarterExeFileName = "Servy.Restarter.Net48";

        #endregion

        #region Private Fields

        /// <summary>The interval, in milliseconds, at which the launcher checks the process status and invokes the OnScmHeartbeat delegate.</summary>
        private readonly int _waitChunkMs;
        /// <summary>Additional time, in milliseconds, used for Service Control Manager (SCM) operations.</summary>
        private readonly int _scmAdditionalTimeMs;

        private readonly SecureData _secureData;
        private readonly Helpers.IServiceHelper _serviceHelper;
        private IServyLogger _logger;
        private readonly IStreamWriterFactory _streamWriterFactory;
        private readonly ITimerFactory _timerFactory;
        private readonly IProcessFactory _processFactory;
        private readonly IPathValidator _pathValidator;
        private string _serviceName;
        private string _realExePath;
        private string _realArgs;
        private string _workingDir;
        private IProcessWrapper _childProcess;
        private IStreamWriter _stdoutWriter;
        private IStreamWriter _stderrWriter;
        private ITimer _healthCheckTimer;
        private int _maxFailedChecks;
        private int _failedChecks = 0;
        private RecoveryAction _recoveryAction;
        private readonly object _healthCheckLock = new object();
        private readonly object _teardownLock = new object();
        private readonly object _fileLock = new object();
        private volatile bool _isRecovering = false;
        private int _maxRestartAttempts = AppConfig.DefaultMaxRestartAttempts; // Maximum number of restart attempts
        private List<EnvironmentVariable> _environmentVariables = new List<EnvironmentVariable>();
        private bool _recoveryActionEnabled = false;
        private string _restartAttemptsFile;
        private bool _preLaunchEnabled;
        private StartOptions _options;
        private CancellationTokenSource _cancellationSource;
        private readonly IServiceRepository _serviceRepository;
        private readonly List<Hook> _trackedHooks = new List<Hook>();
        private IntPtr _serviceHandle;
        private uint _checkPoint = 0;
        private volatile bool _disposed = false; // Tracks whether Dispose has been called
        private volatile bool _isTearingDown = false;
        private volatile bool _isRebooting = false;
        private readonly IProcessHelper _processHelper;
        private readonly IProcessKiller _processKiller;
        private readonly IAppDbContext _dbContext;
        private readonly ProtectedKeyProvider _protectedKeyProvider;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the service stops, used for testing purposes.
        /// </summary>
        public event Action OnStoppedForTest;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class
        /// using the default production implementations for all dependencies.
        /// </summary>
        /// <remarks>
        /// This constructor is intended for the Windows Service Control Manager (SCM). 
        /// It performs full subsystem initialization, including logging, database connectivity, 
        /// and cryptographic setup.
        /// </remarks>
        public Service() : this(
            new Helpers.ServiceHelper(new CommandLineProvider(), new Core.Helpers.ProcessHelper()),
            new EventLogLogger(AppConfig.EventSource),
            new StreamWriterFactory(),
            new TimerFactory(),
            new ProcessFactory(),
            new PathValidator(),
            new Core.Helpers.ProcessHelper(),
            new ProcessKiller()
          )
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class with full dependency injection.
        /// </summary>
        /// <param name="serviceHelper">The service helper.</param>
        /// <param name="logger">The logger wrapper.</param>
        /// <param name="streamWriterFactory">The stream writer factory.</param>
        /// <param name="timerFactory">The timer factory.</param>
        /// <param name="processFactory">The process factory.</param>
        /// <param name="pathValidator">The path validator.</param>
        /// <param name="serviceRepository">The service repository.</param>
        /// <param name="processHelper">The process helper.</param>
        /// <param name="processKiller">The process killer.</param>
        /// <remarks>
        /// <b>NOTE:</b> This constructor is primarily intended for <b>Unit Testing</b> and <b>Inversion of Control (IoC)</b> containers.
        /// <para>
        /// Unlike the production constructors, this version <b>DOES NOT</b>:
        /// <list type="bullet">
        /// <item><description>Initialize the global <see cref="Logger"/> utility.</description></item>
        /// <item><description>Setup the <see cref="SecureData"/> cryptographic subsystem.</description></item>
        /// <item><description>Initialize the database schema or embedded resources.</description></item>
        /// </list>
        /// The caller is responsible for ensuring any required global state is initialized prior to use.
        /// </para>
        /// </remarks>
        public Service(
            Helpers.IServiceHelper serviceHelper,
            IServyLogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory,
            IPathValidator pathValidator,
            IServiceRepository serviceRepository,
            IProcessHelper processHelper,
            IProcessKiller processKiller
            ) // allow injection
        {
            ServiceName = AppConfig.EventSource;

            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamWriterFactory = streamWriterFactory ?? throw new ArgumentNullException(nameof(streamWriterFactory));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
            _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
            _options = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class with core dependencies and performs production setup.
        /// </summary>
        /// <param name="serviceHelper">The service helper instance to use.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <param name="streamWriterFactory">Factory to create rotating stream writers for stdout and stderr.</param>
        /// <param name="timerFactory">Factory to create timers for health monitoring.</param>
        /// <param name="processFactory">Factory to create process wrappers for launching and managing child processes.</param>
        /// <param name="pathValidator">Path Validator.</param>
        /// <param name="processHelper">Helper used to enumerate, inspect, and snapshot child processes.</param>
        /// <param name="processKiller">Helper used to terminate processes (and process trees) during teardown.</param>
        /// <remarks>
        /// This is the primary <b>Production Constructor</b>. It automatically initializes the 
        /// <see cref="Logger"/>, validates the Windows Event Source, loads configuration from 
        /// <c>appsettings.json</c>, and initializes the <see cref="SecureData"/> and database systems.
        /// </remarks>
        public Service(
            Helpers.IServiceHelper serviceHelper,
            IServyLogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory,
            IPathValidator pathValidator,
            IProcessHelper processHelper,
            IProcessKiller processKiller
            )
        {
            Logger.Initialize("Servy.Service.log");

            try
            {
                ServiceName = AppConfig.EventSource;

                // Ensure event source exists
                Helper.EnsureEventSourceExists();

                _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _streamWriterFactory = streamWriterFactory ?? throw new ArgumentNullException(nameof(streamWriterFactory));
                _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
                _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
                _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
                _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
                _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
                _options = null;

                // Load configuration
                var config = ConfigurationManager.AppSettings;

                var connectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
                var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                if (int.TryParse(config["Timing:WaitChunkMs"], out var waitChunkMs) && waitChunkMs > 0)
                {
                    _waitChunkMs = waitChunkMs;
                }
                else
                {
                    _waitChunkMs = AppConfig.DefaultWaitChunkMs;
                }

                if (int.TryParse(config["Timing:ScmAdditionalTimeMs"], out var scmAdditionalTimeMs) && scmAdditionalTimeMs > 0)
                {
                    _scmAdditionalTimeMs = scmAdditionalTimeMs;
                }
                else
                {
                    _scmAdditionalTimeMs = AppConfig.DefaultScmAdditionalTimeMs;
                }

                // Centralized logging bootstrapper
                LoggerConfigurator.ConfigureFromAppSettings(config, instanceLogger: _logger);

                // --- Log Service-specific timing configurations ---
                Logger.Debug("Servy Service Context Configuration Loaded:" + Environment.NewLine +
                    $"  WaitChunkMs: {_waitChunkMs}" + Environment.NewLine +
                    $"  ScmAdditionalTimeMs: {_scmAdditionalTimeMs}");

                // Initialize database and helpers
                _dbContext = new AppDbContext(connectionString);
                DatabaseInitializer.InitializeDatabase(_dbContext, SQLiteDbInitializer.Initialize);

                var dapperExecutor = new DapperExecutor(_dbContext);
                _protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                _secureData = new SecureData(_protectedKeyProvider);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();

                _serviceRepository = new ServiceRepository(dapperExecutor, _secureData, xmlSerializer, jsonSerializer);

                // Copy service executable from embedded resources
                var asm = Assembly.GetExecutingAssembly();
                var sh = new Core.Helpers.ServiceHelper(_serviceRepository);
                var resourceHelper = new ResourceHelper(sh, _processKiller);

                if (!resourceHelper.CopyEmbeddedResourceForceSync(asm, ResourcesNamespace, ServyRestarterExeFileName, "exe"))
                {
                    _logger?.Error($"Failed copying embedded resource: {ServyRestarterExeFileName}.exe");
                }

#if DEBUG
                // Copy debug symbols from embedded resources (only in debug builds)
                if (!resourceHelper.CopyEmbeddedResourceForceSync(asm, ResourcesNamespace, ServyRestarterExeFileName, "pdb"))
                {
                    _logger?.Error($"Failed copying embedded resource: {ServyRestarterExeFileName}.pdb");
                }
#endif

                // Enable Shutdown Notifications
                CanShutdown = true;
            }
            catch (Exception ex)
            {
                // Without Logger.Initialize in the constructor, this would be lost.
                Logger.Error("Fatal error during service construction.", ex);

                // If the environment exit code was successfully set to a custom error 
                // (like 13 from ProtectedKeyProvider), preserve it. Otherwise, set a generic service failure code.
                if (Environment.ExitCode == 0)
                {
                    Environment.ExitCode = AppConfig.ServiceSpecificErrorCode; // ERROR_SERVICE_SPECIFIC_ERROR
                }

                // By explicitly calling Environment.Exit here, we guarantee the SCM registers 
                // the custom exit code immediately, completely preventing the 1053 timeout hang.
                Environment.Exit(Environment.ExitCode);
            }
        }

        #endregion

        /// <summary>
        /// Called when the Windows service is started.
        /// Initializes startup options, configures logging, validates working directories,
        /// runs the optional pre-launch process (if configured), and starts the monitored service process.
        /// Also sets up health monitoring for the service.
        /// <para>
        /// This method now resets the restart attempt counter at startup to ensure that
        /// previous restart limits do not persist across service restarts (including
        /// restarts triggered by <c>Servy.Restarter.Net48.exe</c>).
        /// </para>
        /// </summary>
        /// <param name="args">Command-line arguments passed to the service by the Service Control Manager.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                _ = FreeConsole();
                _ = SetConsoleCtrlHandler(null, true);

                // Check if we are running in a test context to bypass environment-specific hooks
                bool isTestMode = args.Length > 0 &&
                                  string.Equals(args[0], TestModeFlag, StringComparison.OrdinalIgnoreCase);

                // Load startup options
                var fullArgs = _serviceHelper.GetArgs();
                var options = _serviceHelper.ParseOptions(_serviceRepository, fullArgs);

                if (options == null)
                {
                    // Set a non-zero exit code so Windows knows it failed
                    ExitCode = AppConfig.ServiceSpecificErrorCode; // ERROR_SERVICE_SPECIFIC_ERROR
                    throw new InvalidOperationException("Failed to initialize service options.");
                }

                // PROMOTE LOGGER IMMEDIATELY
                // Now every log from this point forward (including validation errors) is prefixed.
                // DO NOT DISPOSE the root logger, as the scoped logger relies on its unmanaged resources 
                // (EventLog handles, FileStreams) to function.
                _logger = _logger?.CreateScoped(options.ServiceName);

                // Log and Validate using the new scoped _logger
                if (!_serviceHelper.ValidateAndLog(options, _logger))
                {
                    ExitCode = AppConfig.ServiceSpecificErrorCode; // ERROR_SERVICE_SPECIFIC_ERROR
                    Stop();
                    return;
                }

                _options = options;

                _preLaunchEnabled = !string.IsNullOrWhiteSpace(options.PreLaunchExecutablePath);

                if (!isTestMode)
                {
                    // NO REFLECTION NEEDED: Access the underlying Service Status Handle directly
                    _serviceHandle = ServiceHandle;

                    if (_serviceHandle != IntPtr.Zero)
                    {
                        _logger?.Info($"Service handle obtained natively: 0x{_serviceHandle.ToInt64():X}");
                    }
                    else
                    {
                        throw new InvalidOperationException("Native ServiceHandle is zero/invalid.");
                    }
                }

                // Ensure working directory is valid
                _serviceHelper.EnsureValidWorkingDirectory(options, _logger);

                _serviceName = options.ServiceName;
                _recoveryActionEnabled = options.EnableHealthMonitoring && options.HeartbeatInterval > 0 && options.MaxFailedChecks > 0 && options.RecoveryAction != RecoveryAction.None;
                _maxRestartAttempts = options.MaxRestartAttempts;
                _maxFailedChecks = options.MaxFailedChecks;
                _recoveryAction = options.RecoveryAction;

                // Request timeout for startup to accommodate slow process
                if (_options.StartTimeoutInSeconds > AppConfig.ScmStartupRequestThresholdSeconds) // Use a lower threshold to be safe
                {
                    _serviceHelper.RequestAdditionalTime(this, ClampTimeout(_options.StartTimeoutInSeconds + AppConfig.ScmStartupRequestBufferSeconds), _logger);
                }

                // Set up attempts file
                SetupAttemptsFile(options);

                // Set up service logging
                HandleLogWriters(options);

                // ROBUSTNESS: Instantiating the new CTS *before* updating the field ensures that
                // concurrent background tasks checking the token never experience a temporary 'null' window.
                // We create this early so that pre-launch processes can observe teardown requests.
                var cts = new CancellationTokenSource();

                // ROBUSTNESS: Capture the token structure immediately while the CTS instance is guaranteed alive.
                // This shields the fire-and-forget task from throwing an ObjectDisposedException if a rapid 
                // recovery cycle or crash loop invokes Interlocked.Exchange and disposes 'cts' down the line.
                var capturedToken = cts.Token;

                var oldCts = Interlocked.Exchange(ref _cancellationSource, cts);

                if (oldCts != null)
                {
                    try
                    {
                        oldCts.Cancel();
                    }
                    catch (ObjectDisposedException) { /* already disposed */ }
                    catch (AggregateException ex)
                    {
                        _logger?.Warn($"Exception(s) raised during CTS cancellation: {ex.Message}");
                    }
                    finally
                    {
                        oldCts.Dispose();
                    }
                }

                // Run pre-launch process if configured
                if (!StartPreLaunchProcess(options))
                {
                    // Abort if pre-launch fails and service is not allowed to start
                    Stop();
                    return;
                }

                // Start the monitored main process
                StartMonitoredProcess(options, capturedToken);

                // Reset restart attempts on service start to avoid blocking recovery
                if (_recoveryActionEnabled)
                {
                    ConditionalResetRestartAttempts(options);
                }

                // Enable service health monitoring
                SetupHealthMonitoring(options);

                // Final step: Inform SCM we are running and specifically that we accept PRESHUTDOWN
                if (_serviceHandle != IntPtr.Zero)
                {
                    // Capture the cancellation token tied to the service's current lifecycle
                    var token = _cancellationSource?.Token ?? CancellationToken.None;

                    // By wrapping this in a Task.Delay, we allow ServiceBase's internal OnStart() 
                    // sequence to complete and report SERVICE_RUNNING first. We then safely overwrite 
                    // the state to include SERVICE_ACCEPT_PRESHUTDOWN, bypassing .NET's internal limitations.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 1. Wait, but respect cancellation if Stop() races us
                            await Task.Delay(AppConfig.PreShutdownRegistrationDelayMs, token);

                            // 2. Validate state before touching the native handle
                            if (token.IsCancellationRequested || _isTearingDown || _disposed || _serviceHandle == IntPtr.Zero)
                            {
                                _logger?.Info("Skipping PRESHUTDOWN registration: Service is already tearing down.");
                                return;
                            }

                            var status = new SERVICE_STATUS
                            {
                                dwServiceType = SERVICE_WIN32_OWN_PROCESS,
                                dwCurrentState = SERVICE_RUNNING,
                                dwControlsAccepted = SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_PRESHUTDOWN,
                                dwWin32ExitCode = 0,
                                dwServiceSpecificExitCode = 0,
                                dwCheckPoint = 0,
                                dwWaitHint = 0
                            };

                            if (!SetServiceStatus(_serviceHandle, ref status))
                            {
                                int error = Marshal.GetLastWin32Error();
                                _logger?.Error($"Failed to register PRESHUTDOWN support via native Win32. Error: {error}");
                            }
                            else
                            {
                                _logger?.Info("Service signaled RUNNING to SCM with PRESHUTDOWN support natively enabled.");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // 3. Gracefully handle the race condition where Delay is aborted
                            _logger?.Info("PRESHUTDOWN registration aborted due to service shutdown.");
                        }
                        catch (Exception ex)
                        {
                            // 4. Prevent unobserved task exceptions from crashing the finalizer thread
                            _logger?.Error("Unexpected error during PRESHUTDOWN registration.", ex);
                        }
                    }, token); // Pass token to Task.Run to prevent execution if already cancelled
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Exception in OnStart.", ex);
                if (ExitCode == 0) ExitCode = AppConfig.ServiceSpecificErrorCode; // ERROR_SERVICE_SPECIFIC_ERROR
                Stop();
            }
        }

        /// <summary>
        /// Initializes the path to the restart attempts file for the current service,
        /// located under the %ProgramData%\Servy\recovery directory.  
        /// The filename is unique per service based on its name to prevent conflicts  
        /// when multiple services are managed by Servy on the same machine.
        /// </summary>
        /// <param name="options">The service startup options containing the service name.</param>
        private void SetupAttemptsFile(StartOptions options)
        {
            SecurityHelper.CreateSecureDirectory(AppConfig.RecoveryFolderPath, breakInheritance: false); // ensures folder exists

            string safeServiceName = MakeFilenameSafe(options.ServiceName);
            _restartAttemptsFile = Path.Combine(AppConfig.RecoveryFolderPath, $"{safeServiceName}_restartAttempts.dat");
        }

        /// <summary>
        /// Sanitizes a string to be safe for use as a filename by replacing
        /// all invalid filename characters with underscores ('_'). Handles DOS reserved device names
        /// and prevents filename namespace collisions.
        /// </summary>
        /// <param name="name">The original string to sanitize.</param>
        /// <returns>A sanitized string safe for use as a filename.</returns>
        public static string MakeFilenameSafe(string name)
        {
            // If null or empty, treat the base name as an underscore but still 
            // append the short hash to satisfy the unique signature layout requirement
            if (string.IsNullOrEmpty(name))
            {
                return $"_{ComputeShortHash(string.Empty)}";
            }

            // 1. Strip trailing spaces, tabs, and periods as Windows ignores these on disk handles (Issue #2069 mitigation)
            string sanitized = name.TrimEnd(' ', '.', '\t');

            // Explicit guard against directory traversal sequences or inputs that normalize to empty/dots
            if (string.IsNullOrEmpty(sanitized) || sanitized == "." || sanitized == "..")
            {
                sanitized = "_";
            }

            // 2. Replace invalid character markers
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // 3. Prevent Reserved DOS Name collisions and User-supplied namespace overlaps (Issue #2118 & #2080)
            int firstDotIndex = sanitized.IndexOf('.');
            string leadingSegment = firstDotIndex >= 0 ? sanitized.Substring(0, firstDotIndex) : sanitized;

            // Isolate the base word by stripping all leading underscores
            string baseSegment = leadingSegment.TrimStart('_');

            // Only escape if the underlying base keyword is an actual hardware reserved name
            if (ReservedNames.ReservedDeviceNames.Contains(baseSegment))
            {
                // Count how many leading underscores the user already had in their input segment
                int existingUnderscores = leadingSegment.Length - baseSegment.Length;

                // Prepend exactly one more underscore than what currently exists to break the collision chain
                string protectionPrefix = new string('_', existingUnderscores + 1);

                sanitized = protectionPrefix + baseSegment + (firstDotIndex >= 0 ? sanitized.Substring(firstDotIndex) : string.Empty);
            }

            // 4. Collision Insurance: Append a deterministic short hash of the ORIGINAL raw name input string.
            // This guarantees unique on-disk allocations for variations such as "MyService", "MyService ", and "MyService."
            string shortHash = ComputeShortHash(name);

            return $"{sanitized}_{shortHash}";
        }

        /// <summary>
        /// Computes a deterministic 6-character hex string hash from an input value.
        /// </summary>
        private static string ComputeShortHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                // 3 bytes maps cleanly to a highly unique 6-character hex identifier
                return BitConverter.ToString(bytes, 0, 3).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Ensures the restart attempts tracking file exists and retrieves the current counter value.
        /// </summary>
        /// <param name="ct">A cancellation token to observe while waiting for the semaphore or performing I/O.</param>
        /// <returns>
        /// The number of restart attempts recorded in the file. 
        /// Returns 0 if the file is missing, corrupt, or if an error occurs during retrieval.
        /// </returns>
        /// <remarks>
        /// This method uses an asynchronous semaphore to prevent race conditions during file access.
        /// If the file content is invalid or unreadable, it automatically resets the file to "0" 
        /// to maintain a clean recovery state for the managed process.
        /// </remarks>
        private int EnsureRestartAttemptsFile()
        {
            if (string.IsNullOrEmpty(_restartAttemptsFile)) return 0;

            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(_restartAttemptsFile))
                    {
                        var content = File.ReadAllText(_restartAttemptsFile).Trim();
                        if (int.TryParse(content, out var attempts) && attempts >= 0)
                            return attempts;

                        WriteAttemptsInternal(0);
                        _logger?.Warn("Corrupt or invalid content found in restart attempts file. Resetting counter to 0.");
                        return 0;
                    }
                    else
                    {
                        WriteAttemptsInternal(0);
                        _logger?.Warn("Restart attempts file not found. Initializing counter to 0.");
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"Error reading restart attempts file: {ex.Message}. Resetting counter to 0.");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Saves the current number of restart attempts to the persistent attempts file.
        /// Updates the Last Write Time, which is critical for session persistence checks.
        /// Does nothing if the attempts file path is null or empty.
        /// </summary>
        /// <param name="attempts">The restart attempts count to save.</param>
        private void SaveRestartAttempts(int attempts)
        {
            if (string.IsNullOrEmpty(_restartAttemptsFile)) return;

            lock (_fileLock)
            {
                try
                {
                    WriteAttemptsInternal(attempts);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Failed to save restart attempts to file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Internal unprotected write logic. Assumes _fileSemaphore is held by the caller.
        /// </summary>
        /// <param name="attempts">The number of restart attempts to persist.</param>
        private void WriteAttemptsInternal(int attempts)
        {
            // Use the atomic helper to ensure we don't corrupt the file on crash
            Helper.WriteFileAtomic(_restartAttemptsFile, stream =>
            {
                // Use StreamWriter for cleaner string handling.
                // We use leaveOpen: true so the Helper can perform the final Flush() on the FileStream.
                using (var sw = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
                {
                    sw.Write(attempts.ToString(CultureInfo.InvariantCulture));
                }
            });
        }

        /// <summary>
        /// Evaluates whether the persistent restart attempts counter should be reset to zero.
        /// </summary>
        /// <param name="options">The startup options containing heartbeat and timeout configurations used to calculate the stability threshold.</param>
        /// <remarks>
        /// This method performs two distinct checks:
        /// <list type="number">
        /// <item>
        /// <description>
        /// <b>Reboot Detection:</b> Compares the last write time of the restart file against the system boot time 
        /// (calculated via <see cref="GetTickCount64"/>). If the file was modified before the current OS session, 
        /// the count is maintained because the service is likely starting due to a "Restart Computer" recovery action.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Stability Reset:</b> If within the same OS session, it resets the counter only if the time elapsed 
        /// since the last failure exceeds a calculated threshold (heartbeat interval + failed check allowance + buffer). 
        /// This ensures that stable runs clear the "punishment" history, while rapid crash loops continue to increment it.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        private void ConditionalResetRestartAttempts(StartOptions options)
        {
            // If the counter is already 0, there is no need to check timestamps or files.
            // This keeps the health check efficient.
            if (EnsureRestartAttemptsFile() == 0) return;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(_restartAttemptsFile);

            // Derive system boot time context directly. Arithmetic on a 64-bit millisecond tick counter 
            // is mathematically protected against runtime overflow exceptions for ~292 million years.
            ulong uptimeMilliseconds = GetTickCount64();
            DateTime systemBootTimeUtc = DateTime.UtcNow.AddMilliseconds(-(double)uptimeMilliseconds);


            // 1. Session Persistence Check
            // If the file's last modification occurred before the current system boot, 
            // the service is starting in a new OS session. We maintain the existing counter 
            // to ensure recovery quotas (like RestartComputer) are respected across reboots.
            if (lastWriteUtc < systemBootTimeUtc)
            {
                // We are running in a new OS session for the first time.
                // Touch the file to anchor it to the current session so future
                // stability checks can decide based on in-session uptime.
                File.SetLastWriteTimeUtc(_restartAttemptsFile, DateTime.UtcNow);
                return;
            }

            // 2. Standard same-session reset logic
            long product = (long)options.HeartbeatInterval * options.MaxFailedChecks;
            int detectionWindowSeconds = product > int.MaxValue ? int.MaxValue : (int)product;

            // Base threshold: detection window + buffer (min 30s or the detection window itself)
            int bufferSeconds = Math.Max(detectionWindowSeconds, AppConfig.ConditionalResetStabilityBufferSeconds);
            int resetThresholdSeconds = detectionWindowSeconds + bufferSeconds;

            // Cap at 1 hour (the cap excludes the pre-launch budget), but ensure we always wait at least one full detection cycle
            int cap = AppConfig.ConditionalResetMaxThresholdSeconds;

            // If the detection window itself already exceeds the cap, the cap is meaningless -
            // the contract is broken at configuration time and should be logged once.
            if (detectionWindowSeconds > cap)
            {
                _logger?.Warn(
                    $"Detection window ({detectionWindowSeconds}s) exceeds the reset cap ({cap}s); " +
                    $"the configured ConditionalResetMaxThresholdSeconds will be ignored for this service.");

                resetThresholdSeconds = detectionWindowSeconds;
            }
            else
            {
                resetThresholdSeconds = Math.Min(resetThresholdSeconds, cap);
                if (resetThresholdSeconds < detectionWindowSeconds)
                    resetThresholdSeconds = detectionWindowSeconds;
            }

            if (_preLaunchEnabled)
            {
                resetThresholdSeconds += options.PreLaunchTimeoutInSeconds;
            }

            double secondsSinceLastAttempt = (DateTime.UtcNow - lastWriteUtc).TotalSeconds;
            if (secondsSinceLastAttempt > resetThresholdSeconds)
            {
                _logger?.Info($"Resetting restart attempts counter. Stable for {secondsSinceLastAttempt:F1} seconds.");
                SaveRestartAttempts(0);
            }
        }

        /// <summary>
        /// Starts the pre-launch process, if configured, before the main service process.
        /// </summary>
        /// <param name="options">The service start options containing the pre-launch configuration.</param>
        /// <returns>
        /// <c>true</c> if the pre-launch process completed successfully or was skipped; 
        /// <c>false</c> if it failed and <see cref="StartOptions.PreLaunchIgnoreFailure"/> is <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method runs the configured pre-launch executable synchronously before the main service process.
        /// It applies the specified working directory, command-line arguments, environment variables, and 
        /// optionally logs standard output and error to the provided file paths.
        /// </para>
        /// <para>
        /// The process is allowed to run for at least <see cref="AppConfig.MinPreLaunchTimeoutSeconds"/> seconds or
        /// <see cref="StartOptions.PreLaunchTimeoutInSeconds"/>, whichever is greater, per attempt.
        /// If the process exits with a non-zero code or times out, it is retried up to 
        /// <see cref="StartOptions.PreLaunchRetryAttempts"/> times.
        /// </para>
        /// <para>
        /// If <see cref="StartOptions.PreLaunchIgnoreFailure"/> is <c>true</c>, the service will continue starting 
        /// even if all attempts fail. Otherwise, the service startup will be aborted.
        /// </para>
        /// <para>
        /// If <see cref="StartOptions.PreLaunchTimeoutInSeconds"/> is set to 0, the pre-launch hook runs in fire-and-forget 
        /// mode and stdout/stderr redirection and retries are not available.
        /// </para>
        /// </remarks>
        private bool StartPreLaunchProcess(StartOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.PreLaunchExecutablePath))
            {
                _logger?.Info("No pre-launch executable configured. Skipping.");
                return true; // proceed with service start
            }

            // 1. Prepare configuration and shared options
            // Pre-Launch is the only hook with dedicated environment variables,
            // all other hooks reuse the main service environment variables.
            // This design is intentional.
            var vars = options.PreLaunchEnvironmentVariables ?? options.EnvironmentVariables;
            var args = options.PreLaunchExecutableArgs ?? string.Empty;

            var workingDir = string.IsNullOrWhiteSpace(options.PreLaunchWorkingDirectory)
                ? options.WorkingDirectory
                : options.PreLaunchWorkingDirectory;

            var fireAndForget = options.PreLaunchTimeoutInSeconds == 0;

            var launchOptions = new ProcessLaunchOptions
            {
                ExecutablePath = options.PreLaunchExecutablePath,
                Arguments = args,
                WorkingDirectory = workingDir,
                EnvironmentVariables = vars,
                WaitChunkMs = _waitChunkMs,
                ScmAdditionalTimeMs = _scmAdditionalTimeMs,
                OnScmHeartbeat = new Action<int>((time) => _serviceHelper.RequestAdditionalTime(this, time, _logger)),
                StdoutPath = options.PreLaunchStdoutPath,
                StderrPath = options.PreLaunchStderrPath,
                RedirectToWriters = !fireAndForget,
                FireAndForget = fireAndForget,
                LogErrorAsWarning = options.PreLaunchIgnoreFailure,
                EnableConsoleUI = options.EnableConsoleUI,
            };

            // 2. Handle Fire-and-Forget Mode
            // If Timeout is 0, we don't care about the exit code or output; we just launch and move on.
            if (fireAndForget)
            {
                return RunFireAndForgetPreLaunch(launchOptions, options.PreLaunchIgnoreFailure);
            }

            // 3. Handle Synchronous Mode with Retries
            launchOptions.TimeoutMs = ClampTimeout(Math.Max(options.PreLaunchTimeoutInSeconds, AppConfig.MinPreLaunchTimeoutSeconds));
            return RunSynchronousPreLaunch(launchOptions, options);
        }

        /// <summary>
        /// Converts a timeout value from seconds to milliseconds and clamps it to the maximum 
        /// allowed value for a 32-bit signed integer.
        /// </summary>
        /// <param name="timeout">The timeout value in seconds.</param>
        /// <returns>
        /// The timeout in milliseconds, or <see cref="int.MaxValue"/> if the 
        /// calculated value exceeds the capacity of a 32-bit signed integer.
        /// </returns>
        /// <remarks>
        /// This method prevents <see cref="OverflowException"/> when converting long-running 
        /// service timeouts. It ensures compatibility with underlying Win32 and .NET APIs 
        /// that require an <see cref="int"/> millisecond value.
        /// </remarks>
        private int ClampTimeout(long timeout)
        {
            // Use 1000L to force the multiplication into a 64-bit context before clamping
            return (int)Math.Min(int.MaxValue, timeout * 1000L);
        }

        /// <summary>
        /// Launches the pre-launch process in fire-and-forget mode and tracks it for cleanup.
        /// </summary>
        /// <param name="launchOptions">The initialized process launch parameters.</param>
        /// <param name="ignoreFailure">If <see langword="true"/>, the service will start even if the process fails to launch.</param>
        /// <returns><see langword="true"/> if launched successfully or if failures are suppressed.</returns>
        private bool RunFireAndForgetPreLaunch(ProcessLaunchOptions launchOptions, bool ignoreFailure)
        {
            try
            {
                _logger?.Info($"Running pre-launch program (fire-and-forget): {launchOptions.ExecutablePath}");
                _logger?.Info("Redirection and retries are ignored in fire-and-forget mode.");

                var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger);

                if (process?.UnderlyingProcess is Process nativeProcess)
                {
                    lock (_trackedHooks)
                    {
                        // We track this so if the main service stops while the fire-and-forget 
                        // process is still running, we can kill the orphan.
                        _trackedHooks.Add(new Hook { OperationName = "pre-launch", Process = nativeProcess });
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to launch fire-and-forget pre-launch process: {ex.Message}");
                return ignoreFailure;
            }
        }

        /// <summary>
        /// Executes the pre-launch process synchronously, handling retries and exit code evaluation.
        /// </summary>
        /// <param name="launchOptions">The initialized process launch parameters.</param>
        /// <param name="options">The original start options for access to retry and ignore settings.</param>
        /// <returns>
        /// <see langword="true"/> if the process eventually returned exit code 0 or if failures are configured to be ignored.
        /// </returns>
        private bool RunSynchronousPreLaunch(ProcessLaunchOptions launchOptions, StartOptions options)
        {
            int maxAttempts = options.PreLaunchRetryAttempts + 1;
            bool ignoreFailure = options.PreLaunchIgnoreFailure;

            // Local function to handle conditional logging based on PreLaunchIgnoreFailure
            void LogIssue(string message, Exception ex = null)
            {
                if (ignoreFailure)
                    _logger?.Warn(message, ex);
                else if (ex != null)
                    _logger?.Error(message, ex);
                else
                    _logger?.Error(message);
            }

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Short-circuit check before starting an attempt
                if (_isTearingDown || (_cancellationSource?.IsCancellationRequested == true))
                {
                    LogIssue("Pre-launch process aborted due to service teardown.");
                    return false;
                }

                _logger?.Info($"Starting pre-launch process (attempt {attempt}/{maxAttempts})...");

                try
                {
                    // ProcessLauncher.Start handles the ScmHeartbeat pulses during the wait.
                    using (var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger))
                    {
                        if (process.ExitCode == 0)
                        {
                            _logger?.Info("Pre-launch process completed successfully.");
                            return true;
                        }

                        LogIssue($"Pre-launch process exited with code {process.ExitCode}.");
                    }
                }
                catch (Exception ex)
                {
                    LogIssue($"Pre-launch process attempt {attempt} failed.", ex);
                }

                // Apply back-off only if further retries remain
                if (attempt < maxAttempts)
                {
                    // Calculate delay: Linear back-off (attempt * initial delay) capped at max delay
                    int delayMs = Math.Min(attempt * AppConfig.PreLaunchRetryInitialDelayMs, AppConfig.PreLaunchRetryMaxDelayMs);

                    _logger?.Info($"Waiting {delayMs}ms before pre-launch retry {attempt + 1}/{maxAttempts}...");

                    int elapsed = 0;
                    while (elapsed < delayMs)
                    {
                        // Token/Teardown-aware short-circuit
                        if (_isTearingDown || (_cancellationSource?.IsCancellationRequested == true))
                        {
                            LogIssue("Pre-launch process aborted during back-off wait due to service teardown.");
                            return false;
                        }

                        int slice = Math.Min(_waitChunkMs, delayMs - elapsed);

                        if (_cancellationSource != null)
                        {
                            // If the wait handle is signaled, cancellation is requested
                            if (_cancellationSource.Token.WaitHandle.WaitOne(slice))
                            {
                                LogIssue("Pre-launch process aborted during back-off wait due to service teardown.");
                                return false;
                            }
                        }
                        else
                        {
                            Thread.Sleep(slice);
                        }

                        // Pulse the SCM during the wait so the service is not killed by timeout.
                        _serviceHelper.RequestAdditionalTime(this, _scmAdditionalTimeMs, _logger);
                        elapsed += slice;
                    }
                }
            }

            // Final failure handling
            LogIssue("Pre-launch process failed after all retry attempts.");

            if (ignoreFailure)
            {
                _logger?.Warn("Ignoring pre-launch failure and continuing service start.");
                return true;
            }

            return false; // stop service start
        }

        /// <summary>
        /// Exposes the protected <see cref="OnStart(string[])"/> method for testing purposes.
        /// Starts the service using the <see cref="TestModeFlag"/> to bypass environment-specific 
        /// initializations like Win32 service handle reflection.
        /// </summary>
        public void StartForTest()
        {
            // Passing the TestModeFlag ensures that the service logic runs without 
            // attempting to hook into the Windows Service Control Manager.
            OnStart(new[] { TestModeFlag });
        }

        /// <summary>
        /// Initializes the stdout and stderr log writers based on the provided start options.
        /// </summary>
        /// <param name="options">The start options containing paths and rotation settings for stdout and stderr.</param>
        /// <remarks>
        /// - If <see cref="StartOptions.StdoutPath"/> is valid, a <see cref="Core.IO.RotatingStreamWriter"/> is created for stdout.
        /// - If <see cref="StartOptions.StderrPath"/> is provided:
        ///     - If it equals <see cref="StartOptions.StdoutPath"/> (case-insensitive), stderr shares the stdout writer.
        ///     - Otherwise, a separate <see cref="Core.IO.RotatingStreamWriter"/> is created for stderr.
        /// - If <see cref="StartOptions.StderrPath"/> is null, empty, or whitespace, no stderr writer is created.
        /// </remarks>
        private void HandleLogWriters(StartOptions options)
        {
            // Helper method to create a rotating writer if the path is valid.
            // Logs an error if the path is invalid or null/whitespace.
            IStreamWriter CreateWriter(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                if (!_pathValidator.IsValidPath(path))
                {
                    _logger?.Error($"Invalid log file path: {path}");
                    return null;
                }

                return _streamWriterFactory.Create(
                    path,
                    options.EnableSizeRotation,
                    options.RotationSizeInBytes,
                    options.EnableDateRotation,
                    options.DateRotationType,
                    options.MaxRotations,
                    options.UseLocalTimeForRotation
                    );
            }

            // Always create stdout writer if path is valid
            _stdoutWriter = CreateWriter(options.StdoutPath);

            // Only create stderr writer if a path is provided
            if (!string.IsNullOrWhiteSpace(options.StderrPath))
            {
                if (_stdoutWriter != null && !string.IsNullOrWhiteSpace(options.StdoutPath) &&
                    _pathValidator.IsValidPath(options.StderrPath) &&
                    _pathValidator.IsValidPath(options.StdoutPath))
                {
                    var canonStdErr = Helper.NormalizePath(options.StderrPath);
                    var canonStdOut = Helper.NormalizePath(options.StdoutPath);

                    // If stderr path equals stdout path (explicitly), use the same writer
                    if (string.Equals(canonStdErr, canonStdOut, StringComparison.OrdinalIgnoreCase))
                    {
                        _stderrWriter = _stdoutWriter;
                        return;
                    }
                }
                _stderrWriter = CreateWriter(options.StderrPath);
            }
        }

        /// <summary>
        /// Starts the monitored child process using the executable path, arguments, and working directory from the options.
        /// Also sets the process priority accordingly.
        /// </summary>
        /// <param name="options">The start options containing executable details and priority.</param>
        /// <param name="token">The cancellation token for the operation.</param>
        private void StartMonitoredProcess(StartOptions options, CancellationToken token)
        {
            StartProcess(options.ExecutablePath, options.ExecutableArgs, options.WorkingDirectory, options.EnvironmentVariables, token);
            SetProcessPriority(options.Priority);
        }

        /// <summary>
        /// Starts the child process.
        /// Redirects standard output and error streams, and sets up event handlers for output, error, and exit events.
        /// </summary>
        /// <param name="realExePath">The full path to the executable to run.</param>
        /// <param name="realArgs">The arguments to pass to the executable.</param>
        /// <param name="workingDir">The working directory for the process.</param>
        /// <param name="environmentVariables">Environment variables to pass to the process.</param>
        /// <param name="token">The cancellation token for the operation.</param>
        private void StartProcess(
            string realExePath,
            string realArgs,
            string workingDir,
            List<EnvironmentVariable> environmentVariables,
            CancellationToken token = default)
        {
            _ = AllocConsole(); // inherited
            _ = SetConsoleCtrlHandler(null, false); // inherited
            _ = SetConsoleOutputCP(CP_UTF8);

            var (expandedEnv, expandedArgs) = Helpers.ProcessHelper.ExpandAndAudit(environmentVariables, realArgs, _logger, "StartProcess");

            _realExePath = realExePath;
            _realArgs = expandedArgs;
            _workingDir = workingDir;
            _environmentVariables = environmentVariables ?? new List<EnvironmentVariable>();

            var enableConsoleUI = _options?.EnableConsoleUI == true;

            // Configure the process start info
            var psi = new ProcessStartInfo
            {
                FileName = _realExePath,
                Arguments = _realArgs,
                WorkingDirectory = _workingDir,
                UseShellExecute = false,
                // If UI support is enabled, we MUST show the window (in Session 0 it remains invisible)
                // and we MUST NOT redirect, otherwise the child gets a File Handle instead of a Console Handle.
                CreateNoWindow = !enableConsoleUI,
                RedirectStandardOutput = !enableConsoleUI,
                RedirectStandardError = !enableConsoleUI,
            };

            if (enableConsoleUI)
            {
                _logger?.Info("Console UI support enabled. Standard output is being rendered to an internal console; stdout/stderr redirection is bypassed.");
            }

            if (!enableConsoleUI)
            {
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
            }

            foreach (var envVar in expandedEnv)
            {
                psi.Environment[envVar.Key] = envVar.Value ?? string.Empty;
            }

            // Apply runtime-specific fixes
            ProcessLauncher.ApplyLanguageFixes(psi);

            _childProcess = _processFactory.Create(psi, _logger);

            // Enable events and attach output/error handlers
            _childProcess.EnableRaisingEvents = true;
            if (!enableConsoleUI)
            {
                _childProcess.OutputDataReceived += OnOutputDataReceived;
                _childProcess.ErrorDataReceived += OnErrorDataReceived;
            }
            _childProcess.Exited += OnProcessExited;

            // Start the process safely
            try
            {
                if (!_childProcess.Start())
                {
                    throw new InvalidOperationException("Process.Start returned false (no process resource started).");
                }

                _logger?.Info($"Started child process with PID: {_childProcess.Id}");
            }
            catch (Exception ex)
            {
                // This is now the SINGLE source of truth for start-up failure logging
                _logger?.Error($"Failed to start process '{_realExePath}': {ex.Message}");

                CleanupFailedProcess();

                _ = FreeConsole();

                // Re-throw so the caller (OnStart) stops the service and signals the SCM
                throw;
            }
            finally
            {
                _ = SetConsoleCtrlHandler(null, true);
            }

            // --- The code below this point ONLY executes if Start() was successful ---

            // Persist PID and PreviousStopTimeout
            PersistProcessState(_childProcess.Id, true);

            // Begin async reading of output and error streams
            if (!enableConsoleUI)
            {
                _childProcess.BeginOutputReadLine();
                _childProcess.BeginErrorReadLine();
            }

            // Fire and forget the post-launch script when process confirmed running
            var capturedProcess = _childProcess;
            var capturedToken = token;

            Task.Run(async () =>
            {
                try
                {
                    if (await capturedProcess.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(_options.StartTimeoutInSeconds), capturedToken))
                    {
                        StartPostLaunchProcess();
                    }
                }
                catch (OperationCanceledException)
                {
                    if (!string.IsNullOrWhiteSpace(_options?.PostLaunchExecutablePath))
                        _logger?.Info("Post-launch action cancelled because service is stopping.");
                }
                catch (Exception ex)
                {
                    _logger?.Error("Unexpected error in post-launch action.", ex);
                }
            }, capturedToken);
        }

        /// <summary>
        /// Cleans up the child process object if it fails to start, ensuring event handlers 
        /// are detached and the object is disposed before it can cause secondary exceptions.
        /// </summary>
        private void CleanupFailedProcess()
        {
            if (_childProcess == null) return;

            try
            {
                // Unsubscribe from events we attached before calling .Start()
                _childProcess.OutputDataReceived -= OnOutputDataReceived;
                _childProcess.ErrorDataReceived -= OnErrorDataReceived;
                _childProcess.Exited -= OnProcessExited;

                _childProcess.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Secondary error during failed process cleanup: {ex.Message}");
            }
            finally
            {
                _childProcess = null;
            }
        }

        /// <summary>
        /// Starts the configured post-launch executable, if defined.
        /// </summary>
        /// <remarks>
        /// This method launches an external program specified in the service options
        /// after the wrapped process has successfully started.  
        /// - If <see cref="_options"/> is <c>null</c> or no <c>PostLaunchExecutablePath</c> is set, the method does nothing.  
        /// - Environment variables in arguments are expanded before execution.  
        /// - The working directory defaults to the provided <c>PostLaunchWorkingDirectory</c>, 
        ///   or falls back to the directory of the executable if not set.  
        /// - The process is started in a fire-and-forget manner; no handle is kept or awaited.  
        /// </remarks>
        /// <exception cref="System.ComponentModel.Win32Exception">
        /// Thrown if the executable cannot be started (e.g., file not found, access denied).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no file name is specified in <c>ProcessStartInfo</c>.
        /// </exception>
        private void StartPostLaunchProcess()
        {
            RunFireAndForgetHook(
                "post-launch",
                exePath: _options?.PostLaunchExecutablePath,
                rawArgs: _options?.PostLaunchExecutableArgs,
                hookWorkingDir: _options?.PostLaunchWorkingDirectory,
                track: true);
        }

        /// <summary>
        /// Executes a secondary process as a "fire-and-forget" hook, optionally tracking its lifecycle 
        /// to ensure resources are cleaned up or managed appropriately.
        /// </summary>
        /// <param name="hookName">A descriptive name for the hook, used for logging and tracking purposes.</param>
        /// <param name="exePath">The file path to the executable to be launched. If null or empty, the hook execution is aborted.</param>
        /// <param name="rawArgs">The command-line arguments string to pass to the executable.</param>
        /// <param name="hookWorkingDir">
        /// The directory in which the process should start. If null or whitespace, defaults to the 
        /// primary service working directory specified in <c>_options</c>.
        /// </param>
        /// <param name="track">
        /// If <c>true</c>, the launched process is added to the internal <c>_trackedHooks</c> collection 
        /// for management; otherwise, the process resources are disposed immediately after launch.
        /// </param>
        /// <remarks>
        /// This method encapsulates process startup logic using <see cref="ProcessLauncher"/> and 
        /// applies service-wide environment variables. Any exceptions during process creation or 
        /// environment validation are caught and logged silently to prevent service interruption.
        /// </remarks>
        private void RunFireAndForgetHook(
            string hookName, string exePath, string rawArgs,
            string hookWorkingDir, bool track)
        {
            if (_options == null || string.IsNullOrWhiteSpace(exePath)) return;
            try
            {
                var workingDir = string.IsNullOrWhiteSpace(hookWorkingDir)
                    ? _options.WorkingDirectory : hookWorkingDir;
                var launchOptions = new ProcessLaunchOptions
                {
                    ExecutablePath = exePath,
                    Arguments = rawArgs ?? string.Empty,
                    WorkingDirectory = workingDir,
                    EnvironmentVariables = _options.EnvironmentVariables,
                    FireAndForget = true,
                    EnableConsoleUI = _options.EnableConsoleUI,
                };
                _logger?.Info($"Running {hookName} program: {launchOptions.ExecutablePath}");
                var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger);
                if (track && process?.UnderlyingProcess is Process p)
                    lock (_trackedHooks) _trackedHooks.Add(new Hook { OperationName = hookName, Process = p });
                else process?.Dispose();
            }
            catch (Exception ex) { _logger?.Error($"Failed to run {hookName} program.", ex); }
        }

        /// <summary>
        /// Executes the configured failure program if specified in the service options.
        /// This is invoked when the child process exits with a non-zero code while recovery
        /// is disabled, or after all recovery attempts have been exhausted
        /// (restartAttempts >= MaxRestartAttempts). It is NOT invoked when the main child
        /// process fails to start - that path stops the service without running the failure program.
        /// </summary>
        /// <remarks>
        /// The failure program path, arguments, and working directory are taken from
        /// the service options:
        /// - <c>FailureProgramPath</c>: the full path to the program to run.
        /// - <c>FailureProgramParameters</c>: the command-line arguments to pass.
        /// - <c>FailureProgramStartupDirectory</c>: the working directory for the program.
        /// 
        /// Exceptions thrown while attempting to start the failure program are caught
        /// and logged to avoid crashing the service.
        /// </remarks>
        private void RunFailureProgram()
        {
            RunFireAndForgetHook(
                 "failure program",
                 exePath: _options?.FailureProgramPath,
                 rawArgs: _options?.FailureProgramArgs,
                 hookWorkingDir: _options?.FailureProgramWorkingDirectory,
                 track: false);
        }

        /// <summary>
        /// Handles redirected standard output from the child process.
        /// Writes output lines to the rotating stdout writer.
        /// </summary>
        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            var writer = _stdoutWriter;        // snapshot
            if (writer == null) return;
            try { writer.WriteLine(e.Data); }
            catch (ObjectDisposedException) { /* shutting down */ }
            catch (Exception ex) { _logger?.Warn($"Failed to write stdout line: {ex.Message}"); }
        }

        /// <summary>
        /// Handles redirected standard error output from the child process.
        /// Writes error lines to the rotating stderr writer and logs errors.
        /// </summary>
        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            var writer = _stderrWriter;        // snapshot
            if (writer == null) return;
            try { writer.WriteLine(e.Data); }
            catch (ObjectDisposedException) { /* shutting down */ }
            catch (Exception ex) { _logger?.Warn($"Failed to write stderr line: {ex.Message}"); }
        }

        /// <summary>
        /// Inserts PID in database and updates PreviousStopTimeout, ActiveStdoutPath and ActiveStderrPath.
        /// </summary>
        /// <param name="pid">PID.</param>
        /// <param name="setPreviousStopTimeout">Indicates whether to set previous stop timeout.</param>
        private void PersistProcessState(int? pid, bool setPreviousStopTimeout)
        {
            if (string.IsNullOrWhiteSpace(_serviceName))
                return;

            try
            {
                // We need to fetch the full unencrypted service DTO in order to update the runtime state fields.
                // We cannot use decrypt:false here because encrypted fields (like Parameters and EnvironmentVariables)
                // are not marked to be ignored during update, and the update operation requires the full DTO to avoid overwriting existing values with wrong values.
                // This is a bit inefficient, but PersistProcessState only runs on service start/stop and process exit,
                // so the performance impact should be minimal in the grand scheme of things.
                var serviceDto = _serviceRepository.GetByName(_serviceName, decrypt: true);

                if (serviceDto != null)
                {
                    serviceDto.Pid = pid;
                    if (setPreviousStopTimeout)
                        serviceDto.PreviousStopTimeout = _options?.StopTimeoutInSeconds;

                    if (pid == null)
                    {
                        serviceDto.ActiveStdoutPath = null;
                        serviceDto.ActiveStderrPath = null;
                    }
                    else
                    {
                        serviceDto.ActiveStdoutPath = _options?.StdoutPath;
                        serviceDto.ActiveStderrPath = _options?.StderrPath;
                    }

                    _serviceRepository.Update(
                        serviceDto,
                        preserveExistingRuntimeState: false,
                        preserveExistingCredentials: true
                        );
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to persist PID {pid} for service '{_serviceName}'.", ex);
            }
        }

        /// <summary>
        /// Resets PID to null in database.
        /// </summary>
        private void ClearProcessState()
        {
            PersistProcessState(null, false);
        }

        /// <summary>
        /// Event handler for the child process's Exited event. 
        /// Evaluates the exit code and determines whether to perform a graceful shutdown 
        /// or trigger the recovery sequence based on failure thresholds.
        /// </summary>
        /// <param name="sender">The source of the event (the child process).</param>
        /// <param name="e">Event data containing no specific exit information.</param>
        private void OnProcessExited(object sender, EventArgs e)
        {
            if (_isTearingDown || _disposed || _isRebooting) return;

            _logger?.Info("Child process exit detected via event.");
            ClearProcessState();

            bool needsRecovery = false;
            bool shouldStop = false;
            int exitCode = -1;

            lock (_healthCheckLock)
            {
                try
                {
                    exitCode = _childProcess?.ExitCode ?? -1;
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"Failed to get exit code: {ex.Message}");
                }

                if (exitCode == 0)
                {
                    if (_options?.RecoveryOnCleanExit == true)
                    {
                        _logger?.Info("Child process exited successfully (Code 0). RecoveryOnCleanExit is ENABLED. Checking recovery...");
                        needsRecovery = RegisterFailureAndCheckRecovery();
                    }
                    else
                    {
                        _logger?.Info("Child process exited successfully (Code 0). Service will stop.");
                        shouldStop = true;
                    }
                }
                else if (_recoveryActionEnabled)
                {
                    // Unified recovery check
                    needsRecovery = RegisterFailureAndCheckRecovery();
                }
                else
                {
                    _logger?.Error($"Process exited with code {exitCode} and recovery is disabled.");
                    shouldStop = true;
                }
            }

            // Actions outside the lock
            if (shouldStop)
            {
                if (exitCode != 0) RunFailureProgram();
                Stop();
            }
            else if (needsRecovery)
            {
                // Calculate delay: Heartbeat interval minus a 5s buffer, minimum 5s.
                var delayMs = Math.Max(ClampTimeout(_options.HeartbeatInterval) - AppConfig.RecoverySchedulingDelayMs, AppConfig.RecoverySchedulingDelayMs);
                _logger?.Info($"[OnProcessExited] Failure threshold reached. Scheduling recovery in {delayMs / 1000}s...");

                // Fire-and-forget the recovery task safely
                _ = ScheduleRecoveryAsync(delayMs);
            }

            // Helper local function to handle the asynchronous wait and safety checks
            async Task ScheduleRecoveryAsync(int delay)
            {
                bool recoveryTriggered = false;
                try
                {
                    // Use the cancellation token to allow the delay to be aborted during service shutdown
                    await Task.Delay(delay, _cancellationSource?.Token ?? CancellationToken.None);

                    // Re-check state after the delay to ensure we aren't mid-shutdown
                    if (!_isTearingDown && !_disposed)
                    {
                        InitiateRecovery();
                        recoveryTriggered = true; // Successfully handed off to the recovery orchestrator
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.Info("Scheduled recovery cancelled due to service shutdown.");
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Unexpected error during scheduled recovery: {ex.Message}");
                }
                finally
                {
                    // SAFETY RESET: If we reach this line, InitiateRecoveryAsync was skipped
                    // or the delay threw an exception. We MUST clear the gatekeeper flag.
                    // Only reset if we didn't hand off to the orchestrator.
                    // If we hand off, InitiateRecoveryAsync() is responsible for managing the state.
                    if (!recoveryTriggered)
                    {
                        lock (_healthCheckLock)
                        {
                            _isRecovering = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Atomically registers a health check failure and evaluates if recovery should be initiated.
        /// Assumes the caller has already acquired the _healthCheckSemaphore.
        /// </summary>
        private bool RegisterFailureAndCheckRecovery()
        {
            // Gatekeeper: If we are already recovering, do not increment or log more failures
            if (_isRecovering) return false;

            _failedChecks++;

            if (_failedChecks >= _maxFailedChecks)
            {
                // Set this immediately to block other threads/timers from incrementing
                // while we are waiting for the recovery task to execute.
                _isRecovering = true;

                // Log inside the threshold block to guarantee it prints exactly once per recovery cycle
                _logger?.Warn($"Health check failed ({_failedChecks}/{_maxFailedChecks}). Initiating recovery.");
                return true;
            }

            _logger?.Warn($"Health check failed ({_failedChecks}/{_maxFailedChecks}).");
            return false;
        }

        /// <summary>
        /// Sets the priority class of the child process.
        /// Logs info on success or a warning if it fails.
        /// </summary>
        /// <param name="priority">The process priority to set.</param>
        public void SetProcessPriority(ProcessPriorityClass priority)
        {
            if (_childProcess == null)
            {
                _logger?.Warn("SetProcessPriority called before child process was started; ignoring.");
                return;
            }

            try
            {
                _childProcess.PriorityClass = priority;
                _logger?.Info($"Set process priority to {_childProcess.PriorityClass}.");
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Failed to set priority: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up health monitoring for the child process using a timer.
        /// Starts the timer if heartbeat interval, max failed checks, and recovery action are valid.
        /// </summary>
        /// <param name="options">The start options containing health check configuration.</param>
        private void SetupHealthMonitoring(StartOptions options)
        {
            if (_recoveryActionEnabled)
            {
                _healthCheckTimer = _timerFactory.Create(options.HeartbeatInterval * 1000.0);
                _healthCheckTimer.Elapsed += CheckHealth;
                _healthCheckTimer.AutoReset = true;
                _healthCheckTimer.Start();

                _logger?.Info("Health monitoring started.");
            }
        }

        /// <summary>
        /// Orchestrates the recovery sequence when the child process is deemed unhealthy.
        /// Handles restart quota management, persistence of attempts, and execution of the recovery strategy.
        /// </summary>
        /// <remarks>
        /// This method uses a "Gatekeeper" pattern via the <c>_isRecovering</c> flag to ensure that 
        /// only one recovery action is executed at a time, even if multiple health checks fail 
        /// simultaneously. The flag is reset in a <c>finally</c> block to guarantee that 
        /// monitoring can resume regardless of whether the recovery succeeded or threw an exception.
        /// </remarks>
        private void InitiateRecovery()
        {
            // Tracks if we actually succeeded in executing a terminal action.
            bool recoveryActionSucceeded = false;
            // We use this flag to decide if the gate should reopen.
            // For Reboot/RestartService, the gate should stay closed forever for this instance.
            bool shouldReopenGate = true;

            try
            {
                bool shouldStop = false;
                int currentAttempts = 0;

                // Guard: prevent concurrent recovery attempts
                if (_isTearingDown || _disposed || _isRebooting) return;

                // _maxRestartAttempts == 0 means unlimited restart attempts
                if (_maxRestartAttempts > 0)
                {
                    currentAttempts = EnsureRestartAttemptsFile();

                    if (currentAttempts >= _maxRestartAttempts)
                    {
                        _logger?.Error($"Maximum restart attempts reached ({_maxRestartAttempts}). Stopping service.");
                        SaveRestartAttempts(0); // Reset for next manual start
                        shouldStop = true;
                    }
                    else
                    {
                        currentAttempts++;
                        SaveRestartAttempts(currentAttempts);
                    }
                }

                // Set the recovery state (MANDATORY for both limited and unlimited)
                if (!shouldStop)
                {
                    _failedChecks = 0;
                    _isRecovering = true; // Set flag to block other health checks: GATE CLOSED

                    // If the action is terminal for this process instance, don't reopen the gate.
                    if (_recoveryAction == RecoveryAction.RestartComputer ||
                        _recoveryAction == RecoveryAction.RestartService)
                    {
                        shouldReopenGate = false;
                    }
                }


                if (shouldStop)
                {
                    RunFailureProgram();
                    Stop();
                    return;
                }

                try
                {
                    // Execute the action and mark success only if no exception occurred
                    ExecuteRecoveryAction(currentAttempts);
                    recoveryActionSucceeded = true;
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Critical error during recovery execution: {ex.Message}");
                    // recoveryActionSucceeded remains false
                }
            }
            finally
            {
                // REOPEN GATE IF:
                // 1. The action wasn't meant to be terminal (shouldReopenGate is true) OR
                // 2. The terminal action failed (recoveryActionSucceeded is false)
                if (shouldReopenGate || !recoveryActionSucceeded)
                {
                    _isRecovering = false;
                }
            }
        }

        /// <summary>
        /// Dispatches the configured <see cref="RecoveryAction"/> by delegating to
        /// the appropriate <see cref="IServiceHelper"/> method (restart service,
        /// restart child process, or reboot computer).
        /// </summary>
        /// <remarks>
        /// This method is invoked exclusively by <see cref="InitiateRecovery"/>
        /// after the gatekeeper flag (<see cref="_isRecovering"/>) has been set and
        /// restart-attempt persistence has been recorded. It assumes the quota check
        /// has already passed and does not enforce <see cref="_maxRestartAttempts"/>.
        /// </remarks>
        /// <param name="attemptCount">The current restart attempt number, logged for diagnostics.</param>
        private void ExecuteRecoveryAction(int attemptCount)
        {
            // LOGGING: Omit the attempt counter in unlimited mode to prevent displaying a misleading '0'.
            string attemptStatus = _maxRestartAttempts > 0
                ? $"({attemptCount}/{_maxRestartAttempts})"
                : "(unlimited)";

            _logger?.Warn($"Performing recovery action '{_recoveryAction}' {attemptStatus}.");

            // Prune exited hooks
            lock (_trackedHooks)
            {
                for (int i = _trackedHooks.Count - 1; i >= 0; i--)
                {
                    var h = _trackedHooks[i];
                    try
                    {
                        if (h.Process == null || h.Process.HasExited)
                        {
                            h.Dispose();
                            _trackedHooks.RemoveAt(i);
                        }
                    }
                    catch { /* leave for teardown */ }
                }
            }

            switch (_recoveryAction)
            {
                case RecoveryAction.RestartService:
                    _serviceHelper.RestartService(_logger, _serviceName);
                    break;

                case RecoveryAction.RestartProcess:
                    _serviceHelper.RestartProcess(
                        _childProcess,
                        StartProcess,
                        _realExePath,
                        _realArgs,
                        _workingDir,
                        _environmentVariables,
                        _logger,
                        ClampTimeout(_options?.StopTimeoutInSeconds ?? AppConfig.DefaultStopTimeout),
                        _cancellationSource?.Token ?? CancellationToken.None
                    );
                    break;

                case RecoveryAction.RestartComputer:
                    try
                    {
                        _isRebooting = true;
                        _serviceHelper.RestartComputer(_logger);
                    }
                    catch
                    {
                        _isRebooting = false; // reboot didn't happen; allow recovery/teardown to continue
                        throw;
                    }
                    break;

                default:
                    _logger?.Info($"Recovery action '{_recoveryAction}' requires no specific logic.");
                    break;
            }
        }

        /// <summary>
        /// Periodically evaluates the health of the child process.
        /// Increments failure counters if the process is missing or crashed, and triggers recovery logic 
        /// when the maximum failure threshold is reached.
        /// </summary>
        /// <param name="sender">The timer instance that triggered the health check.</param>
        /// <param name="e">Event data containing the time the check was triggered.</param>
        /// <remarks>
        /// This method implements a thread-safe "gatekeeper" pattern using <see cref="_isRecovering"/>.
        /// If a recovery is already in progress (triggered by this timer or a process exit event), 
        /// the check exits immediately to prevent duplicate logs and redundant recovery attempts.
        /// </remarks>
        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
            if (_isTearingDown || _disposed || _isRebooting || _isRecovering) return;

            bool needsRecovery = false;
            bool shouldStop = false;
            bool performStabilityCheck = false;

            lock (_healthCheckLock)
            {
                // If we are already recovering, we don't want to increment restartAttempts again
                if (_isTearingDown || _disposed || _isRecovering) return;

                // Capture _childProcess into a local variable to ensure atomicity.
                var process = _childProcess;

                bool isFailed = process == null || process.HasExited;
                if (isFailed)
                {
                    int? exitCode = null;
                    try { exitCode = process?.ExitCode; } catch (Exception ex) { _logger?.Warn($"Health check could not read ExitCode (treating as failure): {ex.Message}"); }

                    if (exitCode == 0)
                    {
                        if (_options.RecoveryOnCleanExit == true)
                        {
                            _logger?.Info("Health check detected clean exit (Code 0), but RecoveryOnCleanExit is ENABLED. Checking recovery...");
                            needsRecovery = RegisterFailureAndCheckRecovery();
                        }
                        else
                        {
                            _logger?.Info("Health check detected child process exited successfully (Code 0). Service will stop.");
                            shouldStop = true;
                        }
                    }
                    else
                    {
                        // Unified recovery check for non-zero exit codes or missing processes
                        needsRecovery = RegisterFailureAndCheckRecovery();
                    }
                }
                else
                {
                    // PROCESS IS HEALTHY
                    if (_failedChecks > 0)
                    {
                        _logger?.Info("Child process is healthy again. Resetting transient failure count.");

                        // Always reset memory count immediately so we don't trigger recovery again unnecessarily
                        _failedChecks = 0;
                    }

                    performStabilityCheck = true;
                }
            }

            if (performStabilityCheck)
            {
                // STABILITY CHECK (Disk I/O)
                // This is safely outside the health lock, preventing it from stalling OnProcessExited.
                ConditionalResetRestartAttempts(_options);
            }

            if (shouldStop) Stop();

            // InitiateRecovery will now find _isRecovering is already true.
            // Ensure InitiateRecovery's internal guard allows it to run if 
            // it's the one performing the recovery!
            if (needsRecovery) InitiateRecovery();
        }

        /// <summary>
        /// Called when the service receives a Stop command from the Service Control Manager (SCM).
        /// Triggers the standardized teardown sequence.
        /// </summary>
        protected override void OnStop()
        {
            ExecuteTeardown(TeardownReason.Stop);

            // Flush logs right before returning control to SCM
            FlushAndShutdownLogger();

            base.OnStop();
        }

        /// <summary>
        /// Handles custom control commands sent to the service by the Service Control Manager (SCM).
        /// Specifically intercepts the Pre-Shutdown signal to begin an orchestrated teardown.
        /// </summary>
        /// <param name="command">The control code sent by the SCM.</param>
        protected override void OnCustomCommand(int command)
        {
            if (command == SERVICE_CONTROL_PRESHUTDOWN)
            {
                if (_isRebooting)
                {
                    _logger?.Info("Pre-Shutdown bypassed: System reboot initiated by recovery logic.");
                    // Signal stopped immediately so the OS doesn't wait for us
                    UpdateServiceStatus(SERVICE_STOPPED, 0);
                    FlushAndShutdownLogger();
                    return;
                }

                _logger?.Info("Pre-Shutdown received. Starting orchestrated teardown...");

                if (_serviceHandle == IntPtr.Zero)
                {
                    _logger?.Error("Service handle is null! SCM notification impossible. Falling back to synchronous teardown.");

                    // Log the completion intention right before the logger is destroyed
                    _logger?.Info("Pre-Shutdown fallback path entered. Initiating synchronous teardown before Environment.Exit.");
                    try
                    {
                        ExecuteTeardown(TeardownReason.PreShutdown);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Fallback teardown failed: {ex.Message}");
                    }
                    finally
                    {
                        var code = Environment.ExitCode != 0 ? Environment.ExitCode : 1;
                        FlushAndShutdownLogger();
                        Environment.Exit(code);
                    }
                    return;
                }

                // 1. Immediately tell SCM we are transitioning to a stop state and need a 30s window.
                // This moves the service into the STOP_PENDING state in the eyes of the OS.
                UpdateServiceStatus(SERVICE_STOP_PENDING, AppConfig.PreShutdownWaitHintMs);

                Task<bool> stopTask = Task.Run(() => ExecuteTeardown(TeardownReason.PreShutdown));

                // 2. Wait in pulses. 
                // We increment the checkpoint each pulse to prove to the SCM that we haven't hung.
                // This loop is guaranteed to terminate because the underlying teardown logic 
                // (SafeKillProcess) enforces an absolute, stopwatch-backed timeout limit.
                bool teardownSucceeded = false;
                try
                {
                    while (!stopTask.Wait(AppConfig.PreShutdownPulseIntervalMs))
                    {
                        _checkPoint++;
                        UpdateServiceStatus(SERVICE_STOP_PENDING, AppConfig.PreShutdownWaitHintMs);
                    }
                    teardownSucceeded = stopTask.Status == TaskStatus.RanToCompletion && stopTask.Result;
                }
                catch (AggregateException ex)
                {
                    _logger?.Error($"Teardown task faulted during pre-shutdown wait: {ex.Flatten().InnerException?.Message}", ex);
                }

                // 3. Final Signal: Inform the SCM that the service has successfully reached the STOPPED state.
                if (teardownSucceeded)
                {
                    _logger?.Info("Pre-Shutdown handling complete. Setting SERVICE_STOPPED.");
                }
                else
                {
                    _logger?.Error("Pre-Shutdown teardown reported failure; signaling SERVICE_STOPPED with non-zero exit code so SCM records the failure.");
                    if (ExitCode == 0) ExitCode = AppConfig.ServiceSpecificErrorCode; // ERROR_SERVICE_SPECIFIC_ERROR
                }
                UpdateServiceStatus(SERVICE_STOPPED, 0);

                // 4. SHUTDOWN LOGGER LAST
                FlushAndShutdownLogger();

                return;
            }

            base.OnCustomCommand(command);
        }

        /// <summary>
        /// Safely flushes and shuts down the loggers with a strict timeout 
        /// to prevent OS-level RPC hangs during system shutdown.
        /// </summary>
        private void FlushAndShutdownLogger()
        {
            // Snapshot + clear synchronously so concurrent callers can no longer reach the old logger.
            IServyLogger toDispose = Interlocked.Exchange(ref _logger, null);

            try
            {
                var flushTask = Task.Run(() =>
                {
                    try { Logger.Shutdown(); } catch { /* fail-silent */ }
                    try { toDispose?.Dispose(); } catch { /* fail-silent */ }
                });

                if (!flushTask.Wait(AppConfig.LoggerFlushTimeoutMs))
                {
                    // Observe orphan so it can't surface an unobserved exception later.
                    _ = flushTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch
            {
                // Fail-silent (per existing contract)
            }
        }

        /// <summary>
        /// Updates the service status by calling the Win32 SetServiceStatus API.
        /// This informs the SCM of the service's current state and expected wait times.
        /// </summary>
        /// <param name="state">The current state of the service (e.g., <c>SERVICE_STOP_PENDING</c> or <c>SERVICE_STOPPED</c>).</param>
        /// <param name="waitHint">The estimated time for the pending operation in milliseconds.</param>
        private void UpdateServiceStatus(int state, int waitHint)
        {
            try
            {
                if (_serviceHandle == IntPtr.Zero)
                {
                    _logger?.Error("Service handle is null, cannot update status");
                    return;
                }

                int win32ExitCode = 0;
                int specificExitCode = 0;
                if (state == SERVICE_STOPPED && ExitCode != 0)
                {
                    // ERROR_SERVICE_SPECIFIC_ERROR tells SCM to read dwServiceSpecificExitCode
                    win32ExitCode = AppConfig.ServiceSpecificErrorCode; // ERROR_SERVICE_SPECIFIC_ERROR
                    specificExitCode = ExitCode;
                }

                // Construct the Win32 status structure.
                SERVICE_STATUS status = new SERVICE_STATUS
                {
                    dwServiceType = SERVICE_WIN32_OWN_PROCESS,
                    dwCurrentState = state,
                    // If stopped, we accept nothing. Otherwise, we maintain our acceptance of Stop and Preshutdown.
                    dwControlsAccepted = state == SERVICE_STOPPED
                        ? 0
                        : (SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_PRESHUTDOWN),
                    dwWin32ExitCode = win32ExitCode,
                    dwServiceSpecificExitCode = specificExitCode,
                    dwCheckPoint = (int)_checkPoint,
                    dwWaitHint = waitHint
                };

                // Invoke the P/Invoke method to update the SCM
                if (!SetServiceStatus(_serviceHandle, ref status))
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger?.Error($"SetServiceStatus failed with Win32 error code: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Exception in UpdateServiceStatus: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the system is shutting down. 
        /// Mimics the Stop command to ensure child processes and hooks are cleaned up before the OS terminates the process.
        /// </summary>
        /// <remarks>
        /// This requires <see cref="ServiceBase.CanShutdown"/> to be set to <see langword="true"/> in the service constructor.
        /// </remarks>
        protected override void OnShutdown()
        {
            if (_isRebooting)
            {
                _logger?.Info("Shutdown bypassed: System reboot initiated by recovery logic.");
                FlushAndShutdownLogger();
                return;
            }

            ExecuteTeardown(TeardownReason.Shutdown);

            // Save final logs before the OS kills the process
            FlushAndShutdownLogger();

            base.OnShutdown();
        }

        /// <summary>
        /// Orchestrates the shared teardown logic for both service stops and system shutdowns.
        /// Handles cancellation, event invocation, and resource cleanup.
        /// </summary>
        /// <param name="reason">The context of the teardown (e.g., "Stop" or "Shutdown") used for logging purposes.</param>
        private bool ExecuteTeardown(TeardownReason reason)
        {
            // Use a local flag to track if we actually performed the work
            bool performedCleanup = false;

            lock (_teardownLock)
            {
                if (_disposed || _isTearingDown) return true;

                _isTearingDown = true;

                // Stop health check timer IMMEDIATELY to prevent interference
                try
                {
                    if (_healthCheckTimer != null)
                    {
                        _healthCheckTimer.Elapsed -= CheckHealth;
                        _healthCheckTimer.Stop();
                        _healthCheckTimer.Dispose();
                        _healthCheckTimer = null;
                    }
                    _logger?.Info("Health check timer disabled during teardown");
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"Error stopping health check timer: {ex.Message}");
                }

                try
                {
                    _logger?.Info($"Executing teardown for reason: {reason}");
                    _cancellationSource?.Cancel();
                    OnStoppedForTest?.Invoke();

                    Cleanup();

                    performedCleanup = true;
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Teardown error during {reason}.", ex);

                    // Reset the tearing down flag so the service can attempt recovery 
                    // if the child process exits or the SCM attempts another stop.
                    _isTearingDown = false;
                }
                finally
                {
                    // Only dispose of core resources and set _disposed = true 
                    // if the teardown actually succeeded.
                    if (performedCleanup)
                    {
                        _disposed = true;

                        try { _cancellationSource?.Dispose(); } catch (Exception ex) { _logger?.Warn($"Disposing _cancellationSource failed: {ex.Message}"); }
                        _cancellationSource = null;

                        try { _secureData?.Dispose(); } catch (Exception ex) { _logger?.Warn($"Disposing _secureData failed: {ex.Message}"); }
                        try { _protectedKeyProvider?.Dispose(); } catch (Exception ex) { _logger?.Warn($"Disposing _protectedKeyProvider failed: {ex.Message}"); }
                        try { _dbContext?.Dispose(); } catch (Exception ex) { _logger?.Warn($"Disposing _dbContext failed: {ex.Message}"); }
                    }
                }
            }

            return performedCleanup;
        }

        /// <summary>
        /// Orchestrates the full cleanup sequence: unhooking events, running the pre-stop hook,
        /// terminating the child process tree, and running the post-stop hook.
        /// </summary>
        private void Cleanup()
        {
            // reset PID
            ClearProcessState();

            // 0. Robustness Guard: If options are null, we haven't initialized hooks or process paths yet.
            if (_options == null)
            {
                _logger?.Debug("Cleanup called before service options were initialized. Bypassing hook execution.");
                return;
            }

            try
            {
                // 1. Unhook Exited event early so we don't trigger "unexpected exit" logic
                if (_childProcess != null)
                    _childProcess.Exited -= OnProcessExited;

                // 2. Pre-Stop Hook
                // Even if this fails, we usually want to continue killing the main process
                var preStopSuccess = StartPreStopProcess(_options);

                if (!preStopSuccess && _options.PreStopLogAsError)
                {
                    _logger?.Error("Pre-stop failed. Manual intervention may be required, but continuing cleanup to avoid orphans.");
                }

                // 3. Main Kill Sequence (with SCM Heartbeats)
                if (_childProcess != null)
                {
                    SafeKillProcess(_childProcess, ClampTimeout(_options.StopTimeoutInSeconds));

                    // Only cancel if redirection was active AND the stream is actually open
                    if (_childProcess.StartInfo.RedirectStandardOutput)
                    {
                        try { _childProcess.CancelOutputRead(); } catch { /* Ignore if already stopped */ }
                        _childProcess.OutputDataReceived -= OnOutputDataReceived;
                    }
                    if (_childProcess.StartInfo.RedirectStandardError)
                    {
                        try { _childProcess.CancelErrorRead(); } catch { /* Ignore if already stopped */ }
                        _childProcess.ErrorDataReceived -= OnErrorDataReceived;
                    }
                }

                // 4. Final Cleanup of all tracked processes
                lock (_trackedHooks)
                {
                    foreach (var hook in _trackedHooks)
                    {
                        if (hook.Process == null) continue;

                        try
                        {
                            // Always check HasExited first
                            if (!hook.Process.HasExited)
                            {
                                var opName = string.IsNullOrWhiteSpace(hook.OperationName) ? "unnamed" : hook.OperationName;
                                int pid = 0;

                                // Process.Id can throw if the process is in a weird state
                                try { pid = hook.Process.Id; } catch { /* Ignored */ }

                                _logger?.Info($"Cleaning up orphaned {opName} hook process tree (PID: {pid}).");

                                // 1. Issue the kill request
                                try
                                {
                                    Helpers.ProcessHelper.KillProcessTree(hook.Process);
                                }
                                catch (Exception killEx)
                                {
                                    _logger?.Warn($"Failed to send Kill signal to {opName} hook: {killEx.Message}");
                                }

                                // 2. Bounded wait with SCM heartbeat pulses
                                int timeoutMs = AppConfig.HookCleanupTimeoutMs;
                                int pulseIntervalMs = AppConfig.SafeKillProcessPulseIntervalMs;
                                int elapsedMs = 0;

                                while (!hook.Process.HasExited && elapsedMs < timeoutMs)
                                {
                                    int waitTime = Math.Min(pulseIntervalMs, timeoutMs - elapsedMs);

                                    // Request additional time to prevent SCM from terminating the service during a slow kernel teardown
                                    _serviceHelper.RequestAdditionalTime(this, _scmAdditionalTimeMs, null);

                                    if (hook.Process.WaitForExit(waitTime))
                                    {
                                        break;
                                    }

                                    elapsedMs += waitTime;
                                }

                                // 3. Evaluate outcome
                                if (!hook.Process.HasExited)
                                {
                                    _logger?.Warn($"Tracked hook '{opName}' (PID: {pid}) did not exit within the {timeoutMs}ms budget. Proceeding with teardown to avoid SCM hang.");
                                }
                                else
                                {
                                    _logger?.Info($"Tracked hook '{opName}' cleaned up successfully.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log locally for debug, but don't let it stop the loop
                            _logger?.Error("Cleanup of tracked hook failed.", ex);
                        }
                    }

                    // Cleanup and dispose tracked hooks
                    CleanupTrackedHooks();
                }

                // 5. Post-Stop Hook
                StartPostStopProcess();

                try
                {
                    // Dispose output writers for stdout and stderr streams
                    var stdout = Interlocked.Exchange(ref _stdoutWriter, null);
                    var stderr = Interlocked.Exchange(ref _stderrWriter, null);

                    try { stdout?.Dispose(); }
                    catch (Exception ex) { _logger?.Warn($"Failed to dispose stdout writer: {ex.Message}"); }

                    if (!ReferenceEquals(stderr, stdout))
                    {
                        try { stderr?.Dispose(); }
                        catch (Exception ex) { _logger?.Warn($"Failed to dispose stderr writer: {ex.Message}"); }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"Failed to dispose output writers: {ex.Message}");
                }

            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to kill child process: {ex.Message}");
            }
            finally
            {
                if (_childProcess != null)
                {
                    _childProcess.Dispose();
                    _childProcess = null;
                }
            }

        }

        /// <summary>
        /// Executes an optional pre-stop executable. 
        /// Supports fire-and-forget or synchronous wait with SCM heartbeat pulses.
        /// </summary>
        /// <param name="options">The service configuration options.</param>
        /// <returns><see langword="true"/> if the process succeeded or failures are ignored; otherwise <see langword="false"/>.</returns>
        private bool StartPreStopProcess(StartOptions options)
        {
            if (_options == null || string.IsNullOrWhiteSpace(options.PreStopExecutablePath))
            {
                _logger?.Info("No pre-stop executable configured. Skipping.");
                return true;
            }

            _logger?.Info("Starting pre-stop process...");
            bool logAsError = options.PreStopLogAsError;

            // Helper to keep the catch block and failure logic clean
            void LogIssue(string message, Exception ex = null)
            {
                if (!logAsError)
                    _logger?.Warn(message);
                else if (ex != null)
                    _logger?.Error(message, ex);
                else
                    _logger?.Error(message);
            }

            try
            {
                // 1. Prepare Environment and Arguments
                var args = options.PreStopExecutableArgs ?? string.Empty;

                var workingDir = string.IsNullOrWhiteSpace(_options.PreStopWorkingDirectory)
                    ? _options.WorkingDirectory
                    : _options.PreStopWorkingDirectory;

                // 2. Configure Launch Options
                var effectiveTimeoutMs = ClampTimeout(options.PreStopTimeoutInSeconds);

                var launchOptions = new ProcessLaunchOptions
                {
                    ExecutablePath = options.PreStopExecutablePath,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    EnvironmentVariables = options.EnvironmentVariables,
                    FireAndForget = (effectiveTimeoutMs == 0),
                    TimeoutMs = effectiveTimeoutMs,
                    WaitChunkMs = _waitChunkMs,
                    ScmAdditionalTimeMs = _scmAdditionalTimeMs,
                    OnScmHeartbeat = time => _serviceHelper.RequestAdditionalTime(this, time, _logger),
                    LogErrorAsWarning = !logAsError,
                    EnableConsoleUI = options.EnableConsoleUI,
                };

                // 3. Launch and Evaluate
                using (var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger))
                {
                    if (launchOptions.FireAndForget)
                    {
                        _logger?.Info("Pre-stop configured as fire-and-forget. Continuing service stop immediately.");
                        return true;
                    }

                    if (process.ExitCode == 0)
                    {
                        _logger?.Info("Pre-stop process completed successfully.");
                        return true;
                    }

                    LogIssue($"Pre-stop process '{launchOptions.ExecutablePath}' exited with code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                LogIssue("Pre-stop process failed.", ex);
            }

            // 4. Final Policy Handling
            if (!logAsError)
            {
                _logger?.Warn("Ignoring pre-stop failure and continuing service stop.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Stops the specified process and its descendant processes in a service-safe manner.
        /// Attempts a graceful shutdown of the main process first (Ctrl+C / close window),
        /// then initiates cleanup of any remaining descendant processes.
        /// The stop sequence runs on a background thread while periodically reporting
        /// wait hints to the Service Control Manager.
        /// </summary>
        /// <param name="process">The process to stop.</param>
        /// <param name="timeoutMs">
        /// The maximum time, in milliseconds, to allow each stop operation to complete.
        /// The same timeout is applied to the main process and descendant cleanup.
        /// </param>
        private void SafeKillProcess(IProcessWrapper process, int timeoutMs)
        {
            // Intentionally do NOT skip when process.HasExited: descendants may still be
            // alive even after the parent exits, so we always run the cleanup path.
            if (process == null) return;

            try
            {
                _logger?.Info($"Starting stop sequence for {process.Format()} (Timeout: {timeoutMs}ms)");

                // 1. Run the blocking process.Stop call on a background thread
                // This ensures we call it exactly once with the full timeout.

                // Capture lineage info while the parent is definitely alive
                var parentPid = 0;
                var parentStartTime = DateTime.MinValue;
                try
                {
                    parentPid = process.Id;
                    parentStartTime = process.StartTime;
                }
                catch (Exception ex)
                {
                    /* Process already dead, can't get children anyway */
                    _logger?.Warn($"SafeKillProcess error while getting process PID and StartTime: {ex.Message}");
                }

                // --- PRE-STOP SCAN ---
                int childCount = 0;
                try
                {
                    // Use GetAllDescendants to scan the entire deep tree instead of just Level 1
                    var initialChildren = ProcessExtensions.GetAllDescendants(parentPid, parentStartTime);
                    childCount = initialChildren.Count;

                    if (childCount > 0)
                    {
                        _logger?.Info($"Pre-stop scan found {childCount} active descendants for PID {parentPid}:");
                        foreach (var child in initialChildren)
                        {
                            _logger?.Info($"  - {child.Format()}");
                            child.Dispose(); // Dispose immediately after logging to avoid handle leaks
                        }
                    }
                    else
                    {
                        _logger?.Info($"Pre-stop scan found no active descendants for PID {parentPid}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"Could not complete pre-stop scan: {ex.Message}");
                }
                // ---------------------

                // Total timeout = time for main process to stop + time for descendants to stop + safety buffer
                var totalTimeoutMs = 2L * timeoutMs + childCount * ((long)timeoutMs + AppConfig.DefaultDescendantPostKillWaitMs) + AppConfig.SafeKillProcessSafetyBufferMs;

                Task<bool?> stopTask = Task.Run(() =>
                {
                    // Send Ctrl+C to the root wrapper first. 
                    // This natively broadcasts the signal to all console-sharing children (like Python).
                    _logger?.Info("Signaling main wrapper process (broadcasts to console group)...");
                    bool? mainExitedGracefully;
                    if (process.HasExited)
                    {
                        mainExitedGracefully = null; // pre-exited
                    }
                    else
                    {
                        mainExitedGracefully = process.Stop(timeoutMs);
                    }

                    // Clean up descendants afterward. 
                    // Thanks to native P/Invoke, we can still trace children even if the parent exited instantly.
                    _logger?.Info($"Initiating descendant cleanup for PID {parentPid}...");
                    process.StopDescendants(parentPid, parentStartTime, timeoutMs);

                    return mainExitedGracefully;
                });

                // 2. Wait for the task to complete in SafeKillProcessPulseIntervalMs pulses
                // This prevents ContextSwitchDeadlock and keeps the SCM happy.

                var maxWaitTime = TimeSpan.FromMilliseconds(totalTimeoutMs);
                var sw = Stopwatch.StartNew();

                bool waitCompleted;
                try
                {
                    while (true)
                    {
                        try { waitCompleted = stopTask.Wait(AppConfig.SafeKillProcessPulseIntervalMs); }
                        catch (AggregateException) { waitCompleted = true; break; } // task finished with fault/cancel
                        if (waitCompleted) break;
                        if (sw.Elapsed > maxWaitTime)
                        {
                            _logger?.Error("Stop operation exceeded safety limit. The process tree may be hung at the kernel level.");
                            break; // Exit the loop and let the service finish/crash
                        }

                        // Pulse the SCM wait-hint (_scmAdditionalTimeMs) once per SafeKillProcessPulseIntervalMs tick.
                        _serviceHelper.RequestAdditionalTime(this, _scmAdditionalTimeMs, null);
                    }
                }
                finally { sw.Stop(); }

                // 3. SAFE RESULT RETRIEVAL
                // We only access .Result if the task truly succeeded.
                bool outcomeHandled = false;
                bool? res = null;

                if (stopTask.Status == TaskStatus.RanToCompletion)
                {
                    res = stopTask.Result;
                }
                else if (stopTask.IsFaulted)
                {
                    // Flatten the AggregateException and get the root cause
                    // (e.g., the actual Win32Exception or InvalidOperationException)
                    var innerEx = stopTask.Exception?.Flatten().InnerException;
                    _logger?.Error($"SafeKillProcess background task failed: {innerEx?.Message}", innerEx);
                    outcomeHandled = true;
                }
                else if (stopTask.IsCanceled)
                {
                    _logger?.Warn("SafeKillProcess was canceled before the stop sequence could finish.");
                    outcomeHandled = true;
                }
                else
                {
                    // Timeout: task is still running. Don't fall through to HandleStopResult,
                    // which would misreport "had already exited".
                    _logger?.Warn($"Child process '{process.Format()}' stop sequence did not complete within the safety budget; the kill task is still running and will be abandoned.");
                    outcomeHandled = true;
                }

                // 4. Handle Logging (only if we have a valid result)
                if (!outcomeHandled)
                {
                    HandleStopResult(process, res);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn("SafeKillProcess error: " + ex.Message);
            }
        }

        /// <summary>
        /// Logs the outcome of a stop operation for the specified child process.
        /// </summary>
        /// <param name="process">The process wrapper representing the child process whose stop result is being handled. Cannot be null.</param>
        /// <param name="result">The result of the stop operation:
        /// <see langword="true"/> if the process stopped gracefully;
        /// <see langword="false"/> if the process was forcefully terminated;
        /// <see langword="null"/> if the process had already exited before the stop sequence ran.</param>
		private void HandleStopResult(IProcessWrapper process, bool? result)
        {
            var message = string.Empty;

            if (result == true)
                message = $"Child process '{process.Format()}' stopped gracefully with exit code {process.ExitCode}.";
            else if (result == false)
                message = $"Child process '{process.Format()}' was forcefully terminated.";
            else // null
                message = $"Child process '{process.Format()}' had already exited before the stop sequence ran.";

            _logger?.Info(message);
        }

        /// <summary>
        /// Initiates a fire-and-forget post-stop executable if configured.
        /// This runs after the main process and its tree have been terminated.
        /// </summary>
        private void StartPostStopProcess()
        {
            RunFireAndForgetHook(
                 "post-stop",
                 exePath: _options?.PostStopExecutablePath,
                 rawArgs: _options?.PostStopExecutableArgs,
                 hookWorkingDir: _options?.PostStopWorkingDirectory,
                 track: false);
        }

        /// <summary>
        /// Iterates through all tracked process hooks and ensures their underlying resources are released.
        /// Caller must hold lock(_trackedHooks).
        /// </summary>
        /// <remarks>
        /// This method should be called during service shutdown or when a service recovery cycle 
        /// requires a fresh state. It explicitly disposes of each <see cref="Hook"/> to prevent 
        /// native process handle leaks from Pre-Launch or Post-Launch operations.
        /// </remarks>
        private void CleanupTrackedHooks()
        {
            if (_trackedHooks == null) return;

            foreach (var hook in _trackedHooks)
            {
                // Safely dispose each hook to release native handles
                try { hook?.Dispose(); }
                catch (Exception ex)
                {
                    _logger?.Warn($"Failed to dispose tracked hook: {ex.Message}");
                }
            }

            // Clear the collection while still under the lock
            try { _trackedHooks.Clear(); }
            catch (Exception ex) { _logger?.Warn($"Failed to clear tracked hooks: {ex.Message}"); }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="Service"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        /// <remarks>
        /// This method follows the standard .NET Dispose pattern. It ensures that 
        /// <see cref="ExecuteTeardown(TeardownReason)"/> is called to gracefully 
        /// stop background processes and cleanup orchestration state before the 
        /// object is destroyed.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 1. Reuse existing orchestration logic to stop the service
                // This is called while managed resources are still valid.
                ExecuteTeardown(TeardownReason.Stop);

                // 2. Catch-all for test environments and manual disposal
                FlushAndShutdownLogger();

                // 3. Existing designer cleanup for components (timers, etc.)
                if (components != null)
                {
                    components.Dispose();
                }
            }

            // 4. Call the base class implementation to complete the chain
            base.Dispose(disposing);
        }

    }
}
