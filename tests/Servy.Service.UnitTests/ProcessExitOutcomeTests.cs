using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
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
    /// the early return while the service cannot react, the clean-exit recovery decision, the
    /// exit code of a cleared child wrapper, the <c>_isRecovering</c> gatekeeper, the scheduled
    /// recovery (its delay, its run, its skip on teardown and its catch-all), the <c>/fail</c>
    /// heartbeat ping, the cleared persisted process state, the teardown races around the
    /// health-check semaphore wait, the swallowed failure of its release, and the handler's own
    /// catch-all.
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
        public void OnProcessExited_ClearsThePersistedProcessState()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_serviceName", "ServyTest");

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: false);
            TestReflection.SetField(service, "_options", options);

            // A crash below the failure threshold neither stops the service nor schedules a recovery, so
            // Cleanup() does not run and the exit handler's own clear is the only one observed.
            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetMaxFailedChecks(5);
            service.SetFailedChecks(0);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.NamedPipesService.Verify(n => n.UpdateRuntimeState(
                "ServyTest",
                It.Is<ServiceRuntimeStateDto>(s => s.Pid == null && s.ActiveStdoutPath == null && s.ActiveStderrPath == null),
                It.IsAny<CancellationToken>()), Times.Once);
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
            service.SetMaxRestartAttempts(0); // unlimited: recovery does not read the attempts counter

            using var recovered = new ManualResetEventSlim(false);
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
                It.IsAny<bool>(),
                It.IsAny<IServyLogger?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // RestartProcess is not terminal for this instance, so the recovery gate reopens in the
            // finally block. Without that reset the next failed check would be swallowed silently.
            SpinWait.SpinUntil(() => !TestReflection.GetField<bool>(service, "_isRecovering"), TestTimeouts.CiGenerous);
            Assert.False(TestReflection.GetField<bool>(service, "_isRecovering"));
        }

        [Fact]
        public void OnProcessExited_SemaphoreDisposedInsideTheLock_SwallowsTheReleaseFailureAndStillSchedulesRecovery()
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: false);
            TestReflection.SetField(service, "_options", options);

            // The scheduled recovery must not outlive the test: cancel its delay when the test ends.
            using var cancellation = new CancellationTokenSource();
            TestReflection.SetField(service, "_cancellationSource", cancellation);

            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);

            var semaphore = TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore");

            var mockProcess = new Mock<IProcessWrapper>();
            // Dispose only once the wait has already succeeded, so the release in the finally
            // is the first call that meets a disposed semaphore
            mockProcess.Setup(p => p.ExitCode).Returns(() => { semaphore.Dispose(); return 1; });
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            // The exit was acted on: without the catch around the release, the ObjectDisposedException
            // would escape into the outer handler, which reports a teardown and schedules nothing
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.StartsWith("[OnProcessExited] Failure threshold reached. Scheduling recovery in")),
                It.IsAny<Exception>()), Times.Once);
            _ctx.Logger.Verify(l => l.Info(
                "OnProcessExited: Semaphore disposed during wait. Teardown in progress.",
                It.IsAny<Exception>()), Times.Never);

            cancellation.Cancel();
        }

        [Fact]
        public void OnProcessExited_ScheduledRecoveryThrows_LogsTheUnexpectedError()
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: false);
            TestReflection.SetField(service, "_options", options);

            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);

            // The restart quota is already used up, so the scheduled recovery logs that it is stopping the
            // service. That line is outside every catch of InitiateRecoveryAsync, so a sink that fails on it
            // makes the recovery itself throw.
            service.SetRestartAttemptsServiceName("ServyTestService");
            service.SetMaxRestartAttempts(2);
            FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 2);

            var failure = new InvalidOperationException("log sink failed");
            _ctx.Logger
                .Setup(l => l.Error(It.Is<string>(s => s.StartsWith("Maximum restart attempts reached")), It.IsAny<Exception>()))
                .Throws(failure);

            using var logged = new ManualResetEventSlim(false);
            _ctx.Logger
                .Setup(l => l.Error("Unexpected error during scheduled recovery: log sink failed", It.IsAny<Exception>()))
                .Callback(() => logged.Set());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            Assert.True(logged.Wait(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken),
                $"The failed scheduled recovery was not logged within {TestTimeouts.CiGenerousSeconds}s.");
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
            service.SetMaxRestartAttempts(0); // unlimited: recovery does not read the attempts counter

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
                It.IsAny<bool>(),
                It.IsAny<IServyLogger?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void OnProcessExited_UnexpectedFailureInsideTheHandler_IsLoggedAndDoesNotEscape()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var failure = new InvalidOperationException("log sink failed");
            _ctx.Logger
                .Setup(l => l.Info("Child process exit detected via event.", It.IsAny<Exception>()))
                .Throws(failure);

            // Act: the throw happens before the handler's first await, so the handler has finished when the call returns
            var escaped = Record.Exception(() => service.InvokeOnProcessExited(null, EventArgs.Empty));

            // Assert
            Assert.Null(escaped);
            _ctx.Logger.Verify(l => l.Error("OnProcessExited failed.", failure), Times.Once);

            // ... and the failed handler neither stopped the service nor scheduled a recovery
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("Scheduling recovery")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public async Task OnProcessExited_ErrorExitWithRecoveryDisabled_EmitsTheFailFlagPing()
        {
            // Arrange
            var service = _ctx.Build();
            var pingLogged = _ctx.CaptureHeartbeatPings();

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(42);
            service.SetChildProcess(mockProcess.Object);

            // The error exit stops the service right after queueing the ping, and OnStop shuts the service's logger
            // down once the teardown returns. The ping logs from a Task.Run that reads that logger when it runs, so
            // a pool that is busy for a few milliseconds lets the shutdown win and the line is never logged. Hold
            // the teardown until the ping line is out.
            service.OnStoppedForTest += () => pingLogged.Task.Wait(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            var completed = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));
            Assert.Same(pingLogged.Task, completed);
            Assert.Contains("(flag: fail)", await pingLogged.Task);
        }

        [Fact]
        public async Task OnProcessExited_FailureThresholdReachedFromTheExitEvent_EmitsTheFailFlagPing()
        {
            // Arrange
            var service = _ctx.Build();
            var pingLogged = _ctx.CaptureHeartbeatPings();

            // Exit code 0 with RecoveryOnCleanExit enabled: the exit is a recovery trigger, not a stop,
            // so only the needsRecovery half of the condition can send the ping.
            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: true);
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
            var completed = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));
            Assert.Same(pingLogged.Task, completed);
            Assert.Contains("(flag: fail)", await pingLogged.Task);
        }

        [Fact]
        public void OnProcessExited_CleanExitThatStopsTheService_SendsNoFailFlagPing()
        {
            // Arrange
            var service = _ctx.Build();
            var pingLogged = _ctx.CaptureHeartbeatPings();

            // Each service pings its own URL, and MaskUrl keeps a 5-character path hint, so the
            // log line says which service sent it: "clean" for the service under test, "mark-" for the marker.
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions("https://127.0.0.1:1/clean-uuid"));

            var cleanProcess = new Mock<IProcessWrapper>();
            cleanProcess.Setup(p => p.ExitCode).Returns(0);
            service.SetChildProcess(cleanProcess.Object);

            // The clean exit stops the service, and the stop shuts the service's logger down. A wrongly sent ping
            // logs from a Task.Run that reads that logger, so hold the teardown for the observation window:
            // a wrong ping then has its chance to be logged while the logger is still there.
            service.OnStoppedForTest += () => pingLogged.Task.Wait(TestTimeouts.NegativeObservationWindow, TestContext.Current.CancellationToken);

            // A second service that shares the logger raises an error exit (a stopped service no longer
            // handles exits): its ping is the marker that every earlier one has had its chance to log.
            var failingService = _ctx.Build();
            TestReflection.SetField(failingService, "_options", ServiceTestContext.CreateDefaultStartOptions("https://127.0.0.1:1/mark-uuid"));
            var failingProcess = new Mock<IProcessWrapper>();
            failingProcess.Setup(p => p.ExitCode).Returns(42);
            failingService.SetChildProcess(failingProcess.Object);

            // Same race for the marker: hold its teardown until its own line is captured. The first ping
            // may be a wrong one, which completes pingLogged on its own, so wait for the marker itself.
            failingService.OnStoppedForTest += () => SpinWait.SpinUntil(
                () => _ctx.HeartbeatPingLines.Any(l => l.Contains("/mark-")), TestTimeouts.CiGenerous);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);
            failingService.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            var diagnostics = "Heartbeat ping lines: [" + string.Join(" | ", _ctx.HeartbeatPingLines) + "]; logger invocations: ["
                + string.Join(" | ", _ctx.Logger.Invocations.Select(i => i.ToString())) + "]";
            Assert.True(_ctx.HeartbeatPingLines.Any(l => l.Contains("/mark-") && l.Contains("(flag: fail)")),
                "The marker service's fail-flag ping was never logged. " + diagnostics);

            // Only the error exit pinged; the clean exit that stopped the service sent nothing.
            Assert.DoesNotContain(_ctx.HeartbeatPingLines, l => l.Contains("/clean"));
        }

        [Theory]
        [InlineData(1, 5)]    // interval below the buffer: the 5 s floor
        [InlineData(10, 5)]   // interval - buffer equals the floor
        [InlineData(30, 25)]  // interval above the buffer: interval minus 5 s
        public void OnProcessExited_FailureThresholdReached_SchedulesRecoveryAfterTheHeartbeatIntervalMinusTheBuffer(
            int heartbeatIntervalInSeconds, int expectedDelaySeconds)
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: false);
            options.HeartbeatIntervalInSeconds = heartbeatIntervalInSeconds;
            TestReflection.SetField(service, "_options", options);

            // The scheduled recovery must not outlive the test: cancel its delay when the test ends.
            using var cancellation = new CancellationTokenSource();
            TestReflection.SetField(service, "_cancellationSource", cancellation);

            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                $"[OnProcessExited] Failure threshold reached. Scheduling recovery in {expectedDelaySeconds}s...",
                It.IsAny<Exception>()), Times.Once);

            cancellation.Cancel();
        }

        [Theory]
        [InlineData("_isTearingDown")]
        [InlineData("_disposed")]
        [InlineData("_isRebooting")]
        [InlineData(null)]
        public void OnProcessExited_ServiceNotInAStateToReact_ReturnsBeforeLoggingTheExit(string? stateFlag)
        {
            // Arrange
            var service = _ctx.Build();

            // A null flag is the "options not initialized yet" row: nothing is configured, so there is
            // no recovery policy to evaluate. Every other row has options and one blocking flag.
            if (stateFlag != null)
            {
                TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());
                TestReflection.SetField(service, stateFlag, true);
            }

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // Act
            // The guard runs before the handler's first await, so it is done when the call returns
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Put the flag back so the context's Dispose still runs a real teardown.
            if (stateFlag != null)
            {
                TestReflection.SetField(service, stateFlag, false);
            }

            // Assert
            _ctx.Logger.Verify(l => l.Info("Child process exit detected via event.", It.IsAny<Exception>()), Times.Never);
            mockProcess.Verify(p => p.ExitCode, Times.Never);
        }

        [Fact]
        public void OnProcessExited_ChildProcessAlreadyCleared_TreatsTheExitCodeAsMinusOne()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());
            service.SetRecoveryActionEnabled(false);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);

            // The exit event can outlive the wrapper field, which a stop or a restart clears; there is
            // then no exit code to read.
            service.SetChildProcess(null!);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            // The fallback code -1 is a failure, so recovery being disabled stops the service with an error
            _ctx.Logger.Verify(l => l.Error(
                "[OnProcessExited] Process exited with code -1 (0xFFFFFFFF) and recovery is disabled.",
                It.IsAny<Exception>()), Times.Once);

            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("exited successfully")), It.IsAny<Exception>()), Times.Never);

            // ... and the -1 comes from the null arm, not from a read that threw
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.Contains("Failed to get exit code")), It.IsAny<Exception>()), Times.Never);
        }

        [Theory]
        [InlineData("_isTearingDown")]
        [InlineData("_disposed")]
        public async Task OnProcessExited_TeardownBeginsWhileWaitingForTheLock_EvaluatesNothing(string flag)
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // Hold the lock, so the handler passes its fast-path check with every flag clear and parks on the wait.
            var semaphore = TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore");
            await semaphore.WaitAsync(TestContext.Current.CancellationToken);
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Act
            // Teardown starts while the handler is parked, then the lock is handed over.
            TestReflection.SetField(service, flag, true);
            semaphore.Release();

            // The handler is async void: it is done with the guarded region once it has given the lock back.
            await Helper.WaitUntilAsync(() => semaphore.CurrentCount == 1, TestTimeouts.CiGenerous,
                cancellationToken: TestContext.Current.CancellationToken);

            // Put the flag back so the context's Dispose still runs a real teardown.
            TestReflection.SetField(service, flag, false);

            // Assert
            // Only the re-check inside the lock can stop this handler: the exit is never evaluated.
            mockProcess.Verify(p => p.ExitCode, Times.Never);
        }

        [Fact]
        public void OnProcessExited_RecoveryAlreadyInProgress_CountsNoFailureAndSchedulesNoSecondRecovery()
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreateOptionsWithMinimumRecoveryDelay(recoveryOnCleanExit: false);
            TestReflection.SetField(service, "_options", options);

            // If a recovery were scheduled after all, it must not outlive the test.
            using var cancellation = new CancellationTokenSource();
            TestReflection.SetField(service, "_cancellationSource", cancellation);

            service.SetRecoveryActionEnabled(true);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetMaxFailedChecks(1);
            service.SetFailedChecks(0);

            // A recovery is already running when the child's exit event arrives.
            TestReflection.SetField(service, "_isRecovering", true);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(1);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            // The exit was evaluated, but the gatekeeper kept it from counting as a new failure:
            // with one allowed failure, counting it would have scheduled a second recovery.
            mockProcess.Verify(p => p.ExitCode, Times.Once);
            Assert.Equal(0, service.GetFailedChecks());
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.Contains("Health check failed")), It.IsAny<Exception>()), Times.Never);
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.StartsWith("[OnProcessExited] Failure threshold reached")), It.IsAny<Exception>()), Times.Never);

            cancellation.Cancel();
        }

        public void Dispose() => _ctx.Dispose();
    }
}
