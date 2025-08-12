using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Service.CommandLine;
using Servy.Service.Logging;
using Servy.Service.ProcessManagement;
using Servy.Service.ServiceHelpers;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service
{
    public partial class Service : ServiceBase
    {
        #region Constants

        private const string WindowsServiceName = "Servy";

        #endregion

        #region Private Fields

        private readonly IServiceHelper _serviceHelper;
        private readonly ILogger _logger;
        private readonly IStreamWriterFactory _streamWriterFactory;
        private readonly ITimerFactory _timerFactory;
        private readonly IProcessFactory _processFactory;
        private readonly IPathValidator _pathValidator;
        private string? _serviceName;
        private string? _realExePath;
        private string? _realArgs;
        private string? _workingDir;
        private IProcessWrapper? _childProcess;
        private IStreamWriter? _stdoutWriter;
        private IStreamWriter? _stderrWriter;
        private ITimer? _healthCheckTimer;
        private int _heartbeatIntervalSeconds;
        private int _maxFailedChecks;
        private int _failedChecks = 0;
        private RecoveryAction _recoveryAction;
        private readonly object _healthCheckLock = new();
        private bool _isRecovering = false;
        private int _maxRestartAttempts = 3; // Maximum number of restart attempts
        private int _restartAttempts = 0;
        private List<EnvironmentVariable> _environmentVariables = new List<EnvironmentVariable>();
        private bool _disposed = false; // Tracks whether Dispose has been called

        #endregion

        /// <summary>
        /// Event invoked when the service stops, used for testing purposes.
        /// </summary>
        public event Action? OnStoppedForTest;

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class
        /// using the default <see cref="ServiceHelper"/> implementation,
        /// path validator and default factories for stream writer, timer, and process.
        /// </summary>
        public Service() : this(
            new ServiceHelper(new CommandLineProvider()),
            new EventLogLogger(WindowsServiceName),
            new StreamWriterFactory(),
            new TimerFactory(),
            new ProcessFactory(),
            new PathValidator()
          )
        {
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
            ServiceName = WindowsServiceName;

            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamWriterFactory = streamWriterFactory ?? throw new ArgumentNullException(nameof(streamWriterFactory));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
            _pathValidator = pathValidator;
        }

        /// <summary>
        /// Called when the Windows service is started.
        /// Initializes startup options, configures logging, validates working directories,
        /// runs the optional pre-launch process (if configured), and starts the monitored service process.
        /// Also sets up health monitoring for the service.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the service by the Service Control Manager.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                // Load and validate service startup options
                var options = _serviceHelper.InitializeStartup(_logger);
                if (options == null)
                {
                    Stop();
                    return;
                }

                // Ensure working directory is valid
                _serviceHelper.EnsureValidWorkingDirectory(options, _logger);

                _serviceName = options.ServiceName;
                _maxRestartAttempts = options.MaxRestartAttempts;

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
        /// </remarks>
        private bool StartPreLaunchProcess(StartOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.PreLaunchExecutablePath))
            {
                _logger?.Info("No pre-launch executable configured. Skipping.");
                return true; // proceed with service start
            }

            const int MinPreLaunchTimeoutSeconds = 5;
            int effectiveTimeout = Math.Max(options.PreLaunchTimeout, MinPreLaunchTimeoutSeconds) * 1000;

            int attempt = 0;
            do
            {
                attempt++;
                _logger?.Info($"Starting pre-launch process (attempt {attempt}/{options.PreLaunchRetryAttempts + 1})...");

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = options.PreLaunchExecutablePath,
                        Arguments = options.PreLaunchExecutableArgs ?? string.Empty,
                        WorkingDirectory = string.IsNullOrWhiteSpace(options.PreLaunchWorkingDirectory)
                            ? options.WorkingDirectory
                            : options.PreLaunchWorkingDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = !string.IsNullOrWhiteSpace(options.PreLaunchStdOutPath),
                        RedirectStandardError = !string.IsNullOrWhiteSpace(options.PreLaunchStdErrPath),
                        CreateNoWindow = true
                    };

                    // Apply environment variables
                    foreach (var envVar in options.PreLaunchEnvironmentVariables ?? Enumerable.Empty<EnvironmentVariable>())
                    {
                        if (!string.IsNullOrWhiteSpace(envVar?.Name))
                        {
                            startInfo.Environment[envVar.Name] = envVar.Value ?? string.Empty;
                        }
                    }

                    using (var process = new Process { StartInfo = startInfo })
                    {
                        StringBuilder stdoutBuffer = new();
                        StringBuilder stderrBuffer = new();

                        if (startInfo.RedirectStandardOutput)
                        {
                            process.OutputDataReceived += (_, e) =>
                            {
                                if (e.Data != null)
                                    stdoutBuffer.AppendLine(e.Data);
                            };
                        }
                        if (startInfo.RedirectStandardError)
                        {
                            process.ErrorDataReceived += (_, e) =>
                            {
                                if (e.Data != null)
                                    stderrBuffer.AppendLine(e.Data);
                            };
                        }

                        process.Start();

                        if (startInfo.RedirectStandardOutput) process.BeginOutputReadLine();
                        if (startInfo.RedirectStandardError) process.BeginErrorReadLine();

                        if (!process.WaitForExit(effectiveTimeout))
                        {
                            try { process.Kill(true); } catch { /* ignore */ }
                            throw new System.TimeoutException($"Pre-launch process timed out after {effectiveTimeout / 1000} seconds.");
                        }

                        // Ensure all async reads are finished
                        process.WaitForExit();

                        // Save logs if paths are set
                        if (!string.IsNullOrWhiteSpace(options.PreLaunchStdOutPath))
                        {
                            File.AppendAllText(options.PreLaunchStdOutPath, stdoutBuffer.ToString());
                        }
                        if (!string.IsNullOrWhiteSpace(options.PreLaunchStdErrPath))
                        {
                            File.AppendAllText(options.PreLaunchStdErrPath, stderrBuffer.ToString());
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

            return false; // stop service start
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
        /// Creates and assigns rotating stream writers for standard output and error
        /// based on the given <see cref="StartOptions"/>.
        /// Logs errors if paths are invalid.
        /// </summary>
        /// <param name="options">The start options containing stdout and stderr paths.</param>
        private void HandleLogWriters(StartOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.StdOutPath) && _pathValidator.IsValidPath(options.StdOutPath))
            {
                _stdoutWriter = _streamWriterFactory.Create(options.StdOutPath, options.RotationSizeInBytes);
            }
            else if (!string.IsNullOrWhiteSpace(options.StdOutPath))
            {
                _logger?.Error($"Invalid stdout file path: {options.StdOutPath}");
            }

            if (!string.IsNullOrWhiteSpace(options.StdErrPath) && _pathValidator.IsValidPath(options.StdErrPath))
            {
                _stderrWriter = _streamWriterFactory.Create(options.StdErrPath, options.RotationSizeInBytes);
            }
            else if (!string.IsNullOrWhiteSpace(options.StdErrPath))
            {
                _logger?.Error($"Invalid stderr file path: {options.StdErrPath}");
            }
        }

        /// <summary>
        /// Starts the monitored child process using the executable path, arguments, and working directory from the options.
        /// Also sets the process priority accordingly.
        /// </summary>
        /// <param name="options">The start options containing executable details and priority.</param>
        private void StartMonitoredProcess(StartOptions options)
        {
            StartProcess(options.ExecutablePath!, options.ExecutableArgs!, options.WorkingDirectory!, options.EnvironmentVariables);
            SetProcessPriority(options.Priority);
        }

        /// <summary>
        /// Starts the child process and assigns it to a Windows Job Object to ensure proper cleanup.
        /// Redirects standard output and error streams, and sets up event handlers for output, error, and exit events.
        /// </summary>
        /// <param name="realExePath">The full path to the executable to run.</param>
        /// <param name="realArgs">The arguments to pass to the executable.</param>
        /// <param name="workingDir">The working directory for the process.</param>
        /// <param name="environmentVariables">Environment variables.</param>
        private void StartProcess(string realExePath, string realArgs, string workingDir, List<EnvironmentVariable> environmentVariables)
        {
            _realExePath = realExePath;
            _realArgs = realArgs;
            _workingDir = workingDir;
            _environmentVariables = environmentVariables;

            // Configure the process start info
            var psi = new ProcessStartInfo
            {
                FileName = realExePath,
                Arguments = realArgs,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (environmentVariables != null)
            {
                foreach (var envVar in environmentVariables)
                {
                    psi.EnvironmentVariables[envVar.Name] = envVar.Value;
                }
            }

            _childProcess = _processFactory.Create(psi);

            // Enable events and attach output/error handlers
            _childProcess.EnableRaisingEvents = true;
            _childProcess.OutputDataReceived += OnOutputDataReceived;
            _childProcess.ErrorDataReceived += OnErrorDataReceived;
            _childProcess.Exited += OnProcessExited!;

            // Start the process
            _childProcess.Start();
            _logger?.Info($"Started child process with PID: {_childProcess.Id}");

            // Begin async reading of output and error streams
            _childProcess.BeginOutputReadLine();
            _childProcess.BeginErrorReadLine();
        }

        /// <summary>
        /// Handles redirected standard output from the child process.
        /// Writes output lines to the rotating stdout writer.
        /// </summary>
        private void OnOutputDataReceived(object? sender, DataReceivedEventArgs e)
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
        private void OnErrorDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _stderrWriter?.WriteLine(e.Data);
                _logger?.Error($"[Error] {e.Data}");
            }
        }

        /// <summary>
        /// Called when the child process exits.
        /// Logs the exit code and whether the exit was successful.
        /// </summary>
        private void OnProcessExited(object? sender, EventArgs e)
        {
            try
            {
                var code = _childProcess!.ExitCode;
                if (code == 0)
                {
                    _logger.Info("Child process exited successfully.");
                }
                else
                {
                    _logger.Warning($"Child process exited with code {code}.");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning($"[Exited] Failed to get exit code: {ex.Message}");
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
                _childProcess!.PriorityClass = priority;
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

            if (_heartbeatIntervalSeconds > 0 && _maxFailedChecks > 0 && _recoveryAction != RecoveryAction.None)
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
        private void CheckHealth(object? sender, ElapsedEventArgs? e)
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
                            $"Health check failed ({_failedChecks}/{_maxFailedChecks}). Child process has exited unexpectedly."
                         );

                        if (_failedChecks >= _maxFailedChecks)
                        {
                            if (_restartAttempts >= _maxRestartAttempts)
                            {
                                _logger?.Error(
                                    $"Max restart attempts ({_maxRestartAttempts}) reached. No further recovery actions will be taken."
                                );
                                _isRecovering = false;
                                return;
                            }

                            _restartAttempts++;
                            _isRecovering = true;
                            _failedChecks = 0;

                            switch (_recoveryAction)
                            {
                                case RecoveryAction.None:
                                    break;

                                case RecoveryAction.RestartService:
                                    _serviceHelper.RestartService(
                                        _logger!,
                                        _serviceName!
                                        );
                                    break;

                                case RecoveryAction.RestartProcess:
                                    _serviceHelper.RestartProcess(
                                         _childProcess!,
                                         StartProcess,
                                         _realExePath!,
                                         _realArgs!,
                                         _workingDir!,
                                         _environmentVariables,
                                         _logger!
                                     );
                                    break;

                                case RecoveryAction.RestartComputer:
                                    _serviceHelper.RestartComputer(_logger!);
                                    break;
                            }

                            // Only now reset the flag
                            _isRecovering = false;
                        }
                    }
                    else
                    {
                        if (_failedChecks > 0)
                        {
                            _logger?.Info("Child process is healthy again. Resetting failure count and restart attempts.");
                            _failedChecks = 0;
                            _restartAttempts = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error in health check: {ex}");
                }
            }
        }

        /// <summary>
        /// Called when the service stops.
        /// Unhooks event handlers, disposes output writers,
        /// kills the child process, and closes the job object handle.
        /// </summary>
        protected override void OnStop()
        {
            OnStoppedForTest?.Invoke();

            Cleanup();

            base.OnStop();

            _logger?.Info("Stopped child process.");
        }

        /// <summary>
        /// Disposes the service, cleaning up resources.
        /// </summary>
        private void Cleanup()
        {
            if (_disposed)
                return;

            if (_childProcess != null)
            {
                // Unsubscribe event handlers to prevent memory leaks or callbacks after dispose
                _childProcess.OutputDataReceived -= OnOutputDataReceived;
                _childProcess.ErrorDataReceived -= OnErrorDataReceived;
                _childProcess.Exited -= OnProcessExited!;
            }

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

            try
            {
                // Attempt to stop child process gracefully or kill forcibly
                SafeKillProcess(_childProcess!);
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

                GC.SuppressFinalize(this);
            }

            _disposed = true;
        }

        /// <summary>
        /// Attempts to gracefully stop the process by sending a close message to its main window.
        /// If that fails or the process has no main window, forcibly kills the process.
        /// Waits up to the specified timeout for the process to exit.
        /// </summary>
        /// <param name="process">Process to stop.</param>
        /// <param name="timeoutMs">Timeout in milliseconds to wait for exit.</param>
        private void SafeKillProcess(IProcessWrapper process, int timeoutMs = 5000)
        {
            try
            {
                if (process == null || process.HasExited) return;

                bool closedGracefully = false;

                // Only GUI processes have a main window to close
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    closedGracefully = process.CloseMainWindow();
                }

                if (!closedGracefully)
                {
                    // Either no GUI window or close failed — kill forcibly
                    _logger?.Warning("Graceful shutdown not supported. Forcing kill.");
                    process.Kill();
                }

                process.WaitForExit(timeoutMs);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"SafeKillProcess error: {ex.Message}");
            }
        }

    }
}
