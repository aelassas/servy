using Moq;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Covers the exit-outcome arms of <c>OnProcessExited</c> and <c>EvaluateExitOutcome</c>:
    /// the clean-exit recovery decision, the scheduled recovery that follows it, and the two
    /// teardown races around the health-check semaphore wait.
    /// </summary>
    public class ProcessExitOutcomeTests : IDisposable
    {
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        /// <summary>
        /// Builds start options whose heartbeat interval pins the recovery scheduling delay to its
        /// floor, <see cref="AppConfig.RecoverySchedulingDelayMs"/>, so a test that waits for the
        /// scheduled recovery waits the shortest window the production formula allows.
        /// </summary>
        private static StartOptions CreateOptionsWithMinimumRecoveryDelay(bool recoveryOnCleanExit)
        {
            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.RecoveryOnCleanExit = recoveryOnCleanExit;

            // delay = Max(HeartbeatIntervalInSeconds * 1000 - RecoverySchedulingDelayMs, RecoverySchedulingDelayMs)
            options.HeartbeatIntervalInSeconds = 1;

            return options;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnProcessExited_CleanExit_RecoveryOnCleanExitDecidesBetweenRecoveryAndStop(bool recoveryOnCleanExit)
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit);
            TestReflection.SetField(service, "_options", options);

            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.None);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(0);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            // The two log lines are mutually exclusive: the conjunct under test picks exactly one.
            _ctx.Logger.Verify(l => l.Info(
                    "[OnProcessExited] Child process exited successfully (Code 0). RecoveryOnCleanExit is ENABLED. Checking recovery...",
                    It.IsAny<Exception>()),
                recoveryOnCleanExit ? Times.Once() : Times.Never());

            _ctx.Logger.Verify(l => l.Info(
                    "[OnProcessExited] Child process exited successfully (Code 0). Service will stop.",
                    It.IsAny<Exception>()),
                recoveryOnCleanExit ? Times.Never() : Times.Once());

            // The failure counter is the state half: only the recovery arm registers a failure,
            // so this fails even if both log lines were to be reworded.
            Assert.Equal(recoveryOnCleanExit ? 1 : 0, service.GetFailedChecks());

            // And only the recovery arm schedules recovery instead of stopping the service.
            _ctx.Logger.Verify(l => l.Info(
                    It.Is<string>(s => s.Contains("[OnProcessExited] Failure threshold reached. Scheduling recovery in")),
                    It.IsAny<Exception>()),
                recoveryOnCleanExit ? Times.Once() : Times.Never());
        }

        [Fact]
        public void OnProcessExited_CleanExitWithRecoveryActionDisabled_StopsDespiteRecoveryOnCleanExit()
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: true);
            TestReflection.SetField(service, "_options", options);

            // RecoveryOnCleanExit is set, but the recovery action itself is not enabled and this is
            // not a health-check pass, so the second conjunct of the clean-exit test is false.
            service.SetRecoveryActionEnabled(false);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(0);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                "[OnProcessExited] Child process exited successfully (Code 0). Service will stop.",
                It.IsAny<Exception>()), Times.Once);

            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("RecoveryOnCleanExit is ENABLED")),
                It.IsAny<Exception>()), Times.Never);

            Assert.Equal(0, service.GetFailedChecks());
        }

        [Fact]
        public void OnProcessExited_FailureThresholdReached_RunsTheScheduledRecoveryAndReopensTheGate()
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: false);
            TestReflection.SetField(service, "_options", options);

            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);
            service.SetMaxRestartAttempts(0); // unlimited: recovery does not read the attempts file

            using var recovered = new ManualResetEventSlim(false);
            _ctx.Helper
                .Setup(h => h.RestartProcess(
                    It.IsAny<IProcessWrapper?>(),
                    It.IsAny<StartProcessCallback>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<List<EnvironmentVariable>>(),
                    It.IsAny<IServyLogger?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => recovered.Set());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            // The existing exit tests stop at the "Scheduling recovery in ..." line; this one waits
            // for the scheduled task to elapse and actually reach InitiateRecoveryAsync.
            Assert.True(recovered.Wait(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken),
                $"The scheduled recovery did not run within {TestTimeouts.CiGenerousSeconds}s.");

            _ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper?>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(),
                It.IsAny<IServyLogger?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // RestartProcess is not terminal for this instance, so the recovery gate reopens in the
            // finally block. Without that reset the next failed check would be swallowed silently.
            SpinWait.SpinUntil(() => !TestReflection.GetField<bool>(service, "_isRecovering"), TestTimeouts.CiGenerous);
            Assert.False(TestReflection.GetField<bool>(service, "_isRecovering"));
        }

        [Fact]
        public void OnProcessExited_SemaphoreDisposedBeforeWait_LogsTeardownInProgressAndEvaluatesNothing()
        {
            // Arrange
            var service = _ctx.Build();

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(0);
            service.SetChildProcess(mockProcess.Object);

            // Teardown disposes the health-check semaphore while the handler is between its
            // fast-path check and the wait.
            TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore").Dispose();

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                "OnProcessExited: Semaphore disposed during wait. Teardown in progress.",
                It.IsAny<Exception>()), Times.Once);

            // The exit was never evaluated, so nothing downstream of the wait ran.
            mockProcess.Verify(p => p.ExitCode, Times.Never);
            _ctx.Logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void OnProcessExited_WaitAlreadyCancelled_LogsProcessExitCancelledAndEvaluatesNothing()
        {
            // Arrange
            var service = _ctx.Build();

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(0);
            service.SetChildProcess(mockProcess.Object);

            var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            TestReflection.SetField(service, "_cancellationSource", cancellationSource);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                "OnProcessExited: Process exit cancelled. Teardown in progress.",
                It.IsAny<Exception>()), Times.Once);

            // The exit was never evaluated, so nothing downstream of the wait ran.
            mockProcess.Verify(p => p.ExitCode, Times.Never);
            _ctx.Logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void OnProcessExited_TeardownBeginsDuringRecoveryDelay_SkipsRecoveryAndReopensTheGate()
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: false);
            TestReflection.SetField(service, "_options", options);

            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);
            service.SetMaxRestartAttempts(0); // unlimited: recovery does not read the attempts file

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // The failure threshold is reached synchronously, which closes the recovery gate and
            // leaves the scheduled recovery waiting out its delay.
            service.InvokeOnProcessExited(null, EventArgs.Empty);
            Assert.True(TestReflection.GetField<bool>(service, "_isRecovering"),
                "The recovery gate did not close, so the scheduled recovery was never armed.");

            // Act
            // Teardown starts before the delay elapses, so the post-delay re-check skips
            // InitiateRecoveryAsync and the hand-off flag stays false.
            TestReflection.SetField(service, "_isTearingDown", true);

            // Assert
            // Only the safety reset in ScheduleRecoveryAsync's own finally can reopen the gate on
            // this path: InitiateRecoveryAsync never ran, so its finally never ran either. Without
            // the reset the gatekeeper stays closed and every later failed check is swallowed.
            Assert.True(SpinWait.SpinUntil(() => !TestReflection.GetField<bool>(service, "_isRecovering"), TestTimeouts.CiGenerous),
                $"The recovery gate was not reopened within {TestTimeouts.CiGenerousSeconds}s after the scheduled recovery was skipped.");

            _ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper?>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(),
                It.IsAny<IServyLogger?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        public void Dispose() => _ctx.Dispose();
    }
}
