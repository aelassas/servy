using Moq;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Covers the arms of <c>ExecuteRecoveryAction</c> that the health-check and process-exit suites
    /// do not reach: the tracked-hook pruning that runs before a restart, the start-process callback
    /// handed to <see cref="IServiceHelper.RestartProcess"/>, and the reboot failure that has to put
    /// <c>_isRebooting</c> back.
    /// </summary>
    /// <remarks>
    /// The two teardown races the main-branch twin of this class also covers have no counterpart
    /// here: on this branch recovery is entered through the synchronous <c>InitiateRecovery</c>,
    /// which has no awaited semaphore wait to dispose or cancel and no catch for either.
    /// </remarks>
    public class RecoveryExecutionTests : IDisposable
    {
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        /// <summary>
        /// A <see cref="Hook"/> that records whether it was disposed, so the pruning test can assert
        /// the hook was released rather than only dropped from the list.
        /// </summary>
        private sealed class RecordingHook : Hook
        {
            /// <summary>
            /// Gets a value indicating whether <see cref="Hook.Dispose()"/> has run on this instance.
            /// </summary>
            public bool WasDisposed { get; private set; }

            /// <summary>
            /// Records the disposal and then defers to the base implementation.
            /// </summary>
            /// <param name="disposing">
            /// <see langword="true"/> when called from <see cref="Hook.Dispose()"/>; this type adds no
            /// finalizer, so it is never <see langword="false"/>.
            /// </param>
            protected override void Dispose(bool disposing)
            {
                WasDisposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Wires the service for a recovery that runs without consulting the restart-attempts file:
        /// options present, recovery enabled, the attempt cap unlimited and the relaunch arguments
        /// set, since <c>ExecuteRecoveryAction</c> passes them to the helper for a RestartProcess action.
        /// </summary>
        /// <param name="service">The service under test.</param>
        /// <param name="action">The recovery action to dispatch.</param>
        /// <param name="options">The start options to install, or <see langword="null"/> for the defaults.</param>
        private static void ArrangeUnlimitedRecovery(TestableService service, RecoveryAction action, StartOptions options = null)
        {
            TestReflection.SetField(service, "_options", options ?? ServiceTestContext.CreateDefaultStartOptions());
            TestReflection.SetField(service, "_realExePath", "C:\\myapp.exe");
            TestReflection.SetField(service, "_realArgs", "--arg");
            TestReflection.SetField(service, "_workingDir", "C:\\workdir");

            service.SetServiceName("ServyTestService");
            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(action);
            service.SetMaxRestartAttempts(0); // unlimited: recovery never reads the attempts file
        }

        /// <summary>
        /// Verifies that no recovery action of any kind was dispatched.
        /// </summary>
        /// <param name="ctx">The context holding the helper mock.</param>
        private static void VerifyNoRecoveryActionRan(ServiceTestContext ctx)
        {
            ctx.Helper.Verify(h => h.RestartService(It.IsAny<string>(), It.IsAny<IServyLogger>()), Times.Never);
            ctx.Helper.Verify(h => h.RestartComputer(It.IsAny<IServyLogger>()), Times.Never);
            ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(),
                It.IsAny<bool>(),
                It.IsAny<IServyLogger>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void InitiateRecovery_RestartProcessCallback_ReAppliesPriorityAndAffinity()
        {
            // Arrange
            var service = _ctx.Build();

            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.Priority = ProcessPriorityClass.High;
            options.CpuAffinity = "0"; // a single-core mask, so the parse does not depend on the runner's core count
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartProcess, options);

            // The relaunched child. StartProcess installs it as _childProcess, which is what
            // SetProcessPriority and SetProcessCpuAffinity then write to.
            var newProcess = new Mock<IProcessWrapper>();
            newProcess.Setup(p => p.Id).Returns(4242);
            newProcess.Setup(p => p.Start()).Returns(true);
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(newProcess.Object);

            // The production helper invokes the callback it is handed; the mock has to do the same or
            // the priority and affinity re-application is never executed.
            _ctx.Helper
                .Setup(h => h.RestartProcess(
                    It.IsAny<IProcessWrapper>(),
                    It.IsAny<StartProcessCallback>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<List<EnvironmentVariable>>(),
                    It.IsAny<bool>(),
                    It.IsAny<IServyLogger>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IProcessWrapper, StartProcessCallback, string, string, string, List<EnvironmentVariable>, bool, IServyLogger, int, CancellationToken>(
                    (process, startProcess, exePath, arguments, workingDirectory, environmentVariables, allowOverriddenRuntimeVars, logger, stopTimeoutMs, cancellationToken) =>
                        startProcess(exePath, arguments, workingDirectory, environmentVariables ?? new List<EnvironmentVariable>(), allowOverriddenRuntimeVars, cancellationToken));

            // Act
            service.InvokeInitiateRecovery();

            // Assert
            // Both settings are re-applied to the NEW process: a restart that forgets them would leave
            // the relaunched child at the default priority and with no affinity mask.
            newProcess.VerifySet(p => p.PriorityClass = ProcessPriorityClass.High, Times.Once);
            newProcess.VerifySet(p => p.ProcessorAffinity = new IntPtr(0x1), Times.Once);
            Assert.Same(newProcess.Object, service.GetChildProcess());
        }

        [Fact]
        public void ExecuteRecoveryAction_PrunesExitedHooksAndLeavesAThrowingHookForTeardown()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.None);

            // A hook with no process is prunable; one whose Process was never started throws
            // InvalidOperationException from HasExited, which the catch has to swallow so the hook
            // survives for teardown to deal with.
            var prunable = new RecordingHook { OperationName = "Pre-Launch", Process = null };
            var throwing = new Hook { OperationName = "Post-Launch", Process = new Process() };

            var trackedHooks = TestReflection.GetField<List<Hook>>(service, "_trackedHooks");
            trackedHooks.Add(prunable);
            trackedHooks.Add(throwing);

            // Act
            service.InvokeExecuteRecoveryAction(1);

            // Assert
            // The prunable hook is disposed and removed; the throwing one is neither.
            Assert.True(prunable.WasDisposed);
            Assert.Equal(new Hook[] { throwing }, trackedHooks.ToArray());

            // RecoveryAction.None dispatches nothing, so the pruning is the only observable effect.
            VerifyNoRecoveryActionRan(_ctx);

            throwing.Dispose();
        }

        [Fact]
        public void InitiateRecovery_RebootFails_ResetsIsRebootingAndLogsTheRecoveryFailure()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartComputer);

            _ctx.Helper
                .Setup(h => h.RestartComputer(It.IsAny<IServyLogger>()))
                .Throws(new InvalidOperationException("reboot refused"));

            // Act
            service.InvokeInitiateRecovery();

            // Assert
            // The rethrow reaches InitiateRecovery's own catch, which logs and leaves
            // recoveryActionSucceeded false.
            _ctx.Logger.Verify(l => l.Error(
                "Critical error during recovery execution: reboot refused",
                It.IsAny<Exception>()), Times.Once);

            // The reset is the behaviour worth pinning: _isRebooting gates the pre-shutdown and
            // process-exit paths, so a reboot that did not happen must not leave the service believing
            // one is under way. ServiceTests already pins what a true _isRebooting suppresses
            // ("Pre-Shutdown bypassed"), so this assertion and that one together cover the consequence.
            Assert.False(TestReflection.GetField<bool>(service, "_isRebooting"));

            // A failed terminal action reopens the gate, so a later health check is not swallowed.
            Assert.False(TestReflection.GetField<bool>(service, "_isRecovering"));
        }

        public void Dispose()
        {
            _ctx.Dispose();
        }
    }
}
