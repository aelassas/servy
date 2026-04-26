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
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static Servy.Core.Native.NativeMethods;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service
{
    public partial class Service : ServiceBase, IDisposable
    {
        #region Static Fields

        /// <summary>
        /// Compiled regex to identify standard environment variable placeholders.
        /// Includes a match timeout to prevent ReDoS attacks.
        /// </summary>
        private static readonly Regex EnvVarPlaceholderRegex = new Regex(
            @"(%[a-zA-Z_][a-zA-Z0-9_]*%)",
            RegexOptions.Compiled,
            AppConfig.InputRegexTimeout); // 200ms is generous for this pattern

        #endregion

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

        /// <summary>
        /// Minimum StartTimeout (in seconds) before requesting additional SCM time.
        /// Below this threshold the default SCM timeout is sufficient.
        /// </summary>
        private const int ScmStartupRequestThresholdSeconds = 20;

        /// <summary>
        /// The wait hint in milliseconds sent to the Service Control Manager (SCM) during a Pre-Shutdown event.
        /// </summary>
        /// <remarks>
        /// This value informs Windows how long the service expects to take to finish its cleanup. 
        /// Servy defaults to 30,000ms (30 seconds) to allow for graceful shutdown of child processes 
        /// and flushing of pending log buffers.
        /// </remarks>
        private const int PreShutdownWaitHintMs = 30_000;

        #endregion

        #region Private Fields

        /// <summary>The interval, in milliseconds, at which the launcher checks the process status and invokes the OnScmHeartbeat delegate.</summary>
        private readonly int _waitChunkMs;
        /// <summary>Additional time, in milliseconds, used for Service Control Manager (SCM) operations.</summary>
        private readonly int _scmAdditionalTimeMs;

        private readonly SecureData _secureData;
        private readonly IServiceHelper _serviceHelper;
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
        private int _heartbeatIntervalSeconds;
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
            new Helpers.ServiceHelper(new CommandLineProvider()),
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
        /// <pa
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
            IServiceHelper serviceHelper,
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
        /// <remarks>
        /// This is the primary <b>Production Constructor</b>. It automatically initializes the 
        /// <see cref="Logger"/>, validates the Windows Event Source, loads configuration from 
        /// <c>appsettings.json</c>, and initializes the <see cref="SecureData"/> and database systems.
        /// </remarks>
        public Service(
            IServiceHelper serviceHelper,
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

                if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel))
                {
                    logLevel = LogLevel.Info;
                }
                Logger.SetLogLevel(logLevel);
                _logger.SetLogLevel(logLevel);

                if (!Enum.TryParse<DateRotationType>(config["LogRollingInterval"], true, out var dateRotationType))
                {
                    dateRotationType = DateRotationType.None;
                }
                Logger.SetDateRotationType(dateRotationType);

                var isEventLogEnabled = bool.TryParse(config["EnableEventLog"] ?? "true", out var elEnabled) && elEnabled;
                _logger.SetIsEventLogEnabled(isEventLogEnabled);

                if (int.TryParse(config["LogRotationSizeMB"], out var logRotationSizeMB) && logRotationSizeMB > 0)
                {
                    Logger.SetLogRotationSize(logRotationSizeMB);
                }
                else
                {
                    Logger.SetLogRotationSize(Logger.DefaultLogRotationSizeMB);
                }

                string rawUseLocalTimeForRotationConfig = config["UseLocalTimeForRotation"] ?? AppConfig.DefaultUseLocalTimeForRotation.ToString();

                if (!bool.TryParse(rawUseLocalTimeForRotationConfig, out bool useLocalTimeForRotation))
                {
                    useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation;
                }
                Logger.SetUseLocalTimeForRotation(useLocalTimeForRotation);

                // --- Log all configurations for debugging ---
                Logger.Debug("Servy Configuration Loaded:" + Environment.NewLine +
                    $"  WaitChunkMs: {_waitChunkMs}" + Environment.NewLine +
                    $"  ScmAdditionalTimeMs: {_scmAdditionalTimeMs}" + Environment.NewLine +
                    $"  LogLevel: {logLevel}" + Environment.NewLine +
                    $"  LogRollingInterval: {dateRotationType}" + Environment.NewLine +
                    $"  EnableEventLog: {isEventLogEnabled}" + Environment.NewLine +
                    $"  LogRotationSizeMB: {logRotationSizeMB}" + Environment.NewLine +
                    $"  UseLocalTimeForRotation: {useLocalTimeForRotation}");

                // Initialize database and helpers
                var dbContext = new AppDbContext(connectionString);
                DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

                var dapperExecutor = new DapperExecutor(dbContext);
                var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                _secureData = new SecureData(protectedKeyProvider);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();

                _serviceRepository = new ServiceRepository(dapperExecutor, _secureData, xmlSerializer, jsonSerializer);

                // Copy service executable from embedded resources
                var asm = Assembly.GetExecutingAssembly();
                var resourceHelper = new ResourceHelper(_serviceRepository, _processHelper, _processKiller);

                if (!resourceHelper.CopyEmbeddedResourceSync(asm, ResourcesNamespace, ServyRestarterExeFileName, "exe"))
                {
                    _logger.Error($"Failed copying embedded resource: {ServyRestarterExeFileName}.exe");
                }

#if DEBUG
                // Copy debug symbols from embedded resources (only in debug builds)
                if (!resourceHelper.CopyEmbeddedResourceSync(asm, ResourcesNamespace, ServyRestarterExeFileName, "pdb"))
                {
                    _logger.Error($"Failed copying embedded resource: {ServyRestarterExeFileName}.pdb");
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
                    Environment.ExitCode = 1064; // ERROR_EXCEPTION_IN_SERVICE
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
                var fullArgs = _serviceHelper.GetArgs(); // You'll need to expose this helper
                var options = _serviceHelper.ParseOptions(_serviceRepository, _processHelper, fullArgs);

                if (options == null)
                {
                    // Set a non-zero exit code so Windows knows it failed
                    ExitCode = 1064; // ERROR_SERVICE_SPECIFIC_ERROR
                    throw new InvalidOperationException("Failed to initialize service options.");
                }

                // PROMOTE LOGGER IMMEDIATELY
                // Now every log from this point forward (including validation errors) is prefixed.
                var rootLogger = _logger;
                _logger = rootLogger.CreateScoped(options.ServiceName);
                rootLogger.Dispose();

                // Log and Validate using the new scoped _logger
                if (!_serviceHelper.ValidateAndLog(options, _logger, _processHelper, fullArgs))
                {
                    Stop();
                    return;
                }

                _options = options;

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

                // Request timeout for startup to accommodate slow process
                if (_options.StartTimeout > ScmStartupRequestThresholdSeconds) // Use a lower threshold to be safe
                {
                    _serviceHelper.RequestAdditionalTime(this, ClampTimeout(_options.StartTimeout + 10), _logger);
                }

                // Set up attempts file
                SetupAttemptsFile(options);

                // Reset restart attempts on service start to avoid blocking recovery
                if (_recoveryActionEnabled)
                {
                    ConditionalResetRestartAttempts(options);
                }

                // Set up service logging
                HandleLogWriters(options);

                // Run pre-launch process if configured
                if (!StartPreLaunchProcess(options))
                {
                    // Abort if pre-launch fails and service is not allowed to start
                    Stop();
                    return;
                }

                // Start the monitored main process
                StartMonitoredProcess(options);

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
                            await Task.Delay(500, token);

                            // 2. Validate state before touching the native handle
                            if (token.IsCancellationRequested || _isTearingDown || _disposed || _serviceHandle == IntPtr.Zero)
                            {
                                _logger?.Info("Skipping PRESHUTDOWN registration: Service is already tearing down.");
                                return;
                            }

                            SERVICE_STATUS status = new SERVICE_STATUS
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
                            _logger?.Error($"Unexpected error during PRESHUTDOWN registration: {ex.Message}", ex);
                        }
                    }, token); // Pass token to Task.Run to prevent execution if already cancelled
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Exception in OnStart: {ex.Message}", ex);
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
            var attemptsDir = Path.Combine(AppConfig.ProgramDataPath, "recovery");
            SecurityHelper.CreateSecureDirectory(attemptsDir, breakInheritance: false); // ensures folder exists

            string safeServiceName = MakeFilenameSafe(options.ServiceName);
            _restartAttemptsFile = Path.Combine(attemptsDir, $"{safeServiceName}_restartAttempts.dat");
        }

        /// <summary>
        /// Sanitizes a string to be safe for use as a filename by replacing
        /// all invalid filename characters with underscores ('_').
        /// </summary>
        /// <param name="name">The original string to sanitize.</param>
        /// <returns>A sanitized string safe for use as a filename.</returns>
        private string MakeFilenameSafe(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
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

                        File.WriteAllText(_restartAttemptsFile, "0");
                        _logger.Warn("Corrupt or invalid content found in restart attempts file. Resetting counter to 0.");
                        return 0;
                    }
                    else
                    {
                        File.WriteAllText(_restartAttemptsFile, "0");
                        _logger.Warn("Restart attempts file not found. Initializing counter to 0.");
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Error reading restart attempts file: {ex.Message}. Resetting counter to 0.");
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
                    File.WriteAllText(_restartAttemptsFile, attempts.ToString());
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to save restart attempts to file: {ex.Message}");
                }
            }
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

            if (!File.Exists(_restartAttemptsFile)) return;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(_restartAttemptsFile);
            DateTime systemBootTimeUtc;

            try
            {
                // GetTickCount64 returns milliseconds since system start as a ulong.
                // It does not suffer from the 24.9-day rollover bug.
                ulong uptimeMilliseconds = GetTickCount64();
                systemBootTimeUtc = DateTime.UtcNow.AddMilliseconds(-(double)uptimeMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to calculate system boot time: {ex.Message}");
                // Safety: If we can't determine boot time, assume boot just happened now to prevent accidental reset
                systemBootTimeUtc = DateTime.UtcNow;
            }

            // 1. Session Persistence Check
            // If the file's last modification occurred before the current system boot, 
            // the service is starting in a new OS session. We maintain the existing counter 
            // to ensure recovery quotas (like RestartComputer) are respected across reboots.
            // This also handles cases where the service has been stable for long periods.
            if (lastWriteUtc < systemBootTimeUtc)
            {
                _logger.Info($"Maintaining restart counter from previous session. (Last Write: {lastWriteUtc:G}, System Boot: {systemBootTimeUtc:G}).");
                return;
            }

            // 2. Standard same-session reset logic
            int detectionWindowSeconds = options.HeartbeatInterval * options.MaxFailedChecks;

            // Base threshold: detection window + buffer (min 30s or the detection window itself)
            int bufferSeconds = Math.Max(detectionWindowSeconds, 30);
            int resetThresholdSeconds = detectionWindowSeconds + bufferSeconds;

            // Cap at 1 hour, but ensure we always wait at least one full detection cycle
            resetThresholdSeconds = Math.Max(Math.Min(resetThresholdSeconds, 3600), detectionWindowSeconds);

            if (_preLaunchEnabled)
            {
                resetThresholdSeconds += options.PreLaunchTimeout;
            }

            double secondsSinceLastAttempt = (DateTime.UtcNow - lastWriteUtc).TotalSeconds;
            if (secondsSinceLastAttempt > resetThresholdSeconds)
            {
                _logger.Info($"Resetting restart attempts counter. Stable for {secondsSinceLastAttempt:F1} seconds.");
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
        /// The process is allowed to run for at least <see cref="MinPreLaunchTimeoutSeconds"/> seconds or 
        /// <see cref="StartOptions.PreLaunchTimeout"/>, whichever is greater, per attempt.  
        /// If the process exits with a non-zero code or times out, it is retried up to 
        /// <see cref="StartOptions.PreLaunchRetryAttempts"/> times.
        /// </para>
        /// <para>
        /// If <see cref="StartOptions.PreLaunchIgnoreFailure"/> is <c>true</c>, the service will continue starting 
        /// even if all attempts fail. Otherwise, the service startup will be aborted.
        /// </para>
        /// <para>
        /// If <see cref="StartOptions.PreLaunchTimeout"/> is set to 0, the pre-launch hook runs in fire-and-forget 
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

            _preLaunchEnabled = true;

            // 1. Prepare configuration and shared options
            var (_, args) = PreparePreLaunchEnv(options);
            var workingDir = string.IsNullOrWhiteSpace(options.PreLaunchWorkingDirectory)
                ? options.WorkingDirectory
                : options.PreLaunchWorkingDirectory;

            var fireAndForget = options.PreLaunchTimeout == 0;

            var launchOptions = new ProcessLaunchOptions
            {
                ExecutablePath = options.PreLaunchExecutablePath,
                Arguments = args,
                WorkingDirectory = workingDir,
                EnvironmentVariables = options.PreLaunchEnvironmentVariables ?? new List<EnvironmentVariable>(),
                WaitChunkMs = _waitChunkMs,
                ScmAdditionalTimeMs = _scmAdditionalTimeMs,
                OnScmHeartbeat = new Action<int>((time) => _serviceHelper.RequestAdditionalTime(this, time, _logger)),
                StdOutPath = options.PreLaunchStdoutPath,
                StdErrPath = options.PreLaunchStderrPath,
                RedirectToWriters = !fireAndForget,
                FireAndForget = fireAndForget,
                LogErrorAsWarning = options.PreLaunchIgnoreFailure,
            };

            // 2. Handle Fire-and-Forget Mode
            // If Timeout is 0, we don't care about the exit code or output; we just launch and move on.
            if (fireAndForget)
            {
                return RunFireAndForgetPreLaunch(launchOptions, options.PreLaunchIgnoreFailure);
            }

            // 3. Handle Synchronous Mode with Retries
            launchOptions.TimeoutMs = ClampTimeout(Math.Max(options.PreLaunchTimeout, AppConfig.MinPreLaunchTimeoutSeconds));
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
                    _logger?.Warn(message);
                else if (ex != null)
                    _logger?.Error(message, ex);
                else
                    _logger?.Error(message);
            }

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                _logger?.Info($"Starting pre-launch process (attempt {attempt}/{maxAttempts})...");

                try
                {
                    // ProcessLauncher.Start handles the ScmHeartbeat pulses during the wait.
                    var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger);

                    if (process.ExitCode == 0)
                    {
                        _logger?.Info("Pre-launch process completed successfully.");
                        return true;
                    }

                    LogIssue($"Pre-launch process exited with code {process.ExitCode}.");
                }
                catch (Exception ex)
                {
                    LogIssue($"Pre-launch process attempt {attempt} failed: {ex.Message}", ex);
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
        /// Prepares and expands environment variables and command-line arguments for the pre-launch process.
        /// </summary>
        private (Dictionary<string, string> env, string args) PreparePreLaunchEnv(StartOptions options)
        {
            // 1. Expand environment variables list
            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(
                options.PreLaunchEnvironmentVariables ?? options.EnvironmentVariables);

            // 2. Audit expanded variables for leftover placeholders
            foreach (var kvp in expandedEnv)
            {
                LogUnexpandedPlaceholders(kvp.Value ?? string.Empty, $"[Pre-Launch] Environment Variable '{kvp.Key}'");
            }

            // 3. Expand command-line arguments using the expanded environment
            var args = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.PreLaunchExecutableArgs ?? string.Empty, expandedEnv);

            // 4. Audit arguments for leftover placeholders
            LogUnexpandedPlaceholders(args, "[Pre-Launch] Arguments");

            return (expandedEnv, args);
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
        /// - If <see cref="StartOptions.StdOutPath"/> is valid, a <see cref="RotatingStreamWriter"/> is created for stdout.
        /// - If <see cref="StartOptions.StdErrPath"/> is provided:
        ///     - If it equals <see cref="StartOptions.StdOutPath"/> (case-insensitive), stderr shares the stdout writer.
        ///     - Otherwise, a separate <see cref="RotatingStreamWriter"/> is created for stderr.
        /// - If <see cref="StdErrPath"/> is null, empty, or whitespace, no stderr writer is created.
        /// </remarks>
        private void HandleLogWriters(StartOptions options)
        {
            /// <summary>
            /// Helper method to create a rotating writer if the path is valid.
            /// Logs an error if the path is invalid or null/whitespace.
            /// </summary>
            /// <param name="path">The file path for the log writer.</param>
            /// <returns>A <see cref="IStreamWriter"/> instance or null if the path is invalid.</returns>
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
            _stdoutWriter = CreateWriter(options.StdOutPath);

            // Only create stderr writer if a path is provided
            if (!string.IsNullOrWhiteSpace(options.StdErrPath))
            {
                // If stderr path equals stdout path (explicitly), use the same writer
                if (_stdoutWriter != null &&
                    options.StdErrPath.Equals(options.StdOutPath, StringComparison.OrdinalIgnoreCase))
                {
                    _stderrWriter = _stdoutWriter;
                }
                else
                {
                    _stderrWriter = CreateWriter(options.StdErrPath);
                }
            }
        }

        /// <summary>
        /// Starts the monitored child process using the executable path, arguments, and working directory from the options.
        /// Also sets the process priority accordingly.
        /// </summary>
        /// <param name="options">The start options containing executable details and priority.</param>
        private void StartMonitoredProcess(StartOptions options)
        {
            StartProcess(options.ExecutablePath, options.ExecutableArgs, options.WorkingDirectory, options.EnvironmentVariables);
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
        private void StartProcess(string realExePath, string realArgs, string workingDir, List<EnvironmentVariable> environmentVariables)
        {
            _ = AllocConsole(); // inherited
            _ = SetConsoleCtrlHandler(null, false); // inherited
            _ = SetConsoleOutputCP(CP_UTF8);

            var (expandedEnv, expandedArgs) = ExpandAndAudit(environmentVariables, realArgs);

            _realExePath = realExePath;
            _realArgs = expandedArgs;
            _workingDir = workingDir;
            _environmentVariables = environmentVariables;

            // Log any remaining unexpanded placeholders
            LogUnexpandedPlaceholders(_realArgs, "Arguments");

            // Configure the process start info
            var psi = new ProcessStartInfo
            {
                FileName = _realExePath,
                Arguments = _realArgs,
                WorkingDirectory = _workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };

            foreach (var envVar in expandedEnv)
            {
                psi.EnvironmentVariables[envVar.Key] = envVar.Value;
            }

            // Ensure UTF-8 encoding and buffered mode for python
            ProcessLauncher.ApplyLanguageFixes(psi);
            EnsureJavaUTF8Encoding(psi);

            _childProcess = _processFactory.Create(psi, _logger);

            // Enable events and attach output/error handlers
            _childProcess.EnableRaisingEvents = true;
            _childProcess.OutputDataReceived += OnOutputDataReceived;
            _childProcess.ErrorDataReceived += OnErrorDataReceived;
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

                // Re-throw so the caller (OnStart) stops the service and signals the SCM
                throw;
            }
            finally
            {
                _ = FreeConsole();
                _ = SetConsoleCtrlHandler(null, true);
            }

            // --- The code below this point ONLY executes if Start() was successful ---

            // Persist PID and PreviousStopTimeout
            PersistProcessState(_childProcess.Id, true);

            // Begin async reading of output and error streams
            _childProcess.BeginOutputReadLine();
            _childProcess.BeginErrorReadLine();

            // Fire and forget the post-launch script when process confirmed running
            _cancellationSource?.Dispose();
            var cts = new CancellationTokenSource();
            _cancellationSource = cts;

            Task.Run(async () =>
            {
                try
                {
                    if (await _childProcess.WaitUntilRunningAsync(TimeSpan.FromSeconds(_options.StartTimeout), cts.Token))
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
                    _logger?.Error($"Unexpected error in post-launch action: {ex.Message}", ex);
                }
            }, cts.Token);
        }

        /// <summary>
        /// Expands environment variables and command-line arguments, auditing both for unexpanded placeholders.
        /// </summary>
        /// <param name="vars">The list of environment variables to expand.</param>
        /// <param name="rawArgs">The raw command-line arguments to expand.</param>
        /// <param name="contextPrefix">An optional prefix for logging (e.g., "Pre-Launch", "Post-Stop").</param>
        /// <returns>A tuple containing the expanded environment dictionary and the expanded arguments string.</returns>
        private (Dictionary<string, string> env, string expandedArgs) ExpandAndAudit(
            List<EnvironmentVariable> vars, string rawArgs, string contextPrefix = "")
        {
            string prefix = string.IsNullOrWhiteSpace(contextPrefix) ? string.Empty : $"[{contextPrefix}] ";

            // 1. Expand environment variables list
            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // 2. Audit expanded variables for leftover placeholders
            foreach (var kvp in expandedEnv)
            {
                LogUnexpandedPlaceholders(kvp.Value ?? string.Empty, $"{prefix}Environment Variable '{kvp.Key}'");
            }

            // 3. Expand command-line arguments using the expanded environment
            var expandedArgs = EnvironmentVariableHelper.ExpandEnvironmentVariables(rawArgs, expandedEnv);

            // 4. Audit arguments for leftover placeholders
            LogUnexpandedPlaceholders(expandedArgs, $"{prefix}Arguments");

            return (expandedEnv, expandedArgs);
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
        /// Ensures that Java processes use UTF-8 as the default file encoding.
        /// </summary>
        /// <param name="psi">The <see cref="ProcessStartInfo"/> used to start the Java process.</param>
        /// <remarks>
        /// This method checks if the Java process already specifies a <c>-Dfile.encoding</c> option.
        /// If not, it prepends <c>-Dfile.encoding=UTF-8</c> to the argument list to enforce UTF-8 encoding.
        /// </remarks>
        private void EnsureJavaUTF8Encoding(ProcessStartInfo psi)
        {
            var fileName = psi.FileName != null ? psi.FileName.ToLowerInvariant() : string.Empty;
            var args = psi.Arguments != null ? psi.Arguments.ToLowerInvariant() : string.Empty;

            if ((fileName.Contains("java") || args.Contains(".java")) &&
                !args.Contains("-dfile.encoding"))
            {
                psi.Arguments = string.Format("-Dfile.encoding=UTF-8 {0}", psi.Arguments).Trim();
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
            if (_options == null || string.IsNullOrWhiteSpace(_options.PostLaunchExecutablePath))
                return;

            try
            {
                // 1. Prepare Environment and Arguments
                var (_, args) = ExpandAndAudit(
                    _options.EnvironmentVariables,
                    _options.PostLaunchExecutableArgs ?? string.Empty,
                    "Post-Launch"
                 );

                var workingDir = string.IsNullOrWhiteSpace(_options.PostLaunchWorkingDirectory)
                    ? Path.GetDirectoryName(_options.PostLaunchExecutablePath) ?? string.Empty
                    : _options.PostLaunchWorkingDirectory;

                // 2. Configure Launch Options
                var launchOptions = new ProcessLaunchOptions
                {
                    ExecutablePath = _options.PostLaunchExecutablePath,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    EnvironmentVariables = _options.EnvironmentVariables,
                    FireAndForget = true, // Post-launch does not block service startup
                };

                _logger?.Info($"Running post-launch program: {launchOptions.ExecutablePath}");

                // 3. Launch via Centralized Utility
                var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger);

                // 4. Track the native process for teardown orchestration
                if (process?.UnderlyingProcess is Process nativeProcess)
                {
                    lock (_trackedHooks)
                    {
                        _trackedHooks.Add(new Hook { OperationName = "post-launch", Process = nativeProcess });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to run post-launch program: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Executes the configured failure program if specified in the service options.
        /// This is intended to be called when the main child process fails to start or when
        /// all recovery action retries have failed.
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
            if (_options == null || string.IsNullOrWhiteSpace(_options.FailureProgramPath))
                return;

            try
            {
                var (_, args) = ExpandAndAudit(
                    _options.EnvironmentVariables,
                    _options.FailureProgramArgs ?? string.Empty
                    );

                var workingDir = string.IsNullOrWhiteSpace(_options.FailureProgramWorkingDirectory)
                    ? Path.GetDirectoryName(_options.FailureProgramPath) ?? string.Empty
                    : _options.FailureProgramWorkingDirectory;

                var psi = new ProcessStartInfo
                {
                    FileName = _options.FailureProgramPath,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Fire-and-forget: start the process without waiting
                var process = Process.Start(psi);

                if (process != null)
                {
                    // IMPORTANT: This process is not added to _trackedHooks to be killed during cleanup, 
                    // because it's meant to run independently after we've stopped the main process tree.
                    using (process)
                    {
                        _logger?.Info($"Running failure program: {psi.FileName}");
                        // The OS process continues running, but the managed handle is freed.
                    }
                }
                else
                {
                    _logger?.Error($"Failed to run failure program: {psi.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to run failure program: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Logs a warning for any unexpanded environment variable placeholders found in the given string.
        /// </summary>
        /// <param name="input">The string to inspect.</param>
        /// <param name="context">The descriptive context (e.g., "Arguments").</param>
        private void LogUnexpandedPlaceholders(string input, string context)
        {
            if (string.IsNullOrEmpty(input))
                return;

            try
            {
                var matches = EnvVarPlaceholderRegex.Matches(input);

                foreach (Match match in matches)
                {
                    string placeholder = match.Value;
                    _logger?.Warn($"Unexpanded environment variable {placeholder} in {context}");
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                // Log that the check itself timed out to avoid silent failure
                _logger?.Error($"Regex timeout while inspecting placeholders in {context}. Input length: {input.Length}", ex);
            }
        }

        /// <summary>
        /// Handles redirected standard output from the child process.
        /// Writes output lines to the rotating stdout writer.
        /// </summary>
        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _stdoutWriter?.WriteLine(e.Data);
            }
        }

        /// <summary>
        /// Handles redirected standard error output from the child process.
        /// Writes error lines to the rotating stderr writer and logs errors.
        /// </summary>
        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _stderrWriter?.WriteLine(e.Data);
                //_logger?.Error($"[Error] {e.Data}");
            }
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
                var serviceDto = _serviceRepository.GetByName(_serviceName);

                if (serviceDto != null)
                {
                    serviceDto.Pid = pid;
                    if (setPreviousStopTimeout)
                        serviceDto.PreviousStopTimeout = _options?.StopTimeout;

                    if (pid == null)
                    {
                        serviceDto.ActiveStdoutPath = null;
                        serviceDto.ActiveStderrPath = null;
                    }
                    else
                    {
                        serviceDto.ActiveStdoutPath = _options?.StdOutPath;
                        serviceDto.ActiveStderrPath = _options?.StdErrPath;
                    }

                    _serviceRepository.Update(serviceDto);
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
            if (_isTearingDown || _disposed) return;

            _logger?.Warn("Child process exit detected via event.");
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
                    _logger?.Info("Child process exited successfully (Code 0).");
                    shouldStop = true;
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
                var delayMs = Math.Max(ClampTimeout(_options.HeartbeatInterval) - 5000, 5000);
                _logger?.Info($"[OnProcessExited] Failure threshold reached. Scheduling recovery in {delayMs / 1000}s...");

                // Fire-and-forget the recovery task safely
                _ = ScheduleRecoveryAsync(delayMs);
            }

            // Helper local function to handle the asynchronous wait and safety checks
            async Task ScheduleRecoveryAsync(int delay)
            {
                try
                {
                    // Use the cancellation token to allow the delay to be aborted during service shutdown
                    await Task.Delay(delay, _cancellationSource?.Token ?? CancellationToken.None);

                    // Re-check state after the delay to ensure we aren't mid-shutdown
                    if (!_isTearingDown && !_disposed)
                    {
                        InitiateRecovery();
                        return; // Successfully handed off to the recovery orchestrator
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

                // SAFETY RESET: If we reach this line, InitiateRecoveryAsync was skipped
                // or the delay threw an exception. We MUST clear the gatekeeper flag.
                _isRecovering = false;
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
            _logger?.Warn($"Health check failed ({_failedChecks}/{_maxFailedChecks}).");

            if (_failedChecks >= _maxFailedChecks)
            {
                // Set this immediately to block other threads/timers from incrementing
                // while we are waiting for the recovery task to execute.
                _isRecovering = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the priority class of the child process.
        /// Logs info on success or a warning if it fails.
        /// </summary>
        /// <param name="priority">The process priority to set.</param>
        public void SetProcessPriority(ProcessPriorityClass priority)
        {
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
            _heartbeatIntervalSeconds = options.HeartbeatInterval;
            _maxFailedChecks = options.MaxFailedChecks;
            _recoveryAction = options.RecoveryAction;

            if (options.EnableHealthMonitoring && options.HeartbeatInterval > 0 && options.MaxFailedChecks > 0 && options.RecoveryAction != RecoveryAction.None)
            {
                _healthCheckTimer = _timerFactory.Create(_heartbeatIntervalSeconds * 1000.0);
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
            bool shouldStop = false;
            int currentAttempts = 0;

            lock (_healthCheckLock)
            {
                // Guard: prevent concurrent recovery attempts
                if (_isTearingDown || _disposed)
                    return;

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
                // The actual logic (RestartService, RestartProcess, etc.)
                ExecuteRecoveryAction(currentAttempts);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Critical error during recovery execution: {ex.Message}");
            }
            finally
            {
                // GUARANTEED EXECUTION: The gate always opens.
                // Because _isRecovering is volatile, it is safe
                // to toggle directly without risking a deadlock
                // in the finally block.
                _isRecovering = false;
            }
        }

        /// <summary>
        /// Orchestrates the recovery process when health check thresholds are exceeded.
        /// Handles restart attempt persistence, quota enforcement, and state synchronization.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="_isRecovering"/> to prevent concurrent recovery executions.
        /// If the maximum number of restart attempts is reached, it will trigger a full service stop.
        /// </remarks>
        private void ExecuteRecoveryAction(int attemptCount)
        {
            try
            {
                _logger?.Warn($"Performing recovery action '{_recoveryAction}' ({attemptCount}/{(_maxRestartAttempts > 0 ? _maxRestartAttempts.ToString() : "unlimited")}).");

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
                            ClampTimeout(_options?.StopTimeout ?? AppConfig.DefaultStopTimeout)
                        );
                        break;

                    case RecoveryAction.RestartComputer:
                        _isRebooting = true;
                        _serviceHelper.RestartComputer(_logger);
                        break;

                    default:
                        _logger?.Info($"Recovery action '{_recoveryAction}' requires no specific logic.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error during recovery execution: {ex.Message}");
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
            if (_isTearingDown || _disposed) return;

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
                    try { exitCode = process?.ExitCode; } catch (Exception ex) { _logger?.Debug($"Could not read ExitCode for health check.", ex); }

                    if (exitCode == 0)
                    {
                        _logger?.Info("Child process exited successfully. Service will stop.");
                        shouldStop = true;
                    }
                    else
                    {
                        // Unified recovery check
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
                    return;
                }

                _logger?.Info("Pre-Shutdown received. Starting orchestrated teardown...");

                if (_serviceHandle == IntPtr.Zero)
                {
                    _logger?.Error("Service handle is null! SCM notification impossible. Falling back to synchronous teardown.");

                    // Log the completion intention right before the logger is destroyed
                    _logger?.Info("Pre-Shutdown handling complete. Setting SERVICE_STOPPED via Environment.Exit.");
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
                        Environment.Exit(1);
                    }
                    return;
                }

                // 1. Immediately tell SCM we are transitioning to a stop state and need a 30s window.
                // This moves the service into the STOP_PENDING state in the eyes of the OS.
                UpdateServiceStatus(SERVICE_STOP_PENDING, PreShutdownWaitHintMs);

                Task<bool> stopTask = Task.Run(() => ExecuteTeardown(TeardownReason.PreShutdown));

                // 2. Wait in 2-second pulses. 
                // We increment the checkpoint each pulse to prove to the SCM that we haven't hung.
                // This loop is guaranteed to terminate because the underlying teardown logic 
                // (SafeKillProcess) enforces an absolute, stopwatch-backed timeout limit.
                int interval = 2000;
                while (!stopTask.Wait(interval))
                {
                    _checkPoint++;
                    UpdateServiceStatus(SERVICE_STOP_PENDING, PreShutdownWaitHintMs);
                }

                // 3. Handle task completion results safely
                if (stopTask.IsFaulted)
                {
                    _logger?.Error($"Teardown task failed: {stopTask.Exception?.InnerException?.Message}");
                }

                // 4. Final Signal: Inform the SCM that the service has successfully reached the STOPPED state.
                _logger?.Info("Pre-Shutdown handling complete. Setting SERVICE_STOPPED.");
                UpdateServiceStatus(SERVICE_STOPPED, 0);

                // 5. SHUTDOWN LOGGER LAST
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
            try
            {
                _ = Task.Run(() =>
                {
                    Logger.Shutdown();

                    if (_logger != null)
                    {
                        _logger.Dispose();
                        _logger = null;
                    }

                }).Wait(1500); // 1.5 seconds max for local disk I/O
            }
            catch
            {
                // Fail-silent
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

                // Construct the Win32 status structure.
                SERVICE_STATUS status = new SERVICE_STATUS
                {
                    dwServiceType = SERVICE_WIN32_OWN_PROCESS,
                    dwCurrentState = state,
                    // If stopped, we accept nothing. Otherwise, we maintain our acceptance of Stop and Preshutdown.
                    dwControlsAccepted = state == SERVICE_STOPPED
                        ? 0
                        : (SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_PRESHUTDOWN),
                    dwWin32ExitCode = 0,
                    dwServiceSpecificExitCode = 0,
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
                    _logger?.Error($"Teardown error during {reason}: {ex.Message}", ex);

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

                        _cancellationSource?.Dispose();
                        _cancellationSource = null;

                        _secureData?.Dispose();
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
                    SafeKillProcess(_childProcess, ClampTimeout(_options.StopTimeout));

                    // Stop async reads first
                    _childProcess.CancelOutputRead();
                    _childProcess.CancelErrorRead();

                    // Unsubscribe output only after the process is dead to catch final logs
                    _childProcess.OutputDataReceived -= OnOutputDataReceived;
                    _childProcess.ErrorDataReceived -= OnErrorDataReceived;
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
                                // Note: p.Id is usually safe if cached, but p.ProcessName 
                                // can throw if the process is in a weird state.
                                var opName = string.IsNullOrWhiteSpace(hook.OperationName) ? "unnamed" : hook.OperationName;
                                _logger?.Info($"Cleaning up orphaned {opName} hook process tree.");

                                Helpers.ProcessHelper.KillProcessTree(hook.Process);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log locally for debug, but don't let it stop the loop
                            Logger.Error("Cleanup failed.", ex);
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
                    _stdoutWriter?.Dispose();
                    _stderrWriter?.Dispose();
                    _stdoutWriter = null;
                    _stderrWriter = null;
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
                try
                {
                    if (_healthCheckTimer != null)
                    {
                        _healthCheckTimer.Elapsed -= CheckHealth;
                        _healthCheckTimer.Stop();
                        _healthCheckTimer.Dispose();
                        _healthCheckTimer = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"Error disposing health check timer: {ex.Message}");
                }

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
            if (string.IsNullOrWhiteSpace(options.PreStopExecutablePath))
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
                var (_, args) = ExpandAndAudit(
                            options.EnvironmentVariables,
                            options.PreStopExecutableArgs ?? string.Empty,
                            "Pre-Stop"
                        );

                // 2. Configure Launch Options
                var effectiveTimeoutMs = ClampTimeout(_options.PreStopTimeout);
                var launchOptions = new ProcessLaunchOptions
                {
                    ExecutablePath = options.PreStopExecutablePath,
                    Arguments = args,
                    WorkingDirectory = string.IsNullOrWhiteSpace(options.PreStopWorkingDirectory) ? options.WorkingDirectory : options.PreStopWorkingDirectory,
                    EnvironmentVariables = options.EnvironmentVariables,
                    FireAndForget = (effectiveTimeoutMs == 0),
                    TimeoutMs = effectiveTimeoutMs,
                    WaitChunkMs = _waitChunkMs,
                    ScmAdditionalTimeMs = _scmAdditionalTimeMs,
                    OnScmHeartbeat = time => _serviceHelper.RequestAdditionalTime(this, time, _logger),
                    LogErrorAsWarning = !logAsError,
                };

                // 3. Launch and Evaluate
                using (var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger))
                {
                    if (process == null)
                    {
                        LogIssue($"Failed to run pre-stop process: {launchOptions.ExecutablePath}");
                        return !logAsError;
                    }

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
                LogIssue($"Pre-stop process failed: {ex.Message}", ex);
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
            // FIX: Remove `|| process.HasExited` so we always clean up orphans
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
                    // FIX: Use GetAllDescendants to scan the entire deep tree instead of just Level 1
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

                // Total timeout = (Parent + Children) * timeoutMs + 10s safety buffer
                var totalTimeoutMs = (((long)(childCount + 1)) * timeoutMs) + 10_000L;

                Task<bool> stopTask = Task.Run(() =>
                {
                    // FIX: Send Ctrl+C to the root wrapper first. 
                    // This natively broadcasts the signal to all console-sharing children (like Python).
                    _logger?.Info("Signaling main wrapper process (broadcasts to console group)...");
                    bool mainExitedGracefully = true;
                    if (!process.HasExited)
                    {
                        mainExitedGracefully = process.Stop(timeoutMs) ?? true;
                    }

                    // FIX: Clean up descendants afterward. 
                    // Thanks to native P/Invoke, we can still trace children even if the parent exited instantly.
                    _logger?.Info($"Initiating descendant cleanup for PID {parentPid}...");
                    process.StopDescendants(parentPid, parentStartTime, timeoutMs);

                    return mainExitedGracefully;
                });

                // 2. Wait for the task to complete in 5-second pulses
                // This prevents ContextSwitchDeadlock and keeps the SCM happy.

                var maxWaitTime = TimeSpan.FromMilliseconds(totalTimeoutMs);
                var sw = Stopwatch.StartNew();

                while (!stopTask.Wait(5000))
                {
                    if (sw.Elapsed > maxWaitTime)
                    {
                        _logger?.Error("Stop operation exceeded safety limit. The process tree may be hung at the kernel level.");
                        break; // Exit the loop and let the service finish/crash
                    }

                    // Request 15s of "Wait Hint" every 5s pulse
                    _serviceHelper.RequestAdditionalTime(this, _scmAdditionalTimeMs, null);
                }

                sw.Stop();

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
        /// <param name="result">The result of the stop operation. <see langword="true"/> if the process was canceled; <see
        /// langword="false"/> if the process was terminated; <see langword="null"/> if the stop operation timed out or
        /// failed.</param>
        private void HandleStopResult(IProcessWrapper process, bool? result)
        {
            var message = string.Empty;

            if (result == true)
                message = $"Child process '{process.Format()}' canceled with code {process.ExitCode}.";
            else if (result == false)
                message = $"Child process '{process.Format()}' terminated.";
            else
                message = $"Child process '{process.Format()}' stop timed out or failed.";

            _logger?.Info(message);
        }

        /// <summary>
        /// Initiates a fire-and-forget post-stop executable if configured.
        /// This runs after the main process and its tree have been terminated.
        /// </summary>
        private void StartPostStopProcess()
        {
            if (_options == null || string.IsNullOrWhiteSpace(_options.PostStopExecutablePath))
                return;

            try
            {
                // 1. Prepare Environment and Arguments
                var (_, args) = ExpandAndAudit(
                            _options.EnvironmentVariables,
                            _options.PostStopExecutableArgs ?? string.Empty,
                            "Post-Stop"
                        );

                var workingDir = string.IsNullOrWhiteSpace(_options.PostStopWorkingDirectory)
                    ? Path.GetDirectoryName(_options.PostStopExecutablePath) ?? string.Empty
                    : _options.PostStopWorkingDirectory;

                // 2. Configure Launch Options
                var launchOptions = new ProcessLaunchOptions
                {
                    ExecutablePath = _options.PostStopExecutablePath,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    EnvironmentVariables = _options.EnvironmentVariables,
                    FireAndForget = true // Post-stop is always fire-and-forget
                };

                _logger?.Info($"Running post-stop program: {launchOptions.ExecutablePath}");

                // 3. Launch via Centralized Utility
                // We use the wrapper but do not store it in _trackedHooks as this process 
                // should outlive the service teardown.
                using (var process = ProcessLauncher.Start(launchOptions, _processFactory, _logger))
                {
                    if (process == null)
                    {
                        _logger?.Error($"Failed to start post-stop program: {launchOptions.ExecutablePath}");
                    }
                    // The process wrapper is disposed, but the underlying OS process continues.
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to run post-stop program: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Iterates through all tracked process hooks and ensures their underlying resources are released.
        /// </summary>
        /// <remarks>
        /// This method should be called during service shutdown or when a service recovery cycle 
        /// requires a fresh state. It explicitly disposes of each <see cref="Hook"/> to prevent 
        /// native process handle leaks from Pre-Launch or Post-Launch operations.
        /// </remarks>
        private void CleanupTrackedHooks()
        {
            if (_trackedHooks == null) return;

            // Use the collection itself as the sync root to stay consistent with Cleanup()
            lock (_trackedHooks)
            {
                try
                {
                    foreach (var hook in _trackedHooks)
                    {
                        // Safely dispose each hook to release native handles
                        hook?.Dispose();
                    }

                    // Clear the collection while still under the lock
                    _trackedHooks.Clear();
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error during hooks cleanup: {ex.Message}");
                }
            }
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

            // 3. Call the base class implementation to complete the chain
            base.Dispose(disposing);
        }

    }
}
