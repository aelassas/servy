using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;

namespace Servy.Service
{
    public partial class Service : ServiceBase
    {
        private Process _childProcess;
        private EventLog _eventLog;

        public Service()
        {
            ServiceName = "Servy";

            // Initialize event log
            _eventLog = new EventLog();
            if (!EventLog.SourceExists(ServiceName))
            {
                EventLog.CreateEventSource(ServiceName, "Application");
            }
            _eventLog.Source = ServiceName;
            _eventLog.Log = "Application";
        }

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

                var realExePath = fullArgs[1];
                var realArgs = fullArgs.Length > 2 ? fullArgs[2] : string.Empty;
                var workingDir = fullArgs.Length > 3 ? fullArgs[3] : string.Empty;
                var priority = fullArgs.Length > 4 && Enum.TryParse<ProcessPriorityClass>(fullArgs[4], ignoreCase: true, out var p) ? p : ProcessPriorityClass.Normal;

                if (!File.Exists(realExePath))
                {
                    _eventLog?.WriteEntry($"Executable not found: {realExePath}", EventLogEntryType.Error);
                    Stop();
                    return;
                }

                if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
                {
                    var system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                    workingDir = Path.GetDirectoryName(realExePath) ?? system32;
                    _eventLog?.WriteEntry($"Working directory fallback applied: {workingDir}", EventLogEntryType.Warning);
                }

                _eventLog?.WriteEntry($"[realExePath] {realExePath}");
                _eventLog?.WriteEntry($"[realArgs] {realArgs}");
                _eventLog?.WriteEntry($"[workingDir] {workingDir}");
                _eventLog?.WriteEntry($"[priority] {priority}");

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

                //_childProcess.OutputDataReceived += (sender, e) =>
                //{
                //    if (!string.IsNullOrWhiteSpace(e.Data))
                //    {
                //        _eventLog?.WriteEntry($"[Output] {e.Data}", EventLogEntryType.Information);
                //    }
                //};

                _childProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        _eventLog?.WriteEntry($"[Error] {e.Data}", EventLogEntryType.Error);
                    }
                };

                _childProcess.EnableRaisingEvents = true;
                _childProcess.Exited += (sender, e) =>
                {
                    try
                    {
                        _eventLog?.WriteEntry(
                            _childProcess.ExitCode == 0
                            ? "Wrapped process exited successfully."
                            : $"Wrapped process exited with code {_childProcess.ExitCode}.",
                            _childProcess.ExitCode == 0 ? EventLogEntryType.Information : EventLogEntryType.Warning);
                    }
                    catch (Exception ex)
                    {
                        _eventLog?.WriteEntry($"[Exited] Failed to get exit code: {ex.Message}", EventLogEntryType.Warning);
                    }
                };

                _childProcess.Start();

                // Set priority *after* starting the process
                try
                {
                    // Idle, BelowNormal, Normal (default), AboveNormal, High, RealTime (use with caution)
                    _childProcess.PriorityClass = priority;
                    _eventLog?.WriteEntry($"Set process priority to {_childProcess.PriorityClass}.");
                }
                catch (Exception ex)
                {
                    _eventLog?.WriteEntry($"Failed to set priority: {ex.Message}", EventLogEntryType.Warning);
                }

                _childProcess.BeginOutputReadLine();
                _childProcess.BeginErrorReadLine();

                _eventLog?.WriteEntry("Started wrapped process.");
            }
            catch (Exception ex)
            {
                _eventLog?.WriteEntry($"Exception in OnStart: {ex.Message}", EventLogEntryType.Error);
                Stop();
            }
        }

        protected override void OnStop()
        {
            if (_childProcess != null && !_childProcess.HasExited)
            {
                try
                {
                    _childProcess.Kill();
                    _childProcess.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    _eventLog?.WriteEntry($"Failed to kill child process: {ex.Message}", EventLogEntryType.Error);
                }
                finally
                {
                    _childProcess.Dispose();
                    _childProcess = null;
                }
            }

            _eventLog?.WriteEntry("Stopped wrapped process.");
        }
    }
}
