using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace Servy.Service.UnitTests
{
    public class TestableService : Service
    {
        private Action<string, string, string, List<EnvironmentVariable>> _startProcessOverride;
        private Action _terminateChildProcessesOverride;

        public TestableService(
            IServiceHelper serviceHelper,
            ILogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory,
            IPathValidator pathValidator,
            IServiceRepository serviceRepository)
            : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, serviceRepository)
        {
        }

        // Instead of overriding OnStart, expose a public method to call the base protected OnStart:
        public void TestOnStart(string[] args)
        {
            base.OnStart(args);
        }

        public void InvokeSetProcessPriority(ProcessPriorityClass priority) => SetProcessPriority(priority);

        public void SetChildProcess(IProcessWrapper process) =>
            typeof(Service).GetField("_childProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(this, process);

        public void InvokeHandleLogWriters(StartOptions options) =>
            typeof(Service).GetMethod("HandleLogWriters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(this, new object[] { options });

        public void InvokeSetupHealthMonitoring(StartOptions options)
        {
            var method = typeof(Service).GetMethod("SetupHealthMonitoring", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(this, new object[] { options });
        }

        public void SetMaxFailedChecks(int value)
        {
            typeof(Service).GetField("_maxFailedChecks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, value);
        }

        public void SetRecoveryAction(RecoveryAction action)
        {
            typeof(Service).GetField("_recoveryAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, action);
        }

        public void SetFailedChecks(int value)
        {
            typeof(Service).GetField("_failedChecks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, value);
        }

        public void SetMaxRestartAttempts(int value)
        {
            typeof(Service).GetField("_maxRestartAttempts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, value);
        }

        public void SetServiceName(string serviceName)
        {
            typeof(Service).GetField("_serviceName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, serviceName);
        }

        public int GetFailedChecks()
        {
            return (int)typeof(Service).GetField("_failedChecks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        public void InvokeCheckHealth(object sender, ElapsedEventArgs e)
        {
            var method = typeof(Service).GetMethod("CheckHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(this, new object[] { sender, e });
        }

        public void InvokeOnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            var method = typeof(Service).GetMethod("OnOutputDataReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(this, new object[] { sender, e });
        }

        public void InvokeOnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            var method = typeof(Service).GetMethod("OnErrorDataReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(this, new object[] { sender, e });
        }

        public void InvokeOnProcessExited(object sender, EventArgs e)
        {
            var method = typeof(Service).GetMethod("OnProcessExited", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(this, new object[] { sender, e });
        }

        public void OverrideStartProcess(Action<string, string, string, List<EnvironmentVariable>> startProcess)
        {
            _startProcessOverride = startProcess;
        }

        public void OverrideTerminateChildProcesses(Action terminateChildProcesses)
        {
            _terminateChildProcessesOverride = terminateChildProcesses;
        }

        // Expose child process for asserts
        public IProcessWrapper GetChildProcess()
        {
            return (IProcessWrapper)typeof(Service).GetField("_childProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        // Expose StartProcess protected method and allow override logic
        public void InvokeStartProcess(string exePath, string args, string workingDir, List<EnvironmentVariable> environmentVariables)
        {
            if (_startProcessOverride != null)
            {
                _startProcessOverride(exePath, args, workingDir, environmentVariables);
            }
            else
            {
                var method = typeof(Service).GetMethod("StartProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method.Invoke(this, new object[] { exePath, args, workingDir, environmentVariables });
            }
        }

        // Expose SafeKillProcess protected method
        public void InvokeSafeKillProcess(IProcessWrapper process)
        {
            var method = typeof(Service).GetMethod("SafeKillProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(this, new object[] { process, 5000 });
        }

    }
}
