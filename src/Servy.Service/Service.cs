using Servy.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;

namespace Servy.Service
{
    public partial class Service : ServiceBase
    {
        private string _serviceName;
        private string _realExePath;
        private string _realArgs;
        private string _workingDir;
        private IntPtr _jobHandle = IntPtr.Zero;
        private Process _childProcess;
        private EventLog _eventLog;
        private RotatingStreamWriter _stdoutWriter;
        private RotatingStreamWriter _stderrWriter;
        private Timer _healthCheckTimer;
        private int _heartbeatIntervalSeconds;
        private int _maxFailedChecks;
        private int _failedChecks = 0;
        private RecoveryAction _recoveryAction;
        private bool _disposed = false; // Tracks whether Dispose has been called
        private readonly object _healthCheckLock = new object();
        private bool _isRecovering = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Service"/> class.
        /// Sets up the service name and event log source.
        /// </summary>
        public Service()
        {
            ServiceName = "Servy";

            // Initialize event log for diagnostic messages.
            _eventLog = new EventLog();
            if (!EventLog.SourceExists(ServiceName))
            {
                EventLog.CreateEventSource(ServiceName, "Application");
            }
            _eventLog.Source = ServiceName;
            _eventLog.Log = "Application";
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
                var fullArgs = Environment.GetCommandLineArgs();

                _eventLog?.WriteEntry($"[Args] {string.Join(" ", fullArgs)}");
                _eventLog?.WriteEntry($"[Args] fullArgs Length: {fullArgs.Length}");

                fullArgs = fullArgs.Select(a => a.Trim(' ', '"')).ToArray();

                if (fullArgs.Length < 2 || string.IsNullOrWhiteSpace(fullArgs[1]))
                {
                    _eventLog?.WriteEntry("Executable path not provided.", EventLogEntryType.Error);
                    Stop();
                    return;
                }

                // Extract parameters from args
                var realExePath = fullArgs[1];
                var realArgs = fullArgs.Length > 2 ? fullArgs[2] : string.Empty;
                var workingDir = fullArgs.Length > 3 ? fullArgs[3] : string.Empty;
                var priority = fullArgs.Length > 4 && Enum.TryParse<ProcessPriorityClass>(fullArgs[4], ignoreCase: true, out var p)
                    ? p
                    : ProcessPriorityClass.Normal;

                var stdoutFilePath = fullArgs.Length > 5 ? fullArgs[5] : string.Empty;
                var stderrFilePath = fullArgs.Length > 6 ? fullArgs[6] : string.Empty;
                var rotationSizeInBytes = fullArgs.Length > 7 && int.TryParse(fullArgs[7], out var rsb) ? rsb : 0; // 0 disables rotation

                var heartbeatInterval = fullArgs.Length > 8 && int.TryParse(fullArgs[8], out var hbi) ? hbi : 0; // 0 disables health monitoring
                var maxFailedChecks = fullArgs.Length > 9 && int.TryParse(fullArgs[9], out var mfc) ? mfc : 0; // 0 disables health monitoring
                var recoveryAction = fullArgs.Length > 10 && Enum.TryParse<RecoveryAction>(fullArgs[10], true, out var ra) ? ra : RecoveryAction.None; // None disables health monitoring
                _serviceName = fullArgs.Length > 11 ? fullArgs[11] : string.Empty;

                var stdoutRotationEnabled = !string.IsNullOrEmpty(stdoutFilePath);
                var stderrRotationEnabled = !string.IsNullOrEmpty(stderrFilePath);

                // Validate service name
                if (string.IsNullOrEmpty(_serviceName))
                {
                    _eventLog?.WriteEntry("Service name empty", EventLogEntryType.Error);
                    Stop();
                    return;
                }

                // Validate executable path existence
                if (!Helper.IsValidPath(realExePath) || !File.Exists(realExePath))
                {
                    _eventLog?.WriteEntry($"Executable not found: {realExePath}", EventLogEntryType.Error);
                    Stop();
                    return;
                }

                // Validate or fallback working directory
                var invalidWorkingDir = string.IsNullOrWhiteSpace(workingDir)
                    || !Helper.IsValidPath(workingDir)
                    || !Directory.Exists(workingDir);

                if (invalidWorkingDir)
                {
                    var system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    workingDir = Path.GetDirectoryName(realExePath) ?? system32;
                    _eventLog?.WriteEntry($"Working directory fallback applied: {workingDir}", EventLogEntryType.Warning);
                }

                // Validate stdout file path, disable rotation if invalid
                if (!string.IsNullOrWhiteSpace(stdoutFilePath) && !Helper.IsValidPath(stdoutFilePath))
                {
                    _eventLog?.WriteEntry($"Invalid stdout file path: {stdoutFilePath}", EventLogEntryType.Error);
                    stdoutRotationEnabled = false;
                }

                // Validate stderr file path, disable rotation if invalid
                if (!string.IsNullOrWhiteSpace(stderrFilePath) && !Helper.IsValidPath(stderrFilePath))
                {
                    _eventLog?.WriteEntry($"Invalid stderr file path: {stderrFilePath}", EventLogEntryType.Error);
                    stderrRotationEnabled = false;
                }

                // Log startup parameters
                _eventLog?.WriteEntry($"[serviceName] {_serviceName}");
                _eventLog?.WriteEntry($"[realExePath] {realExePath}");
                _eventLog?.WriteEntry($"[realArgs] {realArgs}");
                _eventLog?.WriteEntry($"[workingDir] {workingDir}");
                _eventLog?.WriteEntry($"[priority] {priority}");
                _eventLog?.WriteEntry($"[stdoutFilePath] {stdoutFilePath}");
                _eventLog?.WriteEntry($"[stderrFilePath] {stderrFilePath}");
                _eventLog?.WriteEntry($"[rotationSizeInBytes] {rotationSizeInBytes}");
                _eventLog?.WriteEntry($"[heartbeatInterval] {heartbeatInterval}");
                _eventLog?.WriteEntry($"[maxFailedChecks] {maxFailedChecks}");
                _eventLog?.WriteEntry($"[recoveryAction] {recoveryAction}");

                // Initialize rotating log writers if enabled
                if (stdoutRotationEnabled)
                {
                    _stdoutWriter = new RotatingStreamWriter(stdoutFilePath, rotationSizeInBytes);
                }

                if (stderrRotationEnabled)
                {
                    _stderrWriter = new RotatingStreamWriter(stderrFilePath, rotationSizeInBytes);
                }

                StartProcess(realExePath, realArgs, workingDir);

                // Set process priority AFTER starting process
                try
                {
                    _childProcess.PriorityClass = priority;
                    _eventLog?.WriteEntry($"Set process priority to {_childProcess.PriorityClass}.");
                }
                catch (Exception ex)
                {
                    _eventLog?.WriteEntry($"Failed to set priority: {ex.Message}", EventLogEntryType.Warning);
                }

                _heartbeatIntervalSeconds = heartbeatInterval;
                _maxFailedChecks = maxFailedChecks;
                _recoveryAction = recoveryAction;

                // health check setup
                if (_heartbeatIntervalSeconds > 0 && _maxFailedChecks > 0 && _recoveryAction != RecoveryAction.None)
                {
                    // Create timer instance
                    _healthCheckTimer = new Timer(_heartbeatIntervalSeconds * 1000); // interval in milliseconds

                    // Hook event handler
                    _healthCheckTimer.Elapsed += CheckHealth;

                    // Optional: Auto reset means it keeps firing repeatedly
                    _healthCheckTimer.AutoReset = true;

                    // Start the timer
                    _healthCheckTimer.Start();

                    _eventLog?.WriteEntry("Health monitoring started.");
                }

            }
            catch (Exception ex)
            {
                _eventLog?.WriteEntry($"Exception in OnStart: {ex.Message}", EventLogEntryType.Error);
                Stop();
            }
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

            _childProcess = new Process { StartInfo = psi };

            // Enable events and attach output/error handlers
            _childProcess.EnableRaisingEvents = true;
            _childProcess.OutputDataReceived += OnOutputDataReceived;
            _childProcess.ErrorDataReceived += OnErrorDataReceived;
            _childProcess.Exited += OnProcessExited;

            // Create a Windows Job Object to manage the process and ensure it's terminated if the parent dies
            _jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (_jobHandle == IntPtr.Zero || _jobHandle == new IntPtr(-1))
            {
                _eventLog?.WriteEntry("Failed to create Job Object.", EventLogEntryType.Error);
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
                        _eventLog?.WriteEntry("Failed to set information on Job Object.", EventLogEntryType.Error);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }

            // Start the process
            _childProcess.Start();
            _eventLog?.WriteEntry($"Started child process with PID: {_childProcess.Id}");

            // Assign the process to the Job Object
            if (_jobHandle != IntPtr.Zero && _jobHandle != new IntPtr(-1))
            {
                if (!NativeMethods.AssignProcessToJobObject(_jobHandle, _childProcess.Handle))
                {
                    _eventLog?.WriteEntry("Failed to assign process to Job Object.", EventLogEntryType.Error);
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

                        _eventLog?.WriteEntry(
                            $"Health check failed ({_failedChecks}/{_maxFailedChecks}). Child process has exited unexpectedly.",
                            EventLogEntryType.Warning);

                        if (_failedChecks >= _maxFailedChecks)
                        {
                            _isRecovering = true;
                            _failedChecks = 0; // Reset counter before recovery to avoid repeated triggers

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

                                            // Stop the service (this will terminate the current process)
                                            using (var controller = new ServiceController(_serviceName))
                                            {
                                                controller.Stop();
                                            }
                                        }
                                        else
                                        {
                                            _eventLog?.WriteEntry("Servy.Restarter.exe not found.", EventLogEntryType.Error);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _eventLog?.WriteEntry($"Failed to launch restarter: {ex}", EventLogEntryType.Error);
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
                                        _eventLog?.WriteEntry($"Failed to restart computer: {ex.Message}", EventLogEntryType.Error);
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
                            _eventLog?.WriteEntry("Child process is healthy again. Resetting failure count.");
                            _failedChecks = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _eventLog?.WriteEntry($"Error in health check: {ex}", EventLogEntryType.Error);
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
                _eventLog?.WriteEntry("Restarting child process...");

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

                _eventLog?.WriteEntry("Process restarted.");
            }
            catch (Exception ex)
            {
                _eventLog?.WriteEntry($"Failed to restart process: {ex.Message}", EventLogEntryType.Error);
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
                _eventLog?.WriteEntry($"[Error] {e.Data}", EventLogEntryType.Error);
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
                _eventLog?.WriteEntry(
                    code == 0 ? "Child process exited successfully." : $"Child process exited with code {code}.",
                    code == 0 ? EventLogEntryType.Information : EventLogEntryType.Warning);
            }
            catch (Exception ex)
            {
                _eventLog?.WriteEntry($"[Exited] Failed to get exit code: {ex.Message}", EventLogEntryType.Warning);
            }
        }

        /// <summary>
        /// Attempts to gracefully stop the process by sending a close message to its main window.
        /// If that fails or the process has no main window, forcibly kills the process.
        /// Waits up to the specified timeout for the process to exit.
        /// </summary>
        /// <param name="process">Process to stop.</param>
        /// <param name="timeoutMs">Timeout in milliseconds to wait for exit.</param>
        private void SafeKillProcess(Process process, int timeoutMs = 5000)
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
                    _eventLog?.WriteEntry("Graceful shutdown not supported. Forcing kill.", EventLogEntryType.Warning);
                    process.Kill();
                }

                process.WaitForExit(timeoutMs);
            }
            catch (Exception ex)
            {
                _eventLog?.WriteEntry($"SafeKillProcess error: {ex.Message}", EventLogEntryType.Warning);
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
                _eventLog?.WriteEntry($"Failed to dispose output writers: {ex.Message}", EventLogEntryType.Warning);
            }

            try
            {
                // Attempt to stop child process gracefully or kill forcibly
                SafeKillProcess(_childProcess);
            }
            catch (Exception ex)
            {
                _eventLog?.WriteEntry($"Failed to kill child process: {ex.Message}", EventLogEntryType.Error);
            }
            finally
            {
                _healthCheckTimer?.Dispose();
                _healthCheckTimer = null;

                _childProcess.Dispose();
                _childProcess = null;

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
            // Do your cleanup
            Cleanup();

            base.OnStop();

            _eventLog?.WriteEntry("Stopped child process.");
        }
    }
}
