using Moq;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System.Diagnostics;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Covers the arms of <c>InitiateRecoveryAsync</c> and <c>ExecuteRecoveryAction</c> that the
    /// health-check and process-exit suites do not reach: the two teardown races around its own
    /// semaphore wait, the busy-flag re-check under the lock, a stop that cancels the restart-attempts
    /// read or begins during the attempts save, the swallowed failure of the semaphore release, the
    /// state a successful action leaves (the gate, <c>_isRebooting</c> and the failure counter), the
    /// catch that swallows a failing recovery action, the tracked-hook pruning that runs before a
    /// restart, the relaunch values and start-process callback handed to
    /// <see cref="IServiceHelper.RestartProcess"/> (priority and affinity re-applied, the teardown lock
    /// held, the relaunch skipped once a stop has begun), and the reboot failure that has to put
    /// <c>_isRebooting</c> back.
    /// </summary>
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
        /// Wires the service for a recovery that runs without reading the restart-attempts counter:
        /// options present, recovery enabled, the attempt cap unlimited and the relaunch arguments
        /// set, since <c>ExecuteRecoveryAction</c> dereferences them for a RestartProcess action.
        /// </summary>
        /// <param name="service">The service under test.</param>
        /// <param name="action">The recovery action to dispatch.</param>
        /// <param name="options">The start options to install, or <see langword="null"/> for the defaults.</param>
        private static void ArrangeUnlimitedRecovery(TestableService service, RecoveryAction action, StartOptions? options = null)
        {
            TestReflection.SetField(service, "_options", options ?? ServiceTestContext.CreateDefaultStartOptions());
            TestReflection.SetField(service, "_realExePath", "C:\\myapp.exe");
            TestReflection.SetField(service, "_realArgs", "--arg");
            TestReflection.SetField(service, "_workingDir", "C:\\workdir");

            service.SetServiceName("ServyTestService");
            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(action);
            service.SetMaxRestartAttempts(0); // unlimited: recovery never reads the attempts counter
        }

        /// <summary>
        /// Verifies that no recovery action of any kind was dispatched.
        /// </summary>
        /// <param name="ctx">The context holding the helper mock.</param>
        private static void VerifyNoRecoveryActionRan(ServiceTestContext ctx)
        {
            ctx.Helper.Verify(h => h.RestartService(It.IsAny<string>(), It.IsAny<IServyLogger?>()), Times.Never);
            ctx.Helper.Verify(h => h.RestartComputer(It.IsAny<IServyLogger?>()), Times.Never);
            ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper?>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(),
                It.IsAny<bool>(),
                It.IsAny<IServyLogger?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InitiateRecoveryAsync_SemaphoreDisposedBeforeWait_AbandonsRecoveryQuietly()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartService);

            // Teardown disposed the health-check semaphore before recovery reached its wait. The log
            // line carries the method name, so it is distinct from the CheckHealth and OnProcessExited
            // races that HealthCheckTests and ProcessExitOutcomeTests already pin.
            TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore").Dispose();

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                "InitiateRecoveryAsync: Semaphore disposed during wait. Teardown in progress.",
                It.IsAny<Exception>()), Times.Once);

            // The early return is the contract: the gate is never closed and no action is dispatched,
            // so removing that return fails this test even though the log line would still be written.
            Assert.False(TestReflection.GetField<bool>(service, "_isRecovering"));
            VerifyNoRecoveryActionRan(_ctx);
        }

        [Fact]
        public async Task InitiateRecoveryAsync_WaitAlreadyCancelled_AbandonsRecoveryQuietly()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartService);

            var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            TestReflection.SetField(service, "_cancellationSource", cancellationSource);

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                "InitiateRecoveryAsync: Wait cancelled. Teardown in progress.",
                It.IsAny<Exception>()), Times.Once);

            Assert.False(TestReflection.GetField<bool>(service, "_isRecovering"));
            VerifyNoRecoveryActionRan(_ctx);
        }

        [Fact]
        public async Task InitiateRecoveryAsync_RestartProcessCallback_ReAppliesPriorityAndAffinity()
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
                    It.IsAny<IProcessWrapper?>(),
                    It.IsAny<StartProcessCallback>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<List<EnvironmentVariable>>(),
                    It.IsAny<bool>(),
                    It.IsAny<IServyLogger?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IProcessWrapper?, StartProcessCallback, string, string, string, List<EnvironmentVariable>, bool, IServyLogger?, int, CancellationToken>(
                    (_, startProcess, exePath, arguments, workingDirectory, environmentVariables, allowOverriddenRuntimeVars, _, _, cancellationToken) =>
                        startProcess(exePath, arguments, workingDirectory, environmentVariables ?? new List<EnvironmentVariable>(), allowOverriddenRuntimeVars, cancellationToken));

            // Act
            await service.InvokeInitiateRecoveryAsync();

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
        public async Task InitiateRecoveryAsync_RebootFails_ResetsIsRebootingAndLogsTheRecoveryFailure()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartComputer);

            _ctx.Helper
                .Setup(h => h.RestartComputer(It.IsAny<IServyLogger?>()))
                .Throws(new InvalidOperationException("reboot refused"));

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Assert
            // The rethrow reaches InitiateRecoveryAsync's own catch, which logs and leaves
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

        [Fact]
        public async Task InitiateRecoveryAsync_RestartProcess_HandsTheHelperTheConfiguredRelaunchValues()
        {
            // Arrange
            var service = _ctx.Build();

            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.AllowOverriddenRuntimeVars = true;   // the default is false
            options.StopTimeoutInSeconds = 42;           // not AppConfig.DefaultStopTimeout
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartProcess, options);

            var oldProcess = new Mock<IProcessWrapper>();
            service.SetChildProcess(oldProcess.Object);

            var environment = new List<EnvironmentVariable> { new EnvironmentVariable { Name = "A", Value = "1" } };
            TestReflection.SetField(service, "_environmentVariables", environment);

            using var cancellationSource = new CancellationTokenSource();
            TestReflection.SetField(service, "_cancellationSource", cancellationSource);

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Assert
            // Every argument is distinct and non-default, so a swapped, nulled or hard-coded value fails.
            _ctx.Helper.Verify(h => h.RestartProcess(
                oldProcess.Object,
                It.IsAny<StartProcessCallback>(),
                "C:\\myapp.exe",
                "--arg",
                "C:\\workdir",
                environment,
                true,
                _ctx.Logger.Object,
                42 * AppConfig.MillisecondsPerSecond,
                cancellationSource.Token), Times.Once);
        }

        [Theory]
        [InlineData(RecoveryAction.RestartService, true, false)]
        [InlineData(RecoveryAction.RestartComputer, true, true)]
        [InlineData(RecoveryAction.RestartProcess, false, false)]
        [InlineData(RecoveryAction.None, false, false)]
        public async Task InitiateRecoveryAsync_ActionSucceeds_LeavesTheGateAndRebootFlagAsDocumented(
            RecoveryAction action, bool gateStaysClosed, bool rebooting)
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, action);
            service.SetFailedChecks(3);

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Assert
            // A successful RestartService/RestartComputer keeps the gate closed (the instance is about
            // to be replaced); the other actions reopen it. Only a reboot sets _isRebooting, and every
            // dispatched recovery resets the failure counter.
            Assert.Equal(gateStaysClosed, TestReflection.GetField<bool>(service, "_isRecovering"));
            Assert.Equal(rebooting, TestReflection.GetField<bool>(service, "_isRebooting"));
            Assert.Equal(0, service.GetFailedChecks());
        }

        [Theory]
        [InlineData("_isTearingDown")]
        [InlineData("_disposed")]
        [InlineData("_isRebooting")]
        public async Task InitiateRecoveryAsync_BusyFlagSetUnderTheLock_DispatchesNothingAndLeavesTheStateAlone(string flag)
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartService);
            service.SetFailedChecks(3);
            TestReflection.SetField(service, flag, true);

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Put the flag back so the context's Dispose still runs a real teardown.
            TestReflection.SetField(service, flag, false);

            // Assert
            // The guard returns before the recovery state is touched: no action, the gate stays open
            // and the failure counter keeps its value.
            VerifyNoRecoveryActionRan(_ctx);
            Assert.False(TestReflection.GetField<bool>(service, "_isRecovering"));
            Assert.Equal(3, service.GetFailedChecks());
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.StartsWith("Performing recovery action")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public async Task InitiateRecoveryAsync_SemaphoreDisposedInsideTheLock_SwallowsTheReleaseFailureAndStillRecovers()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartService);

            // A capped recovery reads the restart counter inside the lock, which gives the test a point
            // after the wait has succeeded and before the release in the finally
            service.SetRestartAttemptsServiceName("ServyTestService");
            service.SetMaxRestartAttempts(3);

            var semaphore = TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore");
            _ctx.NamedPipesService
                .Setup(p => p.GetRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    semaphore.Dispose();
                    return Task.FromResult(new Servy.Core.DTOs.RestartAttemptsDto { Attempts = 0, UpdatedAtUtc = DateTime.UtcNow });
                });

            // Act
            var thrown = await Record.ExceptionAsync(() => service.InvokeInitiateRecoveryAsync());

            // Assert
            // Without the catch around the release, the ObjectDisposedException leaves the method
            // before the recovery action is dispatched
            Assert.Null(thrown);
            _ctx.Helper.Verify(h => h.RestartService("ServyTestService", It.IsAny<IServyLogger?>()), Times.Once);
        }

        /// <summary>
        /// Makes the helper mock behave like the production <see cref="IServiceHelper.RestartProcess"/>:
        /// it runs <paramref name="beforeRelaunch"/>, standing in for the stop of the old process and the
        /// descendant sweep, and then invokes the relaunch callback it was handed.
        /// </summary>
        /// <param name="ctx">The context holding the helper mock.</param>
        /// <param name="beforeRelaunch">What happens between the stop of the old process and the relaunch.</param>
        /// <returns>
        /// The values the relaunch callback returned, one per invocation, in order: <c>true</c> for a launch,
        /// <c>false</c> for a skipped one.
        /// </returns>
        private static List<bool> SetupRestartProcessInvokingTheCallback(ServiceTestContext ctx, Action beforeRelaunch)
        {
            var results = new List<bool>();
            ctx.Helper
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
                .Callback(new Action<IProcessWrapper, StartProcessCallback, string, string, string, List<EnvironmentVariable>, bool, IServyLogger, int, CancellationToken>(
                    (process, startProcess, exePath, arguments, workingDirectory, environmentVariables, allowOverriddenRuntimeVars, logger, stopTimeoutMs, cancellationToken) =>
                    {
                        beforeRelaunch();
                        results.Add(startProcess(exePath, arguments, workingDirectory, environmentVariables ?? new List<EnvironmentVariable>(), allowOverriddenRuntimeVars, cancellationToken));
                    }));
            return results;
        }

        /// <summary>
        /// Creates a child process mock that starts successfully, for the relaunch tests.
        /// </summary>
        /// <returns>The mock.</returns>
        private static Mock<IProcessWrapper> CreateStartableProcess()
        {
            var process = new Mock<IProcessWrapper>();
            process.Setup(p => p.Id).Returns(4242);
            process.Setup(p => p.Start()).Returns(true);
            return process;
        }

        [Fact]
        public async Task InitiateRecoveryAsync_StopCancelsTheTokenDuringTheAttemptsRead_DispatchesNothing()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartService);
            service.SetRestartAttemptsServiceName("ServyTestService");
            service.SetMaxRestartAttempts(3);

            // Handed to the service, whose teardown disposes it
            var cancellationSource = new CancellationTokenSource();
            TestReflection.SetField(service, "_cancellationSource", cancellationSource);

            // The stop arrives while the host round trip is in flight: it cancels the service's token and
            // the read surfaces that cancellation, which GetRestartAttemptsAsync turns into 0. No teardown
            // flag is set, so only the cancelled token tells that 0 apart from a real count.
            _ctx.NamedPipesService
                .Setup(p => p.GetRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((name, ct) =>
                {
                    cancellationSource.Cancel();
                    throw new OperationCanceledException(ct);
                });

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Assert
            // Treating the cancelled read as "no attempts yet" would dispatch the action as attempt 1
            VerifyNoRecoveryActionRan(_ctx);
            Assert.False(TestReflection.GetField<bool>(service, "_isRecovering"));
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.StartsWith("Performing recovery action")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public async Task InitiateRecoveryAsync_TeardownBeginsDuringTheAttemptsSave_DispatchesNothing()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartService);
            service.SetRestartAttemptsServiceName("ServyTestService");
            service.SetMaxRestartAttempts(3);

            _ctx.NamedPipesService
                .Setup(p => p.GetRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Servy.Core.DTOs.RestartAttemptsDto { Attempts = 0, UpdatedAtUtc = DateTime.UtcNow }));

            // Teardown does not take the health-check semaphore, so it can begin after the read has been
            // checked: here it starts while the incremented counter is being saved
            _ctx.NamedPipesService
                .Setup(p => p.UpdateRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns<string, int, CancellationToken>((name, attempts, ct) =>
                {
                    TestReflection.SetField(service, "_isTearingDown", true);
                    return Task.FromResult(attempts);
                });

            // Act
            await service.InvokeInitiateRecoveryAsync();

            // Put the flag back so the context's Dispose still runs a real teardown.
            TestReflection.SetField(service, "_isTearingDown", false);

            // Assert
            // The save ran, so the stop arrived after the read; the re-check before the dispatch is
            // what keeps the action from running
            _ctx.NamedPipesService.Verify(p => p.UpdateRestartAttemptsAsync("ServyTestService", 1, It.IsAny<CancellationToken>()), Times.Once);
            VerifyNoRecoveryActionRan(_ctx);
        }

        [Fact]
        public void ExecuteRecoveryAction_TeardownBeganBeforeTheRelaunch_SkipsStartingANewChild()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartProcess);

            var newProcess = CreateStartableProcess();
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(newProcess.Object);

            // The stop begins while the helper is stopping the old process, before it calls back
            var relaunchResults = SetupRestartProcessInvokingTheCallback(_ctx, () => TestReflection.SetField(service, "_isTearingDown", true));

            // Act
            service.InvokeExecuteRecoveryAction(1);

            // Put the flag back so the context's Dispose still runs a real teardown.
            TestReflection.SetField(service, "_isTearingDown", false);

            // Assert
            // A child started now would outlive the service: teardown has already passed the point where
            // it kills _childProcess
            _ctx.ProcessFactory.Verify(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()), Times.Never);
            newProcess.Verify(p => p.Start(), Times.Never);
            _ctx.Logger.Verify(l => l.Info("Recovery relaunch skipped: the service is stopping.", It.IsAny<Exception>()), Times.Once);

            // The callback reports the skip, so the helper does not log "Process restarted." for a child
            // that was never started
            Assert.Equal(new[] { false }, relaunchResults);
        }

        [Fact]
        public void ExecuteRecoveryAction_RestartProcessCallback_RelaunchesWhileHoldingTheTeardownLock()
        {
            // Arrange
            var service = _ctx.Build();
            ArrangeUnlimitedRecovery(service, RecoveryAction.RestartProcess);

            var teardownLock = TestReflection.GetField<object>(service, "_teardownLock");
            var lockHeldAtCreate = false;

            var newProcess = CreateStartableProcess();
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(() =>
                {
                    lockHeldAtCreate = Monitor.IsEntered(teardownLock);
                    return newProcess.Object;
                });

            var relaunchResults = SetupRestartProcessInvokingTheCallback(_ctx, () => { });

            // Act
            service.InvokeExecuteRecoveryAction(1);

            // Assert
            // Holding the lock ExecuteTeardown runs under makes the relaunch and teardown mutually
            // exclusive, so a stop either waits and then kills the new child or makes the relaunch skip
            Assert.True(lockHeldAtCreate);
            Assert.Same(newProcess.Object, service.GetChildProcess());

            // The callback reports the launch, so the helper logs "Process restarted."
            Assert.Equal(new[] { true }, relaunchResults);
        }

        public void Dispose() => _ctx.Dispose();
    }
}
