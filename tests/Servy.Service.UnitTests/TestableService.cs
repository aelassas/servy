using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Servy.Service.UnitTests
{
    public class TestableService : Service
    {
        /// <summary>
        /// Caches reflection bindings to <see cref="Service"/>'s private members.
        /// Binding runs on first access to this class, not when <see cref="TestableService"/>
        /// loads, and a failed binding throws (wrapped in a
        /// <see cref="TypeInitializationException"/>, so the message is one InnerException down)
        /// rather than letting a renamed member degrade into a silently skipped assertion.
        /// Members that reach <see cref="Service"/> directly, such as InvokeCheckHealthAsync,
        /// do not pass through here and validate nothing.
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
            public static readonly FieldInfo RecoveryActionEnabledField = GetField("_recoveryActionEnabled");

            public static readonly MethodInfo HandleLogWritersMethod = GetMethod("HandleLogWriters");
            public static readonly MethodInfo SetupHealthMonitoringMethod = GetMethod("SetupHealthMonitoring");
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
            Servy.Service.Helpers.IServiceHelper serviceHelper,
            IServyLogger logger,
            IStreamWriterFactory streamWriterFactory,
            ITimerFactory timerFactory,
            IProcessFactory processFactory,
            IPathValidator pathValidator,
            IServiceRepository serviceRepository,
            IProcessKiller processKiller
            )
            : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, serviceRepository, processKiller)
        {
        }

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

        public Task InvokeCheckHealthAsync(object sender, ElapsedEventArgs e) =>
            CheckHealthCoreAsync(sender, e);

        public void InvokeOnOutputDataReceived(object sender, DataReceivedEventArgs e) =>
            ServiceReflection.OnOutputDataReceivedMethod.Invoke(this, new object[] { sender, e });

        public void InvokeOnErrorDataReceived(object sender, DataReceivedEventArgs e) =>
            ServiceReflection.OnErrorDataReceivedMethod.Invoke(this, new object[] { sender, e });

        public void InvokeOnProcessExited(object sender, EventArgs e) =>
            ServiceReflection.OnProcessExitedMethod.Invoke(this, new object[] { sender, e });

        public IProcessWrapper GetChildProcess() =>
            (IProcessWrapper)ServiceReflection.ChildProcessField.GetValue(this);

        // Expose the private StartProcess method for direct invocation
        public void InvokeStartProcess(string exePath, string args, string workingDir, List<EnvironmentVariable> environmentVariables, CancellationToken cancellationToken)
        {
            ServiceReflection.StartProcessMethod.Invoke(this, new object[] { exePath, args, workingDir, environmentVariables, cancellationToken });
        }

        // Expose the private SafeKillProcess method, including the timeout it computes its deadline from
        public void InvokeSafeKillProcess(IProcessWrapper process, int timeoutMs) =>
            ServiceReflection.SafeKillProcessMethod.Invoke(this, new object[] { process, timeoutMs });

        // Forces the state of the private backer field '_recoveryActionEnabled' via reflection.
        public void SetRecoveryActionEnabled(bool enabled) =>
            ServiceReflection.RecoveryActionEnabledField.SetValue(this, enabled);
    }
}
