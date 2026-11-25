using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Testing;
using System.Diagnostics;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Covers the teardown arms that the lifecycle tests never reach. In <c>Service.Cleanup()</c>: the
    /// cleared runtime state, the redirected-stream unhook, the <c>Exited</c> detach before the child is
    /// stopped, the pre-stop failure policy log, the tracked-hook cleanup loop, the output-writer dispose
    /// failures (including one writer shared by stdout and stderr) and the catch-all. In the
    /// <c>finally</c> of <c>Service.ExecuteTeardown</c>: the cancellation-source and semaphore dispose failures.
    /// </summary>
    public class ServiceCleanupTests : IDisposable
    {
        private const string PreStopExe = @"C:\hooks\prestop.exe";

        private readonly ServiceTestContext _ctx;
        private readonly TestableService _service;

        private readonly Mock<IStreamWriter> _mockStdoutWriter;
        private readonly Mock<IStreamWriter> _mockStderrWriter;
        private readonly Mock<ITimer> _mockTimer;
        private readonly Mock<IProcessWrapper> _mockProcess;

        /// <summary>
        /// Wires a service whose child process, writers and timer are mocks, with no stream
        /// redirection and no pre-stop hook configured; each test opts into the arm it exercises.
        /// </summary>
        public ServiceCleanupTests()
        {
            _ctx = new ServiceTestContext();

            _mockStdoutWriter = new Mock<IStreamWriter>();
            _mockStderrWriter = new Mock<IStreamWriter>();
            _mockTimer = new Mock<ITimer>();
            _mockProcess = new Mock<IProcessWrapper>();

            _mockProcess.Setup(p => p.StartInfo).Returns(new ProcessStartInfo());
            _mockProcess.Setup(p => p.Start()).Returns(true);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            _ctx.StreamWriterFactory
                .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns((string path, bool enableSizeRotation, long size, bool enableDateRotation, DateRotationType dateRotationType, int maxRotations, bool useLocalTimeForRotation) =>
                    path.Contains("stdout") ? _mockStdoutWriter.Object
                    : path.Contains("stderr") ? _mockStderrWriter.Object
                    : null);

            _ctx.TimerFactory.Setup(f => f.Create(It.IsAny<double>())).Returns(_mockTimer.Object);
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(_mockProcess.Object);

            _service = _ctx.Build();
        }

        /// <summary>
        /// Builds the options a cleanup test starts from, and points the two log paths at names the
        /// writer factory above recognises so the dispose arms have real writers to fail on.
        /// </summary>
        private static StartOptions CreateOptions(
            string? preStopExecutablePath = null,
            bool preStopLogAsError = false) => new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                StdoutPath = @"C:\Logs\stdout.log",
                StderrPath = @"C:\Logs\stderr.log",
                StartupDirectory = @"C:\svc",
                PreStopExecutablePath = preStopExecutablePath,
                PreStopStartupDirectory = @"C:\hooks",
                PreStopTimeoutInSeconds = 5,
                PreStopLogAsError = preStopLogAsError,
            };

        /// <summary>
        /// Stubs the start path and returns the scoped logger <c>OnStart</c> installs, which is the
        /// one every <c>Cleanup</c> line writes to.
        /// </summary>
        private Mock<IServyLogger> SetupStart(StartOptions options)
            => _ctx.ArrangeStart(options);

        /// <summary>
        /// Replaces the service's tracked-hook list content, which teardown's cleanup loop walks.
        /// </summary>
        private void SetTrackedHooks(params Hook[] hooks) => SetTrackedHooks(_service, hooks);

        /// <summary>
        /// Replaces the tracked-hook list content of a specific service, for the tests that build
        /// their own subclass instead of using the fixture's <see cref="_service"/>.
        /// </summary>
        /// <param name="service">The service whose tracked-hook list is replaced.</param>
        /// <param name="hooks">The hooks teardown's cleanup loop should walk.</param>
        private static void SetTrackedHooks(TestableService service, params Hook[] hooks)
        {
            var tracked = TestReflection.GetField<List<Hook>>(service, "_trackedHooks");
            tracked.Clear();
            tracked.AddRange(hooks);
        }

        private List<Hook> GetTrackedHooks() => TestReflection.GetField<List<Hook>>(_service, "_trackedHooks");

        [Fact]
        public void Cleanup_RedirectedStreams_CancelsBothReadsAndDetachesBothHandlers()
        {
            // Arrange
            var options = CreateOptions();
            SetupStart(options);
            _mockProcess.Setup(p => p.StartInfo)
                .Returns(new ProcessStartInfo { RedirectStandardOutput = true, RedirectStandardError = true });
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            _mockProcess.Verify(p => p.CancelOutputRead(), Times.Once);
            _mockProcess.Verify(p => p.CancelErrorRead(), Times.Once);
            _mockProcess.VerifyRemove(p => p.OutputDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
            _mockProcess.VerifyRemove(p => p.ErrorDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
        }

        [Fact]
        public void Cleanup_ClearsThePersistedProcessState()
        {
            // Arrange
            var options = CreateOptions();
            SetupStart(options);
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            _ctx.NamedPipesService.Verify(n => n.UpdateRuntimeState(
                "Test",
                It.Is<ServiceRuntimeStateDto>(s => s.Pid == null && s.ActiveStdoutPath == null && s.ActiveStderrPath == null),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Cleanup_StreamsNotRedirected_NeitherReadIsCancelled()
        {
            // Arrange
            var options = CreateOptions();
            SetupStart(options);
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            // The StartInfo flags are the only thing gating the unhook, so the default fixture
            // pins the negative arm of the same two ifs the test above pins positively.
            _mockProcess.Verify(p => p.CancelOutputRead(), Times.Never);
            _mockProcess.Verify(p => p.CancelErrorRead(), Times.Never);
            _mockProcess.VerifyRemove(p => p.OutputDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Never);
        }

        [Fact]
        public void Cleanup_CancelOutputReadThrows_StillDetachesAndFinishesTheTeardown()
        {
            // Arrange
            var options = CreateOptions();
            SetupStart(options);
            _mockProcess.Setup(p => p.StartInfo)
                .Returns(new ProcessStartInfo { RedirectStandardOutput = true, RedirectStandardError = true });
            _mockProcess.Setup(p => p.CancelOutputRead()).Throws(new InvalidOperationException("stream already stopped"));
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            // The swallowing catch is what keeps the rest of the teardown running: the handler is
            // still detached, the error stream is still cancelled and the writers are still disposed.
            _mockProcess.VerifyRemove(p => p.OutputDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
            _mockProcess.Verify(p => p.CancelErrorRead(), Times.Once);
            _mockStdoutWriter.Verify(w => w.Dispose(), Times.Once);
            _mockStderrWriter.Verify(w => w.Dispose(), Times.Once);
        }

        [Fact]
        public void Cleanup_DetachesTheExitedHandlerBeforeTheChildIsStopped()
        {
            // Arrange
            var options = CreateOptions();
            SetupStart(options);
            var detachedBeforeStop = false;
            var detached = false;
            _mockProcess.SetupRemove(p => p.Exited -= It.IsAny<EventHandler>()).Callback(() => detached = true);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true).Callback(() => detachedBeforeStop = detached);
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            _mockProcess.VerifyRemove(p => p.Exited -= It.IsAny<EventHandler>(), Times.Once);
            Assert.True(detachedBeforeStop, "the Exited handler was still attached when the child was stopped");
        }

        [Fact]
        public void Cleanup_CancelErrorReadThrows_StillDetachesAndFinishesTheTeardown()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _mockProcess.Setup(p => p.StartInfo)
                .Returns(new ProcessStartInfo { RedirectStandardOutput = true, RedirectStandardError = true });
            _mockProcess.Setup(p => p.CancelErrorRead()).Throws(new InvalidOperationException("stream already stopped"));
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            // The swallowing catch is what keeps the rest of the teardown running: the stderr handler
            // is still detached and the writers are still disposed instead of the exception landing
            // in Cleanup's catch-all.
            _mockProcess.VerifyRemove(p => p.ErrorDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
            _mockStdoutWriter.Verify(w => w.Dispose(), Times.Once);
            _mockStderrWriter.Verify(w => w.Dispose(), Times.Once);
            scopedLogger.Verify(l => l.Error(
                It.Is<string>(s => s.StartsWith("Failed to kill child process")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void Cleanup_UnexpectedFailure_IsLoggedAndTheTeardownStillCompletes()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // StartInfo is read after the kill sequence, inside Cleanup's try and outside every inner
            // catch, so it is armed only after the start path is done.
            _mockProcess.Setup(p => p.StartInfo).Throws(new InvalidOperationException("handle is gone"));

            // Act
            _service.Stop();

            // Assert
            scopedLogger.Verify(l => l.Error("Failed to kill child process: handle is gone", It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Error(
                It.Is<string>(s => s.StartsWith("Teardown error during")), It.IsAny<Exception>()), Times.Never);

            // The finally still releases the child, and the teardown is recorded as done.
            _mockProcess.Verify(p => p.Dispose(), Times.AtLeastOnce);
            Assert.Null(_service.GetChildProcess());
            Assert.True(TestReflection.GetField<bool>(_service, "_disposed"));
        }

        [Theory]
        [InlineData(3, true, 1)]   // failed, policy says error: the line is written
        [InlineData(3, false, 0)]  // failed, policy says warning: no manual-intervention error
        [InlineData(0, true, 0)]   // succeeded: nothing to report, whatever the policy
        public void Cleanup_PreStop_WritesTheManualInterventionErrorOnlyForAFailureUnderTheErrorPolicy(
            int preStopExitCode, bool preStopLogAsError, int expectedErrors)
        {
            // Arrange
            var options = CreateOptions(preStopExecutablePath: PreStopExe, preStopLogAsError: preStopLogAsError);
            var scopedLogger = SetupStart(options);

            var preStop = new Mock<IProcessWrapper>();
            preStop.Setup(p => p.StartInfo).Returns(new ProcessStartInfo());
            preStop.Setup(p => p.Start()).Returns(true);
            preStop.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            preStop.Setup(p => p.ExitCode).Returns(preStopExitCode);
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns((ProcessStartInfo psi, IServyLogger logger) =>
                    psi.FileName == PreStopExe ? preStop.Object : _mockProcess.Object);

            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            scopedLogger.Verify(l => l.Error(
                "Pre-stop failed. Manual intervention may be required, but continuing cleanup to avoid orphans.",
                It.IsAny<Exception>()), Times.Exactly(expectedErrors));

            // The contract the message states: the main child is still killed whatever the pre-stop hook did.
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void Cleanup_WriterDisposeFailureCannotBeLogged_FallsBackToTheOutputWritersWarning()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _mockStdoutWriter.Setup(w => w.Dispose()).Throws(new IOException("stdout handle is gone"));

            // The per-writer catch logs through the same logger a failing sink would also refuse, and
            // that second failure is what the dispose block's own catch exists for.
            scopedLogger
                .Setup(l => l.Warn("Failed to dispose stdout writer: stdout handle is gone", It.IsAny<Exception>()))
                .Throws(new InvalidOperationException("log sink failed"));
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            // 1. The escaped logging failure is reported once, by the dispose block's catch
            scopedLogger.Verify(l => l.Warn("Failed to dispose output writers: log sink failed", It.IsAny<Exception>()), Times.Once);

            // 2. ... so it never reaches the outer catch that reports a failed kill sequence
            scopedLogger.Verify(l => l.Error(It.Is<string>(s => s.StartsWith("Failed to kill child process")), It.IsAny<Exception>()), Times.Never);
        }

        [Theory]
        [InlineData("_restartAttemptsSemaphore")]
        [InlineData("_healthCheckSemaphore")]
        public void Teardown_SemaphoreDisposeThrows_WarnsAndStillMarksTheServiceDisposed(string semaphoreField)
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // SemaphoreSlim.Dispose never throws on its own, so a subclass whose disposal fails stands in
            // for one that does. Only one of the two semaphores is replaced per row.
            TestReflection.SetField(_service, semaphoreField, new DisposeThrowingSemaphore("semaphore gone"));

            // Act
            _service.Stop();

            // Assert
            // 1. The failure is reported against the semaphore that threw, and only that one
            scopedLogger.Verify(l => l.Warn($"Disposing {semaphoreField} failed: semaphore gone", It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.StartsWith("Disposing _") && s.Contains("failed")), It.IsAny<Exception>()), Times.Once);

            // 2. The teardown still completed
            Assert.True(TestReflection.GetField<bool>(_service, "_disposed"));
        }

        [Fact]
        public void Teardown_CancellationSourceDisposeThrows_WarnsAndStillDisposesBothSemaphores()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // CancellationTokenSource.Dispose never throws on its own, so a subclass whose disposal fails stands in.
            TestReflection.SetField(_service, "_cancellationSource", new DisposeThrowingCancellationSource("source gone"));
            var restartSemaphore = TestReflection.GetField<SemaphoreSlim>(_service, "_restartAttemptsSemaphore");
            var healthSemaphore = TestReflection.GetField<SemaphoreSlim>(_service, "_healthCheckSemaphore");

            // Act
            _service.Stop();

            // Assert
            // 1. The failure is reported once, against the cancellation source
            scopedLogger.Verify(l => l.Warn("Disposing _cancellationSource failed: source gone", It.IsAny<Exception>()), Times.Once);

            // 2. The finally kept going: the field is cleared and both semaphores are disposed
            Assert.Null(TestReflection.GetField<CancellationTokenSource?>(_service, "_cancellationSource"));
            Assert.Throws<ObjectDisposedException>(() => restartSemaphore.Wait(0, TestContext.Current.CancellationToken));
            Assert.Throws<ObjectDisposedException>(() => healthSemaphore.Wait(0, TestContext.Current.CancellationToken));

            // 3. The teardown still completed
            Assert.True(TestReflection.GetField<bool>(_service, "_disposed"));
        }

        [Fact]
        public void Cleanup_StdoutAndStderrShareOneWriter_DisposesItExactlyOnce()
        {
            // Arrange
            var options = CreateOptions();
            options.StderrPath = options.StdoutPath;
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // Guard the premise: the start path really stored one writer in both fields.
            Assert.Same(
                TestReflection.GetField<object>(_service, "_stdoutWriter"),
                TestReflection.GetField<object>(_service, "_stderrWriter"));

            // Act
            _service.Stop();

            // Assert
            _mockStdoutWriter.Verify(w => w.Dispose(), Times.Once);
            _mockStderrWriter.Verify(w => w.Dispose(), Times.Never);
            scopedLogger.Verify(l => l.Warn(
                It.Is<string>(s => s.StartsWith("Failed to dispose")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void Cleanup_WriterDisposeThrows_LogsBothWarningsAndStillDisposesTheChildProcess()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _mockStdoutWriter.Setup(w => w.Dispose()).Throws(new IOException("stdout handle is gone"));
            _mockStderrWriter.Setup(w => w.Dispose()).Throws(new IOException("stderr handle is gone"));
            _service.StartForTest();

            // Act
            _service.Stop();

            // Assert
            scopedLogger.Verify(l => l.Warn("Failed to dispose stdout writer: stdout handle is gone", It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Warn("Failed to dispose stderr writer: stderr handle is gone", It.IsAny<Exception>()), Times.Once);

            // The two warnings above are what pin the per-writer catches. The child is disposed by
            // Cleanup's finally whatever the writers do, so this last check only confirms the
            // teardown still completed; it would pass without either catch.
            _mockProcess.Verify(p => p.Dispose(), Times.AtLeastOnce);
        }

        [Fact]
        public void Cleanup_TrackedHookProcessNeverStarted_LogsTheFailureAndStillClearsTheList()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // A Process that was never started throws InvalidOperationException from HasExited,
            // which is the cheapest deterministic way into the loop's catch. The null-process hook
            // in front of it pins the `continue` guard: the loop must reach the second entry.
            using var neverStarted = new Process();
            SetTrackedHooks(
                new Hook { OperationName = null, Process = null },
                new Hook { OperationName = "Pre-Launch", Process = neverStarted });

            // Act
            _service.Stop();

            // Assert
            scopedLogger.Verify(l => l.Error("Cleanup of tracked hook failed.", It.IsAny<Exception>()), Times.Once);

            // The catch must not abort the loop: CleanupTrackedHooks still runs after it.
            Assert.Empty(GetTrackedHooks());
        }

        [Fact]
        public void Cleanup_TrackedHookAlreadyExited_SkipsTheKillPathEntirely()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // A real process that has already exited. Only this test's own PID is touched.
            using var exited = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false, CreateNoWindow = true })!;
            Assert.True(exited.WaitForExit(TestTimeouts.CiGenerousMs), "the helper process never exited");
            exited.WaitForExit();

            SetTrackedHooks(new Hook { OperationName = "Post-Launch", Process = exited });

            // Act
            _service.Stop();

            // Assert
            // HasExited is true, so the kill-and-wait block is skipped. That block logs "Cleaning up
            // orphaned ..." before it attempts the kill, so the absence of that line is what pins the guard.
            scopedLogger.Verify(l => l.Warn(It.Is<string>(m => m.StartsWith("Failed to send Kill signal")), It.IsAny<Exception>()), Times.Never);
            scopedLogger.Verify(l => l.Info(It.Is<string>(m => m.StartsWith("Cleaning up orphaned")), It.IsAny<Exception>()), Times.Never);
            scopedLogger.Verify(l => l.Error("Cleanup of tracked hook failed.", It.IsAny<Exception>()), Times.Never);
            Assert.Empty(GetTrackedHooks());
        }

        [Theory]
        [InlineData("Post-Launch", "Post-Launch")]
        [InlineData("   ", "unnamed")]
        public void Cleanup_TrackedHookStillRunning_KillsItsTreeAndLogsTheCleanup(string operationName, string expectedName)
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // A real, still-running process. Only this test's own PID (and its ping child) is touched.
            using var running = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 60 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true })!;
            var pid = running.Id;
            var childPid = 0;
            var ownedChildren = new List<Process>();

            try
            {
                Assert.False(running.HasExited, "the helper process exited before the test could track it");

                // cmd.exe launches ping.exe asynchronously, so poll until that child is visible, and
                // match it by NAME: a console host can also show up as a child here and it exits with
                // its client even when the kill spares the tree, so it witnesses nothing.
                var sw = Stopwatch.StartNew();
                while (childPid == 0 && sw.ElapsedMilliseconds < TestTimeouts.CiGenerousMs)
                {
                    foreach (var candidate in ProcessExtensions.GetChildren(pid, running.StartTime))
                    {
                        try
                        {
                            if (childPid == 0 && candidate.ProcessName.Equals("ping", StringComparison.OrdinalIgnoreCase))
                            {
                                childPid = candidate.Id;
                            }
                        }
                        catch (InvalidOperationException) { /* the candidate exited while we read its name */ }

                        candidate.Dispose();
                    }

                    if (childPid == 0) Thread.Sleep(50);
                }

                Assert.NotEqual(0, childPid);

                // Our own handle on the child, so the wait below outlives CleanupTrackedHooks disposing
                // the hook's process. WaitForExit waits on the handle and not on the number, so PID
                // reuse cannot make it lie.
                ownedChildren.Add(Process.GetProcessById(childPid));

                SetTrackedHooks(new Hook { OperationName = operationName, Process = running });

                // Act
                _service.Stop();

                // Assert
                // The kill-and-wait block ran for this hook: announced with its (fallback) name and real PID...
                scopedLogger.Verify(l => l.Info($"Cleaning up orphaned {expectedName} hook process tree (PID: {pid}).", It.IsAny<Exception>()), Times.Once);

                // ...and the bounded wait saw it exit. This line is written only when HasExited is
                // true after the kill, so it is what pins that the process is gone. The Process
                // object cannot be queried here instead: CleanupTrackedHooks disposes the hook on
                // the way out, and every member on a disposed Process throws.
                scopedLogger.Verify(l => l.Info($"Tracked hook '{expectedName}' cleaned up successfully.", It.IsAny<Exception>()), Times.Once);

                // ...and neither failure arm fired
                scopedLogger.Verify(l => l.Warn(It.Is<string>(m => m.StartsWith("Failed to send Kill signal")), It.IsAny<Exception>()), Times.Never);
                scopedLogger.Verify(l => l.Warn(It.Is<string>(m => m.Contains("did not exit within")), It.IsAny<Exception>()), Times.Never);
                scopedLogger.Verify(l => l.Error("Cleanup of tracked hook failed.", It.IsAny<Exception>()), Times.Never);
                Assert.Empty(GetTrackedHooks());

                // ...and the whole TREE went with it, which is the part every assertion above misses:
                // they all read cmd.exe, and cmd.exe dies just as promptly from a kill that spares its
                // children, leaving ping.exe orphaned for the rest of its 60 seconds.
                Assert.True(ownedChildren[0].WaitForExit(TestTimeouts.CiGenerousMs),
                    "the hook's ping child survived the cleanup");
            }
            finally
            {
                // Never leave the helper or its children behind if an assertion above failed before
                // Cleanup killed them. Go by PID: the handle above may already be disposed by
                // CleanupTrackedHooks.
                foreach (var leftoverPid in new[] { pid, childPid })
                {
                    if (leftoverPid <= 0) continue;

                    try
                    {
                        using var leftover = Process.GetProcessById(leftoverPid);
                        leftover.Kill(entireProcessTree: true);
                    }
                    catch { /* teardown is best-effort: already gone, or the PID is no longer live */ }
                }

                foreach (var child in ownedChildren) child.Dispose();
            }
        }

        /// <summary>
        /// A kill that fails must not abort the hook's cleanup: the warning is logged and the exit
        /// check and outcome evaluation still run. No user-mode test can make a real <c>Process.Kill</c>
        /// throw on a live process it owns, so this arm needs the seam.
        /// </summary>
        [Fact]
        public void Cleanup_TrackedHookKillThrows_WarnsAndStillEvaluatesTheOutcome()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            var exitedReads = 0;
            var service = BuildHookKillingService(
                kill: _ => throw new InvalidOperationException("access denied"),
                hasExited: _ => ++exitedReads > 1,   // alive at the guard, gone afterwards
                waitForExit: (_, _) => true);
            service.StartForTest();
            using var hookProcess = new Process();
            SetTrackedHooks(service, new Hook { OperationName = "Post-Launch", Process = hookProcess });

            // Act
            service.Stop();

            // Assert
            scopedLogger.Verify(l => l.Warn("Failed to send Kill signal to Post-Launch hook: access denied", It.IsAny<Exception>()), Times.Once);

            // The catch must not abort the hook's cleanup: the outcome is still evaluated and logged.
            scopedLogger.Verify(l => l.Info("Tracked hook 'Post-Launch' cleaned up successfully.", It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Error("Cleanup of tracked hook failed.", It.IsAny<Exception>()), Times.Never);
        }

        /// <summary>
        /// A hook that survives the whole budget pulses the SCM, spends exactly the budget and warns
        /// instead of reporting success. With the shipped constants the budget equals one pulse
        /// interval, so this pins a single wait; the pulse chunking is pinned by
        /// <see cref="Cleanup_TrackedHookSurvivesABudgetThatIsNotAMultipleOfThePulse_WaitsInPulsesAndThenTheRemainder"/>.
        /// </summary>
        [Fact]
        public void Cleanup_TrackedHookSurvivesTheBudget_PulsesTheScmAndWarnsWithTheBudget()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            var waits = new List<int>();
            var killed = 0;
            var service = BuildHookKillingService(
                kill: _ => killed++,
                hasExited: _ => false,
                waitForExit: (_, ms) => { waits.Add(ms); return false; });
            service.StartForTest();
            using var hookProcess = new Process();
            SetTrackedHooks(service, new Hook { OperationName = "Post-Launch", Process = hookProcess });

            // Act
            service.Stop();

            // Assert
            Assert.Equal(1, killed);

            // The waits add up to exactly the budget (with the shipped constants, one wait).
            Assert.Equal(AppConfig.HookCleanupTimeoutMs, waits.Sum());

            // One pulse per wait, each carrying the service's SCM wait hint.
            _ctx.Helper.Verify(h => h.RequestAdditionalTime(service, AppConfig.DefaultScmAdditionalTimeMs, null), Times.Exactly(waits.Count));
            _ctx.Helper.Verify(h => h.RequestAdditionalTime(service, It.IsAny<int>(), null), Times.Exactly(waits.Count));
            scopedLogger.Verify(l => l.Warn($"Tracked hook 'Post-Launch' (PID: 0) did not exit within the {AppConfig.HookCleanupTimeoutMs}ms budget. Proceeding with teardown to avoid SCM hang.", It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Info(It.Is<string>(m => m.EndsWith("cleaned up successfully.")), It.IsAny<Exception>()), Times.Never);
        }

        /// <summary>
        /// A budget that is not a multiple of the pulse interval is waited in whole pulses followed by
        /// the remainder, never longer than one pulse at a time. The shipped constants cannot show
        /// this, because the budget equals one pulse, so the budget is supplied through the seam.
        /// </summary>
        [Fact]
        public void Cleanup_TrackedHookSurvivesABudgetThatIsNotAMultipleOfThePulse_WaitsInPulsesAndThenTheRemainder()
        {
            // Arrange
            var options = CreateOptions();
            SetupStart(options);
            var waits = new List<int>();
            var service = BuildHookKillingService(
                kill: _ => { },
                hasExited: _ => false,
                waitForExit: (_, ms) => { waits.Add(ms); return false; },
                hookCleanupTimeoutMs: 12_000);
            service.StartForTest();
            using var hookProcess = new Process();
            SetTrackedHooks(service, new Hook { OperationName = "Post-Launch", Process = hookProcess });

            // Act
            service.Stop();

            // Assert
            // Budget 12_000 with the shipped 5_000 pulse: two whole pulses, then the 2_000 remainder.
            Assert.Equal(5_000, AppConfig.SafeKillProcessPulseIntervalMs);
            Assert.Equal(new[] { 5_000, 5_000, 2_000 }, waits);

            // One pulse per wait: three waits, three pulses, each with the service's wait hint.
            _ctx.Helper.Verify(h => h.RequestAdditionalTime(service, AppConfig.DefaultScmAdditionalTimeMs, null), Times.Exactly(3));
        }

        /// <summary>
        /// A hook that exits during the first wait breaks out of the loop at once, so the budget is
        /// not spent and the success arm is taken. The <c>break</c> itself is pinned by the number of
        /// exit checks: skipping the loop's re-check is its only observable effect, because a wait that
        /// returns <c>true</c> already means the process is gone.
        /// </summary>
        [Fact]
        public void Cleanup_TrackedHookExitsDuringTheFirstWait_BreaksOutAndLogsSuccess()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            var exited = false;
            var waits = 0;
            var exitChecks = 0;
            var service = BuildHookKillingService(
                kill: _ => { },
                hasExited: _ => { exitChecks++; return exited; },
                waitForExit: (_, _) => { waits++; exited = true; return true; });
            service.StartForTest();
            using var hookProcess = new Process();
            SetTrackedHooks(service, new Hook { OperationName = "Post-Launch", Process = hookProcess });

            // Act
            service.Stop();

            // Assert
            Assert.Equal(1, waits);

            // The guard, the loop condition and the outcome check, and nothing more: the break skips
            // the loop's re-check. Without it the loop condition is read a second time, making four.
            Assert.Equal(3, exitChecks);
            scopedLogger.Verify(l => l.Info("Tracked hook 'Post-Launch' cleaned up successfully.", It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Warn(It.Is<string>(m => m.Contains("did not exit within")), It.IsAny<Exception>()), Times.Never);
        }

        /// <summary>
        /// Builds a service wired to this fixture's mocks whose tracked-hook kill, exit check and
        /// bounded wait are served by the supplied delegates instead of a real process.
        /// </summary>
        /// <param name="kill">The stand-in for the kill request, called with the hook's process.</param>
        /// <param name="hasExited">The stand-in for the exit check, called with the hook's process.</param>
        /// <param name="waitForExit">The stand-in for the bounded wait, called with the hook's process and the wait in milliseconds.</param>
        /// <param name="hookCleanupTimeoutMs">The stand-in for the cleanup budget, or <see langword="null"/> to keep the shipped one.</param>
        /// <returns>A service whose four tracked-hook seams are served by the supplied delegates and value.</returns>
        private HookKillingService BuildHookKillingService(
            Action<Process> kill,
            Func<Process, bool> hasExited,
            Func<Process, int, bool> waitForExit,
            int? hookCleanupTimeoutMs = null) =>
            new HookKillingService(
                kill,
                hasExited,
                waitForExit,
                hookCleanupTimeoutMs,
                _ctx.Helper.Object,
                _ctx.Logger.Object,
                _ctx.StreamWriterFactory.Object,
                _ctx.TimerFactory.Object,
                _ctx.ProcessFactory.Object,
                _ctx.PathValidator.Object,
                _ctx.NamedPipesService.Object);

        /// <summary>
        /// Serves the tracked-hook kill, exit check, bounded wait and cleanup budget from supplied delegates and value. With a
        /// real <see cref="Process"/> no user-mode test can make <c>Kill</c> throw on a process it
        /// owns, nor make one survive the kill for the whole cleanup budget, so the kill-failure
        /// warning, the wait loop's pulse and budget arithmetic and the "did not exit within the
        /// budget" arm were never executed under test.
        /// </summary>
        private sealed class HookKillingService : TestableService
        {
            private readonly Action<Process> _kill;
            private readonly Func<Process, bool> _hasExited;
            private readonly Func<Process, int, bool> _waitForExit;
            private readonly int? _hookCleanupTimeoutMs;

            /// <summary>
            /// Initializes a new instance of the <see cref="HookKillingService"/> class.
            /// </summary>
            /// <param name="kill">The stand-in for the kill request.</param>
            /// <param name="hasExited">The stand-in for the exit check.</param>
            /// <param name="waitForExit">The stand-in for the bounded wait.</param>
            /// <param name="hookCleanupTimeoutMs">The stand-in for the cleanup budget, or <see langword="null"/> to keep the shipped one.</param>
            /// <param name="serviceHelper">The SCM helper the base service reports through.</param>
            /// <param name="logger">The logger the base service writes to.</param>
            /// <param name="streamWriterFactory">The factory for the redirected output writers.</param>
            /// <param name="timerFactory">The factory for the health-check and rotation timers.</param>
            /// <param name="processFactory">The factory for the child process wrappers.</param>
            /// <param name="pathValidator">The validator the base service checks configured paths with.</param>
            /// <param name="namedPipesService">The named pipes service the base service interacts with.</param>
            public HookKillingService(
                Action<Process> kill,
                Func<Process, bool> hasExited,
                Func<Process, int, bool> waitForExit,
                int? hookCleanupTimeoutMs,
                Servy.Service.Helpers.IServiceHelper serviceHelper,
                IServyLogger logger,
                IStreamWriterFactory streamWriterFactory,
                ITimerFactory timerFactory,
                IProcessFactory processFactory,
                Servy.Service.Validation.IPathValidator pathValidator,
                INamedPipesService namedPipesService)
                : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, namedPipesService)
            {
                _kill = kill;
                _hasExited = hasExited;
                _waitForExit = waitForExit;
                _hookCleanupTimeoutMs = hookCleanupTimeoutMs;
            }

            /// <summary>
            /// Serves the kill request from the supplied delegate instead of killing a real tree.
            /// </summary>
            /// <param name="process">The tracked hook's process.</param>
            protected override void KillTrackedHook(Process process) => _kill(process);

            /// <summary>
            /// Serves the exit check from the supplied delegate instead of reading the real process.
            /// </summary>
            /// <param name="process">The tracked hook's process.</param>
            /// <returns>Whatever the supplied delegate returns.</returns>
            protected override bool HasTrackedHookExited(Process process) => _hasExited(process);

            /// <summary>
            /// Serves the bounded wait from the supplied delegate instead of really waiting.
            /// </summary>
            /// <param name="process">The tracked hook's process.</param>
            /// <param name="timeoutMs">The maximum time to wait, in milliseconds.</param>
            /// <returns>Whatever the supplied delegate returns.</returns>
            protected override bool WaitForTrackedHookExit(Process process, int timeoutMs) => _waitForExit(process, timeoutMs);

            /// <summary>
            /// Serves the cleanup budget from the supplied value, or the shipped one when none was supplied.
            /// </summary>
            /// <returns>The supplied budget, or <see cref="AppConfig.HookCleanupTimeoutMs"/>.</returns>
            protected override int GetHookCleanupTimeoutMs() => _hookCleanupTimeoutMs ?? base.GetHookCleanupTimeoutMs();
        }

        /// <summary>
        /// A <see cref="SemaphoreSlim"/> whose disposal always fails, standing in for the semaphore
        /// failure that <c>SemaphoreSlim.Dispose</c> itself never produces.
        /// </summary>
        /// <remarks>
        /// <see cref="SemaphoreSlim.Dispose(bool)"/> is the only member overridden, so the semaphore
        /// counts and waits like any other; it just never releases its handle.
        /// </remarks>
        private sealed class DisposeThrowingSemaphore : SemaphoreSlim
        {
            private readonly string _message;

            /// <summary>
            /// Initializes a new instance of the <see cref="DisposeThrowingSemaphore"/> class with one free slot.
            /// </summary>
            /// <param name="message">The message of the exception thrown from every disposal.</param>
            public DisposeThrowingSemaphore(string message)
                : base(1, 1)
            {
                _message = message;
            }

            /// <summary>
            /// Always throws instead of releasing the semaphore.
            /// </summary>
            /// <param name="disposing">Ignored.</param>
            /// <exception cref="InvalidOperationException">Always thrown, carrying the message given to the constructor.</exception>
            protected override void Dispose(bool disposing) => throw new InvalidOperationException(_message);
        }

        /// <summary>
        /// A <see cref="CancellationTokenSource"/> whose disposal always fails, standing in for the failure
        /// that <c>CancellationTokenSource.Dispose</c> itself never produces.
        /// </summary>
        private sealed class DisposeThrowingCancellationSource : CancellationTokenSource
        {
            private readonly string _message;

            /// <summary>
            /// Initializes a new instance of the <see cref="DisposeThrowingCancellationSource"/> class.
            /// </summary>
            /// <param name="message">The message of the exception thrown from every disposal.</param>
            public DisposeThrowingCancellationSource(string message)
            {
                _message = message;
            }

            /// <summary>
            /// Always throws instead of releasing the source.
            /// </summary>
            /// <param name="disposing">Ignored.</param>
            /// <exception cref="InvalidOperationException">Always thrown, carrying the message given to the constructor.</exception>
            protected override void Dispose(bool disposing) => throw new InvalidOperationException(_message);
        }

        /// <summary>
        /// Disposes the service instances the context built for this test.
        /// </summary>
        public void Dispose() => _ctx.Dispose();
    }
}
