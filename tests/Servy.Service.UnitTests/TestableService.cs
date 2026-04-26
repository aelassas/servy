using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
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
using System.Reflection;
using System.Timers;

namespace Servy.Service.UnitTests
{
    public class TestableService : Service
    {
        private Action<string, string, string, List<EnvironmentVariable>> _startProcessOverride;
        private Action _terminateChildProcessesOverride;

        /// <summary>
        /// Caches reflection bindings at class-load time. 
        /// Throws loudly and immediately if the underlying Service class is refactored 
        /// (e.g., variables renamed) ensuring tests don't silently fail.
        /// </summary>
        private static class ServiceReflection
        {
            private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

            public static readonly FieldInfo ChildProcessField = GetField("_childProcess");
            public static readonly FieldInfo MaxFailedChecksField = GetField("_maxFailedChecks");
            public static readonly FieldInfo RecoveryActionField = GetField("_recoveryAction");
            public static readonly FieldInfo FailedChecksField = GetField("_failedChecks");
            public static readonly FieldInfo MaxRestartAttemptsField = GetField("_maxRestartAttempts");
            public static readonly FieldInfo ServiceNameField = GetField("_serviceName");

            public static readonly MethodInfo HandleLogWritersMethod = GetMethod("HandleLogWriters");
            public static readonly MethodInfo SetupHealthMonitoringMethod = GetMethod("SetupHealthMonitoring");
            public static readonly MethodInfo CheckHealthMethod = GetMethod("CheckHealth");
            public static readonly MethodInfo OnOutputDataReceivedMethod = GetMethod("OnOutputDataReceived");
            public static readonly MethodInfo OnErrorDataReceivedMethod = GetMethod("OnErrorDataReceived");
            public static readonly MethodInfo OnProcessExitedMethod = GetMethod("OnProcessExited");
            public static readonly MethodInfo StartProcessMethod = GetMethod("StartProcess");
            public static readonly MethodInfo SafeKillProcessMethod = GetMethod("SafeKillProcess");

            private static FieldInfo GetField(string name) =>
                typeof(Service).GetField(name, Flags)
                ?? throw new InvalidOperationException($"Reflection binding failed: Field '{name}' not found on Service. Did you rename it?");

            private static MethodInfo GetMethod(string name) =>
                typeof(Service).GetMethod(name, Flags)
                ?? throw new InvalidOperationException($"Reflection binding failed: Method '{name}' not found on Service. Did you rename it?");
        }

        public TestableService(
            IServiceHelper serviceHelper,
            IServyLogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory,
            IPathValidator pathValidator,
            IServiceRepository serviceRepository,
            IProcessHelper processHelper,
            IProcessKiller processKiller
            )
            : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, serviceRepository, processHelper, processKiller)
        {
        }

        // Instead of overriding OnStart, expose a public method to call the base protected OnStart:
        public void TestOnStart()
        {
            base.OnStart(new string[] { TestModeFlag });
        }

        public void InvokeSetProcessPriority(ProcessPriorityClass priority) => SetProcessPriority(priority);

        public void SetChildProcess(IProcessWrapper process) =>
            ServiceReflection.ChildProcessField.SetValue(this, process);

        public void InvokeHandleLogWriters(StartOptions options) =>
            ServiceReflection.HandleLogWritersMethod.Invoke(this, new object[] { options });

        public void InvokeSetupHealthMonitoring(StartOptions options) =>
            ServiceReflection.SetupHealthMonitoringMethod.Invoke(this, new object[] { options });

        public void SetMaxFailedChecks(int value) =>
            ServiceReflection.MaxFailedChecksField.SetValue(this, value);

        public void SetRecoveryAction(RecoveryAction action) =>
            ServiceReflection.RecoveryActionField.SetValue(this, action);

        public void SetFailedChecks(int value) =>
            ServiceReflection.FailedChecksField.SetValue(this, value);

        public void SetMaxRestartAttempts(int value) =>
            ServiceReflection.MaxRestartAttemptsField.SetValue(this, value);

        public void SetServiceName(string serviceName) =>
            ServiceReflection.ServiceNameField.SetValue(this, serviceName);

        public int GetFailedChecks() =>
            (int)ServiceReflection.FailedChecksField.GetValue(this);

        public void InvokeCheckHealth(object sender, ElapsedEventArgs e) =>
            ServiceReflection.CheckHealthMethod.Invoke(this, new object[] { sender, e });

        public void InvokeOnOutputDataReceived(object sender, DataReceivedEventArgs e) =>
            ServiceReflection.OnOutputDataReceivedMethod.Invoke(this, new object[] { sender, e });

        public void InvokeOnErrorDataReceived(object sender, DataReceivedEventArgs e) =>
            ServiceReflection.OnErrorDataReceivedMethod.Invoke(this, new object[] { sender, e });

        public void InvokeOnProcessExited(object sender, EventArgs e) =>
            ServiceReflection.OnProcessExitedMethod.Invoke(this, new object[] { sender, e });

        public void OverrideStartProcess(Action<string, string, string, List<EnvironmentVariable>> startProcess)
        {
            _startProcessOverride = startProcess;
        }

        public void OverrideTerminateChildProcesses(Action terminateChildProcesses)
        {
            _terminateChildProcessesOverride = terminateChildProcesses;
        }

        // Expose child process for asserts
        public IProcessWrapper GetChildProcess() =>
            (IProcessWrapper)ServiceReflection.ChildProcessField.GetValue(this);

        // Expose StartProcess protected method and allow override logic
        public void InvokeStartProcess(string exePath, string args, string workingDir, List<EnvironmentVariable> environmentVariables)
        {
            if (_startProcessOverride != null)
            {
                _startProcessOverride(exePath, args, workingDir, environmentVariables);
            }
            else
            {
                ServiceReflection.StartProcessMethod.Invoke(this, new object[] { exePath, args, workingDir, environmentVariables });
            }
        }

        // Expose SafeKillProcess protected method
        public void InvokeSafeKillProcess(IProcessWrapper process) =>
            ServiceReflection.SafeKillProcessMethod.Invoke(this, new object[] { process, 5000 });
    }
}