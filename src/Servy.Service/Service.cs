using Servy.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Timers;

namespace Servy.Service
{
    public partial class Service : ServiceBase
    {
        private readonly IServiceHelper _serviceHelper;
        private readonly ILogger _logger;
        private readonly IStreamWriterFactory _streamWriterFactory;
        private readonly ITimerFactory _timerFactory;
        private readonly IProcessFactory _processFactory;
        private string _serviceName;
        private string _realExePath;
        private string _realArgs;
        private string _workingDir;
        private IntPtr _jobHandle = IntPtr.Zero;
        private IProcessWrapper _childProcess;
        private IStreamWriter _stdoutWriter;
        private IStreamWriter _stderrWriter;
        private ITimer _healthCheckTimer;
        private int _heartbeatIntervalSeconds;
        private int _maxFailedChecks;
        private int _failedChecks = 0;
        private RecoveryAction _recoveryAction;
        private bool _disposed = false; // Tracks whether Dispose has been called
        private readonly object _healthCheckLock = new object();
        private bool _isRecovering = false;
        private int _maxRestartAttempts = 3; // Maximum number of restart attempts
        private int _restartAttempts = 0;

        public event Action OnStoppedForTest;

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class
        /// using the default <see cref="ServiceHelper"/> implementation.
        /// </summary>
        public Service() : this(
            new ServiceHelper(new CommandLineProvider()), new EventLogLogger("Servy"),
            new StreamWriterFactory(),
            new TimerFactory(),
            new ProcessFactory()
            ) // or your default implementation
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
        public Service(
            IServiceHelper serviceHelper,
            ILogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory)
        {
            ServiceName = "Servy";

            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamWriterFactory = streamWriterFactory ?? throw new ArgumentNullException(nameof(streamWriterFactory));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        }

        private void SetProcessPriority(ProcessPriorityClass priority)
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

        private void HandleLogWriters(StartOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.StdOutPath) && Helper.IsValidPath(options.StdOutPath))
            {
                _stdoutWriter = _streamWriterFactory.Create(options.StdOutPath, options.RotationSizeInBytes);
            }
            else if (!string.IsNullOrWhiteSpace(options.StdOutPath))
            {
                _logger?.Error($"Invalid stdout file path: {options.StdOutPath}");
            }

            if (!string.IsNullOrWhiteSpace(options.StdErrPath) && Helper.IsValidPath(options.StdErrPath))
            {
                _stderrWriter = _streamWriterFactory.Create(options.StdErrPath, options.RotationSizeInBytes);
            }
            else if (!string.IsNullOrWhiteSpace(options.StdErrPath))
            {
                _logger?.Error($"Invalid stderr file path: {options.StdErrPath}");
            }
        }

        private void StartMonitoredProcess(StartOptions options)
        {
            StartProcess(options.ExecutablePath, options.ExecutableArgs, options.WorkingDirectory);
            SetProcessPriority(options.Priority);
        }

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
        /// Called when the service is started.
        /// Parses command line arguments, sets up logging,
        /// creates the job object, starts the child process,
        /// assigns it to the job object, and hooks up event handlers.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the service.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                var options = _serviceHelper.InitializeStartup(_logger);
                if (options == null)
                {
                    Stop();
                    return;
                }

                _serviceHelper.EnsureValidWorkingDirectory(options, _logger);
                _serviceName = options.ServiceName;
                _maxRestartAttempts = options.MaxRestartAttempts;

                HandleLogWriters(options);
                StartMonitoredProcess(options);
                SetupHealthMonitoring(options);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Exception in OnStart: {ex.Message}", ex);
                Stop();
            }
        }

        public void StartForTest(string[] args)
        {
            OnStart(args);
        }

        /// <summary>
        /// Starts the child process and assigns it to a Windows Job Object to ensure proper cleanup.
        /// Redirects standard output and error streams, and sets up event handlers for output, error, and exit events.
        /// </summary>
        /// <param name="realExePath">The full path to the executable to run.</param>
        /// <param name="realArgs">The arguments to pass to the executable.</param>
        /// <param name="workingDir">The working directory for the process.</param>
        private void StartProcess(string realExePath, string realArgs, string workingDir)
        {
            _realExePath = realExePath;
            _realArgs = realArgs;
            _workingDir = workingDir;

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

            _childProcess = _processFactory.Create(psi);

            // Enable events and attach output/error handlers
            _childProcess.EnableRaisingEvents = true;
            _childProcess.OutputDataReceived += OnOutputDataReceived;
            _childProcess.ErrorDataReceived += OnErrorDataReceived;
            _childProcess.Exited += OnProcessExited;

            // Create a Windows Job Object to manage the process and ensure it's terminated if the parent dies
            _jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (_jobHandle == IntPtr.Zero || _jobHandle == new IntPtr(-1))
            {
                _logger?.Error("Failed to create Job Object.");
            }
            else
            {
                // Configure the Job Object to automatically kill all processes associated with it on handle close
                var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = { LimitFlags = NativeMethods.LimitFlags.KillOnJobClose }
                };

                int length = Marshal.SizeOf(typeof(NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, extendedInfoPtr, false);
                    if (!NativeMethods.SetInformationJobObject(
                        _jobHandle,
                        NativeMethods.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                        extendedInfoPtr,
                        (uint)length))
                    {
                        _logger?.Error("Failed to set information on Job Object.");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }

            // Start the process
            _childProcess.Start();
            _logger?.Info($"Started child process with PID: {_childProcess.Id}");

            // Assign the process to the Job Object
            if (_jobHandle != IntPtr.Zero && _jobHandle != new IntPtr(-1))
            {
                if (!NativeMethods.AssignProcessToJobObject(_jobHandle, _childProcess.Handle))
                {
                    _logger?.Error("Failed to assign process to Job Object.");
                }
            }

            // Begin asynchronous reading of the output and error streams
            _childProcess.BeginOutputReadLine();
            _childProcess.BeginErrorReadLine();
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
                            $"Health check failed ({_failedChecks}/{_maxFailedChecks}). Child process has exited unexpectedly."
                         );

                        if (_failedChecks >= _maxFailedChecks)
                        {
                            if (_restartAttempts >= _maxRestartAttempts)
                            {
                                _logger?.Error(
                                    $"Max restart attempts ({_maxRestartAttempts}) reached. No further recovery actions will be taken."
                                );
                                return;
                            }

                            _restartAttempts++;
                            _isRecovering = true;
                            _failedChecks = 0;

                            switch (_recoveryAction)
                            {
                                case RecoveryAction.None:
                                    _isRecovering = false;
                                    break;

                                case RecoveryAction.RestartService:
                                    try
                                    {
                                        var exePath = Assembly.GetExecutingAssembly().Location;
                                        var dir = Path.GetDirectoryName(exePath);
                                        var restarter = Path.Combine(dir, "Servy.Restarter.exe");

                                        if (File.Exists(restarter))
                                        {
                                            Process.Start(new ProcessStartInfo
                                            {
                                                FileName = restarter,
                                                Arguments = _serviceName,
                                                CreateNoWindow = true,
                                                UseShellExecute = false
                                            });

                                            using (var controller = new ServiceController(_serviceName))
                                            {
                                                controller.Stop();
                                            }
                                        }
                                        else
                                        {
                                            _logger?.Error("Servy.Restarter.exe not found.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.Error($"Failed to launch restarter: {ex}");
                                    }
                                    break;

                                case RecoveryAction.RestartProcess:
                                    TryRestartChildProcess();
                                    _isRecovering = false;
                                    break;

                                case RecoveryAction.RestartComputer:
                                    try
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            FileName = "shutdown",
                                            Arguments = "/r /t 0 /f",
                                            CreateNoWindow = true,
                                            UseShellExecute = false
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.Error($"Failed to restart computer: {ex.Message}");
                                    }
                                    finally
                                    {
                                        _isRecovering = false;
                                    }
                                    break;
                            }
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
        /// Terminates all child processes in the job object (if one is active) by closing the job handle.
        /// This ensures no orphaned or zombie processes remain.
        /// </summary>
        private void TerminateChildProcesses()
        {
            // Closing the job handle will terminate all associated child processes automatically
            if (_jobHandle != IntPtr.Zero && _jobHandle != new IntPtr(-1))
            {
                NativeMethods.CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Attempts to restart the child process by:
        /// 1. Killing it if it's running.
        /// 2. Terminating the associated job object (which ensures all child processes are also killed).
        /// 3. Restarting the process with the original parameters.
        /// </summary>
        private void TryRestartChildProcess()
        {
            try
            {
                _logger?.Info("Restarting child process...");

                // Kill the process if it's still running
                if (_childProcess != null && !_childProcess.HasExited)
                {
                    _childProcess.Kill();
                    _childProcess.WaitForExit();
                }

                // Clean up the job object and any associated processes
                TerminateChildProcesses();

                // Start the process again
                StartProcess(_realExePath, _realArgs, _workingDir);

                _logger?.Info("Process restarted.");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to restart process: {ex.Message}");
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
                _logger?.Error($"[Error] {e.Data}");
            }
        }

        /// <summary>
        /// Called when the child process exits.
        /// Logs the exit code and whether the exit was successful.
        /// </summary>
        private void OnProcessExited(object sender, EventArgs e)
        {
            try
            {
                var code = _childProcess.ExitCode;
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

        /// <summary>
        /// Disposes the service, cleaning up resources.
        /// </summary>
        private void Cleanup()
        {
            if (_disposed)
                return;

            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;

            if (_childProcess != null)
            {
                // Unsubscribe event handlers to prevent memory leaks or callbacks after dispose
                _childProcess.OutputDataReceived -= OnOutputDataReceived;
                _childProcess.ErrorDataReceived -= OnErrorDataReceived;
                _childProcess.Exited -= OnProcessExited;
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
                SafeKillProcess(_childProcess);
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

                TerminateChildProcesses();

                GC.SuppressFinalize(this);
            }

            _disposed = true;
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
    }
}
