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
        private const string RestartAttemptsFile = "restartAttempts.dat";

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
        private List<EnvironmentVariable> _environmentVariables = new List<EnvironmentVariable>();
        private bool _disposed = false; // Tracks whether Dispose has been called
        private bool _recoveryActionEnabled = false;
        private string? _restartAttemptsFile;
        private bool _preLaunchEnabled = false;

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
                _recoveryActionEnabled = options.HeartbeatInterval > 0 && options.MaxFailedChecks > 0 && options.RecoveryAction != RecoveryAction.None;
                _maxRestartAttempts = options.MaxRestartAttempts;

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
        /// located under the %ProgramData%\Servy directory.  
        /// The filename is unique per service based on its name to prevent conflicts  
        /// when multiple services are managed by Servy on the same machine.
        /// </summary>
        /// <param name="options">The service startup options containing the service name.</param>
        private void SetupAttemptsFile(StartOptions options)
        {
            string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Servy");
            Directory.CreateDirectory(dataFolder); // ensures folder exists

            string safeServiceName = MakeFilenameSafe(options.ServiceName!);
            _restartAttemptsFile = Path.Combine(dataFolder, $"{safeServiceName}_restartAttempts.dat");
        }

        /// <summary>
        /// Sanitizes a string to be safe for use as a filename by replacing
        /// all invalid filename characters with underscores ('_').
        /// </summary>
        /// <param name="name">The original string to sanitize.</param>
        /// <returns>A sanitized string safe for use as a filename.</returns>
        private string MakeFilenameSafe(string name)
        {
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
                    File.WriteAllText(_restartAttemptsFile!, "0");
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
        /// </remarks>
        private bool StartPreLaunchProcess(StartOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.PreLaunchExecutablePath))
            {
                _logger?.Info("No pre-launch executable configured. Skipping.");
                return true; // proceed with service start
            }

            _preLaunchEnabled = true;

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
                //_logger?.Error($"[Error] {e.Data}");
            }
        }

        /// <summary>
        /// Handles the event when the monitored child process exits.
        /// Logs the exit code and whether the process ended successfully or with an error.
        /// If recovery is disabled and the process exits with a non-zero code, stops the service.
        /// If recovery is disabled and the process exits successfully, the service continues running
        /// (unless configured otherwise).
        /// If recovery is enabled, logs that recovery/restart logic will be applied.
        /// </summary>
        private void OnProcessExited(object? sender, EventArgs e)
        {
            lock (_healthCheckLock)
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

                    if (!_recoveryActionEnabled)
                    {
                        if (code != 0)
                        {
                            _logger.Info("Recovery disabled and child process failed. Stopping service.");
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

                                // No more retries → reset counter so next session starts fresh
                                SaveRestartAttempts(0);
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
                                        _serviceHelper.RestartService(_logger!, _serviceName!);
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
