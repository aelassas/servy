using System;
using System.Diagnostics;
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
            string[] fullArgs = Environment.GetCommandLineArgs();
            _eventLog?.WriteEntry($"[Args] {string.Join(" ", fullArgs)}");
            string realExePath = (fullArgs.Length > 0 ? fullArgs[1] : string.Empty).Trim('"');
            string realArgs = (fullArgs.Length > 1 ? fullArgs[2] : string.Empty).Trim('"');
            string workingDir = (fullArgs.Length > 2 ? fullArgs[3] : string.Empty).Trim('"');

            _eventLog?.WriteEntry($"[realExePath] {realExePath}");
            _eventLog?.WriteEntry($"[realArgs] {realArgs}");
            _eventLog?.WriteEntry($"[workingDir] {workingDir}");

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

            _childProcess = new Process();
            _childProcess.StartInfo = psi;

            //_childProcess.OutputDataReceived += (sender, e) =>
            //{
            //    if (!string.IsNullOrEmpty(e.Data))
            //    {
            //        _eventLog?.WriteEntry($"[Output] {e.Data}", EventLogEntryType.Information);
            //    }
            //};

            _childProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _eventLog?.WriteEntry($"[Error] {e.Data}", EventLogEntryType.Error);
                }
            };

            _childProcess.Start();
            _childProcess.BeginOutputReadLine();
            _childProcess.BeginErrorReadLine();

            _eventLog?.WriteEntry("Started wrapped process.");
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
