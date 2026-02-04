using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.Native;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service
{
    public partial class Service : ServiceBase
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
        /// Wait chunk in milliseconds. Used in pre-launch and pre-stop hooks.
        /// </summary>
        private const int WaitChunkMs = 5000;

        /// <summary>
        /// Specifies the additional time, in milliseconds, used for Service Control Manager (SCM) operations.
        /// </summary>
        /// <remarks>This constant can be used to extend timeouts or delays when interacting with the
        /// Windows Service Control Manager to account for potential processing overhead.</remarks>
        private const int ScmAdditionalTime = 15_000;

        /// <summary>
        /// The service can perform cleanup tasks during a system shutdown. 
        /// Setting this flag in the 'acceptedCommands' bitmask enables the service 
        /// to receive the SERVICE_CONTROL_PRESHUTDOWN notification.
        /// </summary>
        private const int SERVICE_ACCEPT_PRESHUTDOWN = 0x00000100;

        /// <summary>
        /// The control code sent by the SCM to notify a service that the system is 
        /// about to shut down. This notification provides a longer timeout window 
        /// than the standard SERVICE_CONTROL_SHUTDOWN signal.
        /// </summary>
        private const int SERVICE_CONTROL_PRESHUTDOWN = 0x0000000F;

        /// <summary>
        /// The service is stopping. This corresponds to the <c>SERVICE_STOP_PENDING</c> state.
        /// </summary>
        private const int SERVICE_STOP_PENDING = 0x00000003;

        /// <summary>
        /// The service is not running. This corresponds to the <c>SERVICE_STOPPED</c> state.
        /// </summary>
        private const int SERVICE_STOPPED = 0x00000001;

        /// <summary>
        /// The service can be stopped. This control code allows the SCM to send the <c>SERVICE_CONTROL_STOP</c> request.
        /// </summary>
        private const int SERVICE_ACCEPT_STOP = 0x00000001;

        /// <summary>
        /// The service runs in its own process. Corresponds to <c>SERVICE_WIN32_OWN_PROCESS</c>.
        /// </summary>
        private const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        #endregion

        #region P/Invoke and Structs

        /// <summary>
        /// Updates the service control manager's status information for the calling service.
        /// </summary>
        /// <param name="hServiceStatus">A handle to the status information structure for the current service.</param>
        /// <param name="lpServiceStatus">A pointer to the <see cref="SERVICE_STATUS"/> structure containing the latest status information.</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);

        /// <summary>
        /// Contains status information for a service.
        /// </summary>
        /// <remarks>
        /// See <see href="https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_status">SERVICE_STATUS documentation</see> for details.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            /// <summary>The type of service.</summary>
            public int dwServiceType;

            /// <summary>The current state of the service (e.g., STARTING, RUNNING, STOPPING).</summary>
            public int dwCurrentState;

            /// <summary>The control codes the service accepts and processes in its handler function.</summary>
            public int dwControlsAccepted;

            /// <summary>The error code the service uses to report an error that occurs when it is starting or stopping.</summary>
            public int dwWin32ExitCode;

            /// <summary>A service-specific error code that the service returns when an error occurs while the service is starting or stopping.</summary>
            public int dwServiceSpecificExitCode;

            /// <summary>The check-point value that the service increments periodically to report its progress during a lengthy start, stop, pause, or continue operation.</summary>
            public int dwCheckPoint;

            /// <summary>The estimated time required for a pending start, stop, pause, or continue operation, in milliseconds.</summary>
            public int dwWaitHint;
        }

        #endregion

        #region Private Fields

        private readonly IServiceHelper _serviceHelper;
        private readonly ILogger _logger;
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
        private bool _isRecovering = false;
        private int _maxRestartAttempts = 3; // Maximum number of restart attempts
        private List<EnvironmentVariable> _environmentVariables = new List<EnvironmentVariable>();
        private bool _disposed = false; // Tracks whether Dispose has been called
        private bool _recoveryActionEnabled = false;
        private string _restartAttemptsFile;
        private bool _preLaunchEnabled;
        private StartOptions _options;
        private CancellationTokenSource _cancellationSource;
        private readonly IServiceRepository _serviceRepository;
        private readonly List<Hook> _trackedHooks = new List<Hook>();
        private IntPtr _serviceHandle;
        private uint _checkPoint = 0;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the service stops, used for testing purposes.
        /// </summary>
        public event Action OnStoppedForTest;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class
        /// using the default <see cref="ServiceHelper"/> implementation,
        /// path validator and default factories for stream writer, timer, and process.
        /// </summary>
        public Service() : this(
            new Helpers.ServiceHelper(new CommandLineProvider()),
            new EventLogLogger(AppConfig.ServiceNameEventSource),
            new StreamWriterFactory(),
            new TimerFactory(),
            new ProcessFactory(),
            new PathValidator()
          )
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class.
        /// </summary>
        public Service(
            IServiceHelper serviceHelper,
            ILogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory,
            IPathValidator pathValidator,
            IServiceRepository serviceRepository) // allow injection
        {
            ServiceName = AppConfig.ServiceNameEventSource;

            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamWriterFactory = streamWriterFactory ?? throw new ArgumentNullException(nameof(streamWriterFactory));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
            _pathValidator = pathValidator;
            _options = null;
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class.
        /// Sets the service name, initializes the event log source, and assigns the required dependencies.
        /// </summary>
        /// <param name="serviceHelper">The service helper instance to use.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <param name="streamWriterFactory">Factory to create rotating stream writers for stdout and stderr.</param>
        /// <param name="timerFactory">Factory to create timers for health monitoring.</param>
        /// <param name="processFactory">Factory to create process wrappers for launching and managing child processes.</param>
        /// <param name="pathValidator">Path Validator.</param>
        public Service(
            IServiceHelper serviceHelper,
            ILogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory,
            IPathValidator pathValidator)
        {
            ServiceName = AppConfig.ServiceNameEventSource;

            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamWriterFactory = streamWriterFactory ?? throw new ArgumentNullException(nameof(streamWriterFactory));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
            _pathValidator = pathValidator;
            _options = null;

            // Load configuration
            var config = ConfigurationManager.AppSettings;

            var connectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
            var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
            var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

            // Initialize database and helpers
            var dbContext = new AppDbContext(connectionString);
            DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

            var dapperExecutor = new DapperExecutor(dbContext);
            var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
            var securePassword = new SecurePassword(protectedKeyProvider);
            var xmlSerializer = new XmlServiceSerializer();

            _serviceRepository = new ServiceRepository(dapperExecutor, securePassword, xmlSerializer);

            // Enable Shutdown Notifications
            CanShutdown = true;

            // Tell Windows we accept PreShutdown
            InitializePreShutdownHook();
        }

        /// <summary>
        /// Configures the service to intercept the Windows Pre-Shutdown notification.
        /// </summary>
        /// <remarks>
        /// This method uses reflection to access the private <c>acceptedCommands</c> field of the 
        /// <see cref="ServiceBase"/> class. By injecting the <c>SERVICE_ACCEPT_PRESHUTDOWN</c> (0x100) 
        /// bitmask, the service informs the Service Control Manager (SCM) that it should receive 
        /// control code 15 (Pre-Shutdown) before the standard shutdown sequence begins. 
        /// This allows the service more time to clean up child processes or flush logs.
        /// </remarks>
        private void InitializePreShutdownHook()
        {
            try
            {
                // Modern .NET uses "_acceptedCommands", Legacy uses "acceptedCommands"
                string[] fieldNames = { "_acceptedCommands", "acceptedCommands" };
                FieldInfo acceptedField = null;

                foreach (var name in fieldNames)
                {
                    acceptedField = typeof(ServiceBase).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

                    if (acceptedField != null) break;
                }

                if (acceptedField != null)
                {
                    int val = (int)(acceptedField.GetValue(this) ?? 0);
                    acceptedField.SetValue(this, val | SERVICE_ACCEPT_PRESHUTDOWN);
                }
                else
                {
                    _logger?.Error("[Pre-Shutdown] Hook Failed: Neither '_acceptedCommands' nor 'acceptedCommands' found.");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[Pre-Shutdown] Reflection Hook Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the Windows service is started.
        /// Initializes startup options, configures logging, validates working directories,
        /// runs the optional pre-launch process (if configured), and starts the monitored service process.
        /// Also sets up health monitoring for the service.
        /// <para>
        /// This method now resets the restart attempt counter at startup to ensure that
        /// previous restart limits do not persist across service restarts (including
        /// restarts triggered by <c>Servy.Restarter.exe</c>).
        /// </para>
        /// </summary>
        /// <param name="args">Command-line arguments passed to the service by the Service Control Manager.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                _ = NativeMethods.FreeConsole();
                _ = NativeMethods.SetConsoleCtrlHandler(null, true);

                // Load and validate service startup options
                var options = _serviceHelper.InitializeStartup(_logger);
                if (options == null)
                {
                    // Set a non-zero exit code so Windows knows it failed
                    ExitCode = 1064; // ERROR_SERVICE_SPECIFIC_ERROR
                    throw new InvalidOperationException("Failed to initialize service options.");
                }

                _options = options;

                try
                {
                    // List of possible field names across different .NET versions
                    string[] possibleFieldNames = new[]
                    {
                        "serviceStatusHandle",  // .NET Framework
                        "statusHandle",         // Alternative .NET Framework
                        "_statusHandle",        // Modern .NET (private with underscore)
                        "_serviceStatusHandle", // Modern .NET variant
                        "m_statusHandle",       // Older naming convention
                        "m_serviceStatusHandle" // Older naming convention
                    };

                    FieldInfo serviceHandleField = null;

                    foreach (var fieldName in possibleFieldNames)
                    {
                        serviceHandleField = typeof(ServiceBase).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                        if (serviceHandleField != null)
                        {
                            //_logger?.Info($"Found service handle field: {fieldName}");
                            break;
                        }
                    }

                    if (serviceHandleField != null)
                    {
                        var handleValue = serviceHandleField.GetValue(this);
                        _serviceHandle = handleValue is IntPtr ptr ? ptr : IntPtr.Zero;
                        _logger?.Info($"Service handle obtained: 0x{_serviceHandle.ToInt64():X}");
                    }
                    else
                    {
                        _logger?.Error("Could not find service status handle field via reflection!");

                        // Log all available fields for debugging
                        var allFields = typeof(ServiceBase).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                        _logger?.Info($"Available private fields in ServiceBase: {string.Join(", ", allFields.Select(f => f.Name))}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Failed to get service handle: {ex.Message}", ex);
                }

                // Ensure working directory is valid
                _serviceHelper.EnsureValidWorkingDirectory(options, _logger);

                _serviceName = options.ServiceName;
                _recoveryActionEnabled = options.HeartbeatInterval > 0 && options.MaxFailedChecks > 0 && options.RecoveryAction != RecoveryAction.None;
                _maxRestartAttempts = options.MaxRestartAttempts;

                // Request timeout for startup to accommodate slow process
                if (_options.StartTimeout > 20) // Use a lower threshold to be safe
                {
                    _serviceHelper.RequestAdditionalTime(this, (_options.StartTimeout + 10) * 1000, _logger);
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
            Directory.CreateDirectory(attemptsDir); // ensures folder exists

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
        /// Reads the current restart attempts count from the persistent file storage.
        /// </summary>
        /// <remarks>
        /// This method ensures that the restart attempts counter is always retrieved from disk,
        /// allowing the value to persist across service restarts.  
        /// <para>
        /// If the file is missing, contains invalid data, or an error occurs during reading,
        /// the counter is reset to <c>0</c> and the file is updated accordingly.
        /// </para>
        /// <para>
        /// Use this method at service startup (e.g., in <see cref="OnStart"/>) to ensure
        /// the restart attempts value reflects the most recent persisted state.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The number of recorded restart attempts, or <c>0</c> if the counter was reset.
        /// </returns>
        private int GetRestartAttempts()
        {
            try
            {
                if (File.Exists(_restartAttemptsFile))
                {
                    var content = File.ReadAllText(_restartAttemptsFile).Trim();
                    if (int.TryParse(content, out var attempts) && attempts >= 0)
                        return attempts;

                    File.WriteAllText(_restartAttemptsFile, "0");
                    _logger.Warning("Corrupt or invalid content found in restart attempts file. Resetting counter to 0.");
                    return 0;
                }
                else
                {
                    File.WriteAllText(_restartAttemptsFile, "0");
                    _logger.Warning("Restart attempts file not found. Initializing counter to 0.");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error reading restart attempts file: {ex.Message}. Resetting counter to 0.");
                return 0;
            }
        }

        /// <summary>
        /// Saves the current number of restart attempts to the persistent attempts file.
        /// Does nothing if the attempts file path is null or empty.
        /// </summary>
        /// <param name="attempts">The restart attempts count to save.</param>
        private void SaveRestartAttempts(int attempts)
        {
            if (!string.IsNullOrEmpty(_restartAttemptsFile))
            {
                File.WriteAllText(_restartAttemptsFile, attempts.ToString());
            }
        }

        /// <summary>
        /// Resets the restart attempts counter if the last recorded restart attempt
        /// occurred more than a calculated threshold ago, treating the current start as a fresh session.
        /// </summary>
        /// <param name="options">The service startup options containing heartbeat, recovery, and pre-launch settings.</param>
        /// <remarks>
        /// This method helps prevent the service from getting stuck in an infinite restart loop
        /// by only resetting the restart attempts counter when sufficient time has elapsed
        /// since the last attempt. The threshold is calculated based on the configured
        /// heartbeat interval, maximum allowed failed health checks, and an added buffer
        /// to accommodate timing variations and any configured pre-launch script timeout.
        /// <para>
        /// Rapid restarts occurring within this threshold will continue incrementing the restart attempts,
        /// allowing the service to eventually stop restarting after reaching the max allowed attempts.
        /// </para>
        /// <para>
        /// Use this method in <see cref="OnStart"/> to ensure that restart attempts are only reset
        /// when the service truly restarts after a stable downtime.
        /// </para>
        /// </remarks>
        private void ConditionalResetRestartAttempts(StartOptions options)
        {
            double resetThresholdMinutes = ((options.HeartbeatInterval * options.MaxFailedChecks) + 30) / 60.0; // adding 30 seconds buffer

            if (_preLaunchEnabled)
            {
                resetThresholdMinutes += options.PreLaunchTimeout / 60.0; // Convert seconds to minutes
            }

            var lastWrite = File.Exists(_restartAttemptsFile)
                ? File.GetLastWriteTimeUtc(_restartAttemptsFile)
                : DateTime.MinValue;

            if ((DateTime.UtcNow - lastWrite).TotalMinutes > resetThresholdMinutes)
            {
                _logger.Info("Resetting restart attempts counter due to sufficient downtime since last attempt.");
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

            var effectiveTimeout = Math.Max(options.PreLaunchTimeout, AppConfig.MinPreLaunchTimeoutSeconds) * 1000;

            var fireAndForget = effectiveTimeout == 0;

            if (fireAndForget)
            {
                try
                {
                    var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.PreLaunchEnvironmentVariables ?? Enumerable.Empty<EnvironmentVariable>().ToList());

                    foreach (var kvp in expandedEnv)
                    {
                        LogUnexpandedPlaceholders(kvp.Value ?? string.Empty, $"[Pre-Launch] Environment Variable '{kvp.Key}'");
                    }

                    var args = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.PreLaunchExecutableArgs ?? string.Empty, expandedEnv);

                    LogUnexpandedPlaceholders(args, "[Pre-Launch] Arguments");

                    var workingDir = string.IsNullOrWhiteSpace(options.PreLaunchWorkingDirectory)
                                    ? options.WorkingDirectory
                                    : options.PreLaunchWorkingDirectory;

                    var psi = new ProcessStartInfo
                    {
                        FileName = options.PreLaunchExecutablePath,
                        Arguments = args,
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    // Apply environment variables
                    foreach (var envVar in expandedEnv)
                    {
                        psi.Environment[envVar.Key] = envVar.Value ?? string.Empty;
                    }

                    _logger?.Info($"Running pre-launch program (fire-and-forget): {psi.FileName}");
                    _logger?.Info("Pre-launch stdout/stderr redirection and retries are ignored in fire-and-forget mode.");

                    // Fire-and-forget: start the process without waiting
                    var process = Process.Start(psi);

                    // Track the process so we can kill it if the Service stops
                    if (process != null)
                    {
                        lock (_trackedHooks)
                        {
                            _trackedHooks.Add(new Hook { OperationName = "pre-launch", Process = process });
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Failed to launch fire-and-forget pre-launch process: {ex.Message}");
                    return options.PreLaunchIgnoreFailure; // Use the user's preference for failure
                }
            }
            else
            {
                int attempt = 0;
                do
                {
                    attempt++;
                    _logger?.Info($"Starting pre-launch process (attempt {attempt}/{options.PreLaunchRetryAttempts + 1})...");

                    try
                    {
                        var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.PreLaunchEnvironmentVariables ?? Enumerable.Empty<EnvironmentVariable>().ToList());

                        foreach (var kvp in expandedEnv)
                        {
                            LogUnexpandedPlaceholders(kvp.Value ?? string.Empty, $"[Pre-Launch] Environment Variable '{kvp.Key}'");
                        }

                        var args = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.PreLaunchExecutableArgs ?? string.Empty, expandedEnv);

                        LogUnexpandedPlaceholders(args, "[Pre-Launch] Arguments");

                        var psi = new ProcessStartInfo
                        {
                            FileName = options.PreLaunchExecutablePath,
                            Arguments = args,
                            WorkingDirectory = string.IsNullOrWhiteSpace(options.PreLaunchWorkingDirectory)
                                ? options.WorkingDirectory
                                : options.PreLaunchWorkingDirectory,
                            UseShellExecute = false,
                            RedirectStandardOutput = !string.IsNullOrWhiteSpace(options.PreLaunchStdOutPath),
                            RedirectStandardError = !string.IsNullOrWhiteSpace(options.PreLaunchStdErrPath),
                            CreateNoWindow = true
                        };

                        if (psi.RedirectStandardOutput)
                        {
                            psi.StandardOutputEncoding = Encoding.UTF8;
                        }

                        if (psi.RedirectStandardError)
                        {
                            psi.StandardErrorEncoding = Encoding.UTF8;
                        }

                        // Apply environment variables
                        foreach (var envVar in expandedEnv)
                        {
                            psi.Environment[envVar.Key] = envVar.Value ?? string.Empty;
                        }

                        // Ensure UTF-8 encoding and buffered mode for python
                        EnsurePythonUTF8EncodingAndBufferedMode(psi);
                        EnsureJavaUTF8Encoding(psi);

                        using (var process = new Process { StartInfo = psi })
                        {
                            var stdoutBuffer = new StringBuilder();
                            var stderrBuffer = new StringBuilder();

                            if (psi.RedirectStandardOutput)
                            {
                                process.OutputDataReceived += (_, e) =>
                                {
                                    if (e.Data != null)
                                        stdoutBuffer.AppendLine(e.Data);
                                };
                            }
                            if (psi.RedirectStandardError)
                            {
                                process.ErrorDataReceived += (_, e) =>
                                {
                                    if (e.Data != null)
                                        stderrBuffer.AppendLine(e.Data);
                                };
                            }

                            process.Start();

                            if (psi.RedirectStandardOutput) process.BeginOutputReadLine();
                            if (psi.RedirectStandardError) process.BeginErrorReadLine();

                            // Wait for pre-lauch process to exit with timeout
                            WaitForProcessWithScmHeartbeat(
                                process,
                                effectiveTimeout,
                                WaitChunkMs,
                                "Pre-launch");

                            // Ensure all async reads are finished
                            process.WaitForExit();

                            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // UTF-8 without BOM

                            // Save logs if paths are set
                            if (!string.IsNullOrWhiteSpace(options.PreLaunchStdOutPath))
                            {
                                File.AppendAllText(options.PreLaunchStdOutPath, stdoutBuffer.ToString(), encoding);
                            }
                            if (!string.IsNullOrWhiteSpace(options.PreLaunchStdErrPath))
                            {
                                File.AppendAllText(options.PreLaunchStdErrPath, stderrBuffer.ToString(), encoding);
                            }

                            if (process.ExitCode == 0)
                            {
                                _logger?.Info("Pre-launch process completed successfully.");
                                return true;
                            }
                            else
                            {
                                _logger?.Error($"Pre-launch process exited with code {process.ExitCode}.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Pre-launch process attempt {attempt} failed: {ex.Message}", ex);
                    }

                } while (attempt <= options.PreLaunchRetryAttempts);

                _logger?.Error("Pre-launch process failed after all retry attempts.");

                if (options.PreLaunchIgnoreFailure)
                {
                    _logger?.Warning("Ignoring pre-launch failure and continuing service start.");
                    return true;
                }
            }

            return false; // stop service start
        }

        /// <summary>
        /// Waits for a process to exit while periodically sending "Wait Hints" to the Windows Service Control Manager (SCM).
        /// </summary>
        /// <param name="process">The external process to monitor.</param>
        /// <param name="effectiveTimeoutMs">The maximum total time (in milliseconds) allowed before the process is forcibly terminated.</param>
        /// <param name="waitChunkMs">The interval (in milliseconds) at which to report progress to the SCM and logs.</param>
        /// <param name="operationName">A descriptive name for the process, used for logging and exception messages.</param>
        /// <exception cref="TimeoutException">Thrown when the process execution time exceeds <paramref name="effectiveTimeoutMs"/>.</exception>
        /// <remarks>
        /// This method uses a "heartbeat" pattern. It is recommended that <paramref name="waitChunkMs"/> 
        /// be significantly shorter than the 15-second SCM wait hint to ensure the service remains responsive.
        /// </remarks>
        private void WaitForProcessWithScmHeartbeat(
            Process process,
            int effectiveTimeoutMs,
            int waitChunkMs,
            string operationName)
        {
            int elapsed = 0;

            while (!process.WaitForExit(waitChunkMs))
            {
                elapsed += waitChunkMs;

                // Keep SCM informed: Request 15s of additional time every chunk
                _serviceHelper?.RequestAdditionalTime(this, ScmAdditionalTime, null);

                //_logger?.Info($"{operationName} process still running... {elapsed / 1000}s elapsed.");

                if (elapsed >= effectiveTimeoutMs)
                {
                    try
                    {
                        Helpers.ProcessHelper.KillProcessTree(process);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warning(
                            $"Failed to kill {operationName} process: {ex.Message}");
                    }

                    throw new System.TimeoutException(
                        $"{operationName} process timed out after {effectiveTimeoutMs / 1000} seconds.");
                }
            }

            // Ensure async stdout/stderr drains to avoid data loss in logs
            process.WaitForExit();
        }

        /// <summary>
        /// Exposes the protected <see cref="OnStart(string[])"/> method for testing purposes.
        /// Starts the service with the specified command-line arguments.
        /// </summary>
        /// <param name="args">The command-line arguments to pass to the service on start.</param>
        public void StartForTest(string[] args)
        {
            OnStart(args);
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
                    options.MaxRotations
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
            _ = NativeMethods.AllocConsole(); // inherited
            _ = NativeMethods.SetConsoleCtrlHandler(null, false); // inherited
            _ = NativeMethods.SetConsoleOutputCP(NativeMethods.CP_UTF8);

            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(environmentVariables);

            foreach (var kvp in expandedEnv)
            {
                LogUnexpandedPlaceholders(kvp.Value ?? string.Empty, $"Environment Variable '{kvp.Key}'");
            }

            _realExePath = realExePath;
            _realArgs = EnvironmentVariableHelper.ExpandEnvironmentVariables(realArgs, expandedEnv);
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
            EnsurePythonUTF8EncodingAndBufferedMode(psi);
            EnsureJavaUTF8Encoding(psi);

            _childProcess = _processFactory.Create(psi, _logger);

            // Enable events and attach output/error handlers
            _childProcess.EnableRaisingEvents = true;
            _childProcess.OutputDataReceived += OnOutputDataReceived;
            _childProcess.ErrorDataReceived += OnErrorDataReceived;
            _childProcess.Exited += OnProcessExited;

            // Start the process
            try
            {
                _childProcess.Start();
                _logger?.Info($"Started child process with PID: {_childProcess.Id}");
            }
            finally
            {
                _ = NativeMethods.FreeConsole();
                _ = NativeMethods.SetConsoleCtrlHandler(null, true);
            }

            // Persist PID and PreviousStopTimeout
            InsertPid(_childProcess.Id, true);

            // Begin async reading of output and error streams
            _childProcess.BeginOutputReadLine();
            _childProcess.BeginErrorReadLine();

            // Fire and forget the post-launch script when process confirmed running
            var cts = new CancellationTokenSource();
            _cancellationSource = cts;

            Task.Run(async () =>
            {
                try
                {
                    if (await _childProcess.WaitUntilHealthyAsync(TimeSpan.FromSeconds(_options.StartTimeout), cts.Token))
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
        /// Ensures that Python processes use UTF-8 encoding for standard I/O and operate in unbuffered mode.
        /// </summary>
        /// <param name="psi">The <see cref="ProcessStartInfo"/> used to start the Python process.</param>
        /// <remarks>
        /// This method detects Python executables or scripts and configures the following environment variables:
        /// <list type="bullet">
        /// <item><description><c>PYTHONLEGACYWINDOWSSTDIO=0</c> - Enables wide-character I/O APIs.</description></item>
        /// <item><description><c>PYTHONIOENCODING=utf-8</c> - Forces UTF-8 for <c>stdout</c> and <c>stderr</c>.</description></item>
        /// <item><description><c>PYTHONUTF8=1</c> - Enables UTF-8 mode globally (Python 3.7+).</description></item>
        /// <item><description><c>PYTHONUNBUFFERED=1</c> - Disables I/O buffering to ensure real-time output.</description></item>
        /// </list>
        /// </remarks>
        private void EnsurePythonUTF8EncodingAndBufferedMode(ProcessStartInfo psi)
        {
            var fileName = psi.FileName != null ? psi.FileName.ToLowerInvariant() : string.Empty;
            var args = psi.Arguments != null ? psi.Arguments.ToLowerInvariant() : string.Empty;

            if (fileName.Contains("python") || args.Contains(".py"))
            {
                psi.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "0";
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";
                psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
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
                var args = EnvironmentVariableHelper.ExpandEnvironmentVariables(
                    _options.PostLaunchExecutableArgs ?? string.Empty,
                    new Dictionary<string, string>()
                );

                var workingDir = string.IsNullOrWhiteSpace(_options.PostLaunchWorkingDirectory)
                    ? Path.GetDirectoryName(_options.PostLaunchExecutablePath)
                    : _options.PostLaunchWorkingDirectory;

                var psi = new ProcessStartInfo
                {
                    FileName = _options.PostLaunchExecutablePath,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger?.Info($"Running post-launch program: {psi.FileName}");

                // Fire-and-forget: start the process without disposing immediately
                var process = Process.Start(psi);

                // Track the process so we can kill it if the Service stops
                if (process != null)
                {
                    lock (_trackedHooks)
                    {
                        _trackedHooks.Add(new Hook { OperationName = "post-launch", Process = process });
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
                var args = EnvironmentVariableHelper.ExpandEnvironmentVariables(
                    _options.FailureProgramArgs ?? string.Empty,
                    new Dictionary<string, string>()
                );

                var workingDir = string.IsNullOrWhiteSpace(_options.FailureProgramWorkingDirectory)
                    ? Path.GetDirectoryName(_options.FailureProgramPath)
                    : _options.FailureProgramWorkingDirectory;

                var psi = new ProcessStartInfo
                {
                    FileName = _options.FailureProgramPath,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger?.Info($"Running failure program: {psi.FileName}");

                // Fire-and-forget: start the process without disposing immediately
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to run failure program: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Logs a warning for any unexpanded environment variable placeholders found in the given string.
        /// Placeholders are identified as text surrounded by '%' signs (e.g., %VAR_NAME%).
        /// </summary>
        /// <param name="input">The string to inspect for unexpanded environment variable placeholders.</param>
        /// <param name="context">
        /// A descriptive name of the context where the string is used, 
        /// e.g., "Executable Path", "Arguments", or "Working Directory".
        /// This helps in logging clear warning messages.
        /// </param>
        /// <remarks>
        /// This method does not throw exceptions and will safely ignore null or empty input strings.
        /// Warnings are logged via the service logger (_logger) if configured.
        /// </remarks>
        private void LogUnexpandedPlaceholders(string input, string context)
        {
            if (string.IsNullOrEmpty(input))
                return;

            int start = input.IndexOf('%');
            while (start >= 0)
            {
                int end = input.IndexOf('%', start + 1);
                if (end > start)
                {
                    string placeholder = input.Substring(start, end - start + 1);
                    if (!string.IsNullOrEmpty(placeholder))
                    {
                        _logger?.Warning($"Unexpanded environment variable {placeholder} in {context}");
                    }
                    start = input.IndexOf('%', end + 1);
                }
                else
                {
                    break;
                }
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
        private void InsertPid(int? pid, bool setPreviousStopTimeout)
        {
            if (string.IsNullOrWhiteSpace(_serviceName))
                return;

            var serviceDto = _serviceRepository
                .GetByNameAsync(_serviceName)
                .ConfigureAwait(false).GetAwaiter().GetResult();

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

                _ = _serviceRepository
                    .UpdateAsync(serviceDto)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Resets PID to null in database.
        /// </summary>
        private void ResetPid()
        {
            InsertPid(null, false);
        }

        /// <summary>
        /// Handles the event when the monitored child process exits.
        /// Logs the exit code and whether the process ended successfully or with an error.
        /// If recovery is disabled and the process exits with a non-zero code, stops the service.
        /// If recovery is disabled and the process exits successfully, the service continues running
        /// (unless configured otherwise).
        /// If recovery is enabled, logs that recovery/restart logic will be applied.
        /// If the process fails to start and recovery actions are disabled, the configured failure program (if any)
        /// will be executed. When recovery actions are enabled, the failure program will only be executed if all 
        /// retries are exhausted.
        /// </summary>
        private void OnProcessExited(object sender, EventArgs e)
        {
            lock (_healthCheckLock)
            {
                try
                {
                    // reset PID
                    ResetPid();

                    var code = _childProcess.ExitCode;
                    if (code == 0)
                    {
                        _logger.Info("Child process exited successfully.");
                    }
                    else
                    {
                        _logger.Warning($"Child process exited with code {code}.");
                    }

                    if (!_recoveryActionEnabled)
                    {
                        if (code != 0)
                        {
                            _logger.Error("Recovery disabled and child process failed. Stopping service.");

                            RunFailureProgram();

                            Stop();
                        }
                        else
                        {
                            _logger.Info("Recovery disabled but child process exited successfully. Service continues.");
                        }
                    }
                    else
                    {
                        _logger.Info("Recovery enabled, child process exited. Restart logic expected.");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warning($"[Exited] Failed to get exit code: {ex.Message}");

                    if (!_recoveryActionEnabled)
                    {
                        _logger?.Error("Recovery disabled and exit code unknown. Stopping service.");
                        Stop();
                    }
                }
            }
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
                _logger?.Warning($"Failed to set priority: {ex.Message}");
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

            if (options.HeartbeatInterval > 0 && options.MaxFailedChecks > 0 && options.RecoveryAction != RecoveryAction.None)
            {
                _healthCheckTimer = _timerFactory.Create(_heartbeatIntervalSeconds * 1000);
                _healthCheckTimer.Elapsed += CheckHealth;
                _healthCheckTimer.AutoReset = true;
                _healthCheckTimer.Start();

                _logger?.Info("Health monitoring started.");
            }
        }

        /// <summary>
        /// Handles the periodic health check triggered by the timer.
        /// Compares the last received heartbeat timestamp with the current time,
        /// and performs the configured recovery action if the heartbeat is missed.
        /// </summary>
        /// <param name="sender">The timer object that raised the event.</param>
        /// <param name="e">Elapsed event data.</param>
        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            lock (_healthCheckLock)
            {
                if (_isRecovering)
                    return;

                try
                {
                    if (_childProcess == null || _childProcess.HasExited)
                    {
                        _failedChecks++;

                        _logger?.Warning(
                            $"Health check failed ({_failedChecks}/{_maxFailedChecks}). Child process is not running."
                        );

                        if (_failedChecks >= _maxFailedChecks)
                        {
                            var restartAttempts = GetRestartAttempts();

                            if (restartAttempts >= _maxRestartAttempts)
                            {
                                _logger?.Error(
                                    $"Maximum restart attempts reached ({_maxRestartAttempts}). Recovery actions stopped."
                                );

                                // No more retries -> reset counter so next session starts fresh
                                SaveRestartAttempts(0);

                                RunFailureProgram();

                                Stop();

                                return;
                            }

                            restartAttempts++;
                            SaveRestartAttempts(restartAttempts);
                            _failedChecks = 0;
                            _isRecovering = true;

                            try
                            {
                                _logger?.Warning(
                                    $"Performing recovery action ({restartAttempts}/{_maxRestartAttempts})."
                                );

                                switch (_recoveryAction)
                                {
                                    case RecoveryAction.None:
                                        _logger?.Info("Recovery action is set to 'None'. No action taken.");
                                        break;

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
                                            (_options?.StopTimeout ?? AppConfig.DefaultStopTimeout) * 1000
                                        );
                                        break;

                                    case RecoveryAction.RestartComputer:
                                        _serviceHelper.RestartComputer(_logger);
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.Error($"Error during recovery action: {ex}");
                            }
                            finally
                            {
                                _isRecovering = false;
                            }
                        }
                    }
                    else
                    {
                        if (_failedChecks > 0)
                        {
                            _logger?.Info("Child process is healthy again. Resetting failure count and restart attempts.");
                            _failedChecks = 0;
                            SaveRestartAttempts(0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Unexpected error in health check: {ex}");
                }
            }
        }

        /// <summary>
        /// Called when the service receives a Stop command from the Service Control Manager (SCM).
        /// Triggers the standardized teardown sequence.
        /// </summary>
        protected override void OnStop()
        {
            ExecuteTeardown(TeardownReason.Stop);
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
                _logger?.Info("Pre-Shutdown received. Starting orchestrated teardown...");

                if (_serviceHandle == IntPtr.Zero)
                {
                    _logger?.Error("Service handle is null!");
                    return;
                }

                // 1. Immediately tell SCM we are transitioning to a stop state and need a 30s window.
                // This moves the service into the STOP_PENDING state in the eyes of the OS.
                UpdateServiceStatus(SERVICE_STOP_PENDING, 30000);

                Task<bool> stopTask = Task.Run(() => ExecuteTeardown(TeardownReason.PreShutdown));

                // 2. Wait in 2-second pulses. 
                // We increment the checkpoint each pulse to prove to the SCM that we haven't hung.
                int interval = 2000;
                while (!stopTask.Wait(interval))
                {
                    _checkPoint++;
                    UpdateServiceStatus(SERVICE_STOP_PENDING, 30000);
                }

                // 3. Handle task completion results
                if (stopTask.IsFaulted)
                {
                    _logger?.Error($"Teardown task failed: {stopTask.Exception?.InnerException?.Message}");
                }

                // 4. Final Signal: Inform the SCM that the service has successfully reached the STOPPED state.
                _logger?.Info("Pre-Shutdown handling complete. Setting SERVICE_STOPPED.");
                UpdateServiceStatus(SERVICE_STOPPED, 0);

                return;
            }

            base.OnCustomCommand(command);
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
        /// Orchestrates the shared teardown logic for both service stops and system shutdowns.
        /// Handles cancellation, event invocation, and resource cleanup.
        /// </summary>
        /// <param name="reason">The context of the teardown (e.g., "Stop" or "Shutdown") used for logging purposes.</param>
        private bool ExecuteTeardown(TeardownReason reason)
        {
            lock (_teardownLock)
            {
                if (_disposed) return true;

                try
                {
                    _logger?.Info($"Executing teardown for reason: {reason}");
                    _cancellationSource?.Cancel();
                    OnStoppedForTest?.Invoke();

                    Cleanup();

                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Teardown error during {reason}: {ex.Message}", ex);
                    return false;
                }
                finally
                {
                    _disposed = true;
                    _cancellationSource?.Dispose();
                    _cancellationSource = null;
                }
            }
        }

        /// <summary>
        /// Orchestrates the full cleanup sequence: unhooking events, running the pre-stop hook,
        /// terminating the child process tree, and running the post-stop hook.
        /// </summary>
        private void Cleanup()
        {
            if (_disposed) return;

            // reset PID
            ResetPid();

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
                    SafeKillProcess(_childProcess, _options.StopTimeout * 1000);

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
                            Debug.WriteLine($"Cleanup failed: {ex.Message}");
                        }
                        finally
                        {
                            try { hook.Process.Dispose(); } catch { /* ignore */ } // Important: Release the process handles!
                        }
                    }
                    _trackedHooks.Clear();
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
                    _logger?.Warning($"Failed to dispose output writers: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to kill child process: {ex.Message}");
            }
            finally
            {
                _healthCheckTimer?.Dispose();
                _healthCheckTimer = null;

                if (_childProcess != null)
                {
                    _childProcess.Dispose();
                    _childProcess = null;
                }

                //GC.SuppressFinalize(this);
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
                return true; // proceed with service stop
            }

            var effectiveTimeout = options.PreStopTimeout * 1000;
            var fireAndForget = effectiveTimeout == 0;

            _logger?.Info("Starting pre-stop process...");

            try
            {
                var args = EnvironmentVariableHelper.ExpandEnvironmentVariables(
                    options.PreStopExecutableArgs ?? string.Empty,
                    new Dictionary<string, string>());

                LogUnexpandedPlaceholders(args, "[Pre-Stop] Arguments");

                var psi = new ProcessStartInfo
                {
                    FileName = options.PreStopExecutablePath,
                    Arguments = args,
                    WorkingDirectory = string.IsNullOrWhiteSpace(options.PreStopWorkingDirectory)
                        ? options.WorkingDirectory
                        : options.PreStopWorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (fireAndForget)
                {
                    // Fire-and-forget: start the process without waiting
                    Process.Start(psi);
                    _logger?.Info("Pre-stop configured as fire-and-forget. Continuing service stop immediately.");
                    return true;
                }

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();

                    // Wait for pre-stop process to exit with timeout
                    WaitForProcessWithScmHeartbeat(
                        process,
                        effectiveTimeout,
                        WaitChunkMs,
                        "Pre-stop");

                    if (process.ExitCode == 0)
                    {
                        _logger?.Info("Pre-stop process completed successfully.");
                        return true;
                    }
                    else
                    {
                        _logger?.Error($"Pre-stop process exited with code {process.ExitCode}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Pre-stop process failed: {ex.Message}", ex);
            }

            if (!options.PreStopLogAsError)
            {
                _logger?.Warning("Ignoring pre-stop failure and continuing service stop.");
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
            if (process == null || process.HasExited) return;

            try
            {
                _logger?.Info($"Starting stop sequence for {process.Format()} (Timeout: {timeoutMs}ms)");

                // 1. Run the blocking process.Stop call on a background thread
                // This ensures we call it exactly once with the full timeout.
                Task<bool?> stopTask = Task.Run(() =>
                {
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
                        _logger?.Warning($"SafeKillProcess error while getting process PID and StartTime: {ex.Message}");
                    }

                    // 1. Stop the main process
                    var result = process.Stop(timeoutMs);

                    // 2. Immediately start cleaning up descendants 
                    // This now happens while the main loop is still "pulsing" the SCM
                    // This will walk the entire process tree using the configured stop timeout
                    process.StopDescendants(parentPid, parentStartTime, timeoutMs);

                    return result;
                });

                // 2. Wait for the task to complete in 5-second pulses
                // This prevents ContextSwitchDeadlock and keeps the SCM happy.
                while (!stopTask.Wait(5000))
                {
                    // Request 15s of "Wait Hint" every 5s pulse
                    _serviceHelper.RequestAdditionalTime(this, ScmAdditionalTime, null);
                }

                // 3. Retrieve the result from the finished task
                var res = stopTask.IsCompleted ? stopTask.Result : null;

                // 4. Handle Logging
                HandleStopResult(process, res);
            }
            catch (Exception ex)
            {
                _logger?.Warning("SafeKillProcess error: " + ex.Message);
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
                var args = EnvironmentVariableHelper.ExpandEnvironmentVariables(
                    _options.PostStopExecutableArgs ?? string.Empty,
                    new Dictionary<string, string>()
                );

                var workingDir = string.IsNullOrWhiteSpace(_options.PostStopWorkingDirectory)
                    ? Path.GetDirectoryName(_options.PostStopExecutablePath)
                    : _options.PostStopWorkingDirectory;

                var psi = new ProcessStartInfo
                {
                    FileName = _options.PostStopExecutablePath,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger?.Info($"Running post-stop program: {psi.FileName}");

                // Fire-and-forget: start the process without waiting
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to run post-stop program: {ex.Message}", ex);
            }
        }

    }
}
