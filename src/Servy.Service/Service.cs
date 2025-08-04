using Servy.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Servy.Service
{
    public partial class Service : ServiceBase
    {
        private IntPtr _jobHandle = IntPtr.Zero;
        private Process _childProcess;
        private EventLog _eventLog;
        private RotatingStreamWriter _stdoutWriter;
        private RotatingStreamWriter _stderrWriter;

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

                var stdoutRotationEnabled = !string.IsNullOrEmpty(stdoutFilePath);
                var stderrRotationEnabled = !string.IsNullOrEmpty(stderrFilePath);

                // Validate executable path existence
                if (!File.Exists(realExePath))
                {
                    _eventLog?.WriteEntry($"Executable not found: {realExePath}", EventLogEntryType.Error);
                    Stop();
                    return;
                }

                // Validate or fallback working directory
                if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
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
                _eventLog?.WriteEntry($"[realExePath] {realExePath}");
                _eventLog?.WriteEntry($"[realArgs] {realArgs}");
                _eventLog?.WriteEntry($"[workingDir] {workingDir}");
                _eventLog?.WriteEntry($"[priority] {priority}");
                _eventLog?.WriteEntry($"[stdoutFilePath] {stdoutFilePath}");
                _eventLog?.WriteEntry($"[stderrFilePath] {stderrFilePath}");
                _eventLog?.WriteEntry($"[rotationSizeInBytes] {rotationSizeInBytes}");

                // Initialize rotating log writers if enabled
                if (stdoutRotationEnabled)
                {
                    _stdoutWriter = new RotatingStreamWriter(stdoutFilePath, rotationSizeInBytes);
                }

                if (stderrRotationEnabled)
                {
                    _stderrWriter = new RotatingStreamWriter(stderrFilePath, rotationSizeInBytes);
                }

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

                _childProcess.EnableRaisingEvents = true;
                _childProcess.OutputDataReceived += OnOutputDataReceived;
                _childProcess.ErrorDataReceived += OnErrorDataReceived;
                _childProcess.Exited += OnProcessExited;

                // Create a Windows Job Object to manage child process lifetime
                _jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero || _jobHandle == new IntPtr(-1))
                {
                    _eventLog?.WriteEntry("Failed to create Job Object.", EventLogEntryType.Error);
                }
                else
                {
                    // Configure Job Object to terminate all child processes when this handle is closed
                    var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                    {
                        BasicLimitInformation = { LimitFlags = NativeMethods.LimitFlags.KillOnJobClose }
                    };

                    int length = Marshal.SizeOf(typeof(NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                    try
                    {
                        Marshal.StructureToPtr(info, extendedInfoPtr, false);
                        if (!NativeMethods.SetInformationJobObject(_jobHandle,
                            NativeMethods.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                            extendedInfoPtr, (uint)length))
                        {
                            _eventLog?.WriteEntry("Failed to set information on Job Object.", EventLogEntryType.Error);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(extendedInfoPtr);
                    }
                }

                _childProcess.Start();

                // Assign child process to the job object for group management
                if (_jobHandle != IntPtr.Zero && _jobHandle != new IntPtr(-1))
                {
                    if (!NativeMethods.AssignProcessToJobObject(_jobHandle, _childProcess.Handle))
                    {
                        _eventLog?.WriteEntry("Failed to assign process to Job Object.", EventLogEntryType.Error);
                    }
                }

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

                _childProcess.BeginOutputReadLine();
                _childProcess.BeginErrorReadLine();

                _eventLog?.WriteEntry("Started child process.");
            }
            catch (Exception ex)
            {
                _eventLog?.WriteEntry($"Exception in OnStart: {ex.Message}", EventLogEntryType.Error);
                Stop();
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
                    code == 0 ? "Wrapped process exited successfully." : $"Wrapped process exited with code {code}.",
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
        /// Called when the service stops.
        /// Unhooks event handlers, disposes output writers,
        /// kills the child process, and closes the job object handle.
        /// </summary>
        protected override void OnStop()
        {
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
                _childProcess.Dispose();
                _childProcess = null;

                // Closing the job handle will terminate all associated child processes automatically
                if (_jobHandle != IntPtr.Zero && _jobHandle != new IntPtr(-1))
                {
                    NativeMethods.CloseHandle(_jobHandle);
                    _jobHandle = IntPtr.Zero;
                }

                GC.SuppressFinalize(this);
            }

            _eventLog?.WriteEntry("Stopped child process.");
        }
    }
}
