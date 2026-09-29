using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Covers the teardown arms of <c>Service.Cleanup()</c> that the lifecycle tests never reach:
    /// the redirected-stream unhook, the pre-stop failure policy log, the tracked-hook cleanup loop
    /// and the output-writer dispose failures.
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
            string preStopExecutablePath = null,
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
        {
            var scopedLogger = new Mock<IServyLogger>();

            _ctx.Helper.Setup(h => h.GetArgs()).Returns(new[] { "servy.exe" });
            _ctx.Helper.Setup(h => h.ParseOptions(It.IsAny<IServiceRepository>(), It.IsAny<string[]>())).Returns(options);
            _ctx.Logger.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(scopedLogger.Object);
            _ctx.Helper.Setup(h => h.ValidateAndLog(options, scopedLogger.Object)).Returns(true);

            return scopedLogger;
        }

        /// <summary>
        /// Replaces the service's tracked-hook list content, which teardown's cleanup loop walks.
        /// </summary>
        private void SetTrackedHooks(params Hook[] hooks)
        {
            SetTrackedHooks(_service, hooks);
        }

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
        public void Cleanup_PreStopFailsWithLogAsError_LogsManualInterventionAndStillStopsTheChild()
        {
            // Arrange
            var options = CreateOptions(preStopExecutablePath: PreStopExe, preStopLogAsError: true);
            var scopedLogger = SetupStart(options);

            var preStop = new Mock<IProcessWrapper>();
            preStop.Setup(p => p.StartInfo).Returns(new ProcessStartInfo());
            preStop.Setup(p => p.Start()).Returns(true);
            preStop.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            preStop.Setup(p => p.ExitCode).Returns(3);
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
                It.IsAny<Exception>()), Times.Once);

            // The contract the message states: the main child is still killed after a failed pre-stop.
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
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
            using (var neverStarted = new Process())
            {
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
        }

        [Fact]
        public void Cleanup_TrackedHookAlreadyExited_SkipsTheKillPathEntirely()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            _service.StartForTest();

            // A real process that has already exited. Only this test's own PID is touched.
            using (var exited = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false, CreateNoWindow = true }))
            {
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
            using (var running = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 60 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true }))
            {
                var pid = running.Id;

                try
                {
                    Assert.False(running.HasExited, "the helper process exited before the test could track it");
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
                }
                finally
                {
                    // Never leave the helper behind if an assertion above failed before Cleanup killed
                    // it. Go by PID: the handle above may already be disposed by CleanupTrackedHooks.
                    try
                    {
                        using (var leftover = Process.GetProcessById(pid))
                        {
                            Servy.Service.Helpers.ProcessHelper.KillProcessTree(leftover);
                        }
                    }
                    catch { /* teardown is best-effort: already gone, or the PID is no longer live */ }
                }
            }
        }

        /// <summary>
        /// A kill that fails must not abort the hook's cleanup: the warning is logged and the wait
        /// and outcome evaluation still run. No user-mode test can make a real kill throw on a live
        /// process it owns, so this arm needs the seam.
        /// </summary>
        [Fact]
        public void Cleanup_TrackedHookKillThrows_WarnsAndStillEvaluatesTheOutcome()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            var exitedReads = 0;
            var service = BuildHookKillingService(
                kill: process => throw new InvalidOperationException("access denied"),
                hasExited: process => ++exitedReads > 1,   // alive at the guard, gone afterwards
                waitForExit: (process, ms) => true);
            service.StartForTest();
            using (var hookProcess = new Process())
            {
                SetTrackedHooks(service, new Hook { OperationName = "Post-Launch", Process = hookProcess });

                // Act
                service.Stop();

                // Assert
                scopedLogger.Verify(l => l.Warn("Failed to send Kill signal to Post-Launch hook: access denied", It.IsAny<Exception>()), Times.Once);

                // The catch must not abort the hook's cleanup: the outcome is still evaluated and logged.
                scopedLogger.Verify(l => l.Info("Tracked hook 'Post-Launch' cleaned up successfully.", It.IsAny<Exception>()), Times.Once);
                scopedLogger.Verify(l => l.Error("Cleanup of tracked hook failed.", It.IsAny<Exception>()), Times.Never);
            }
        }

        /// <summary>
        /// A hook that survives the whole budget pulses the SCM, never waits longer than one pulse
        /// interval at a time, spends exactly the budget and warns instead of reporting success.
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
                kill: process => killed++,
                hasExited: process => false,
                waitForExit: (process, ms) => { waits.Add(ms); return false; });
            service.StartForTest();
            using (var hookProcess = new Process())
            {
                SetTrackedHooks(service, new Hook { OperationName = "Post-Launch", Process = hookProcess });

                // Act
                service.Stop();

                // Assert
                Assert.Equal(1, killed);

                // The waits never exceed one pulse and add up to exactly the budget.
                Assert.All(waits, ms => Assert.True(ms <= AppConfig.SafeKillProcessPulseIntervalMs));
                Assert.Equal(AppConfig.HookCleanupTimeoutMs, waits.Sum());
                _ctx.Helper.Verify(h => h.RequestAdditionalTime(service, It.IsAny<int>(), null), Times.AtLeast(waits.Count));
                scopedLogger.Verify(l => l.Warn($"Tracked hook 'Post-Launch' (PID: 0) did not exit within the {AppConfig.HookCleanupTimeoutMs}ms budget. Proceeding with teardown to avoid SCM hang.", It.IsAny<Exception>()), Times.Once);
                scopedLogger.Verify(l => l.Info(It.Is<string>(m => m.EndsWith("cleaned up successfully.")), It.IsAny<Exception>()), Times.Never);
            }
        }

        /// <summary>
        /// A hook that exits during the first wait breaks out of the loop at once, so the budget is
        /// not spent and the success arm is taken.
        /// </summary>
        [Fact]
        public void Cleanup_TrackedHookExitsDuringTheFirstWait_BreaksOutAndLogsSuccess()
        {
            // Arrange
            var options = CreateOptions();
            var scopedLogger = SetupStart(options);
            var exited = false;
            var waits = 0;
            var service = BuildHookKillingService(
                kill: process => { },
                hasExited: process => exited,
                waitForExit: (process, ms) => { waits++; exited = true; return true; });
            service.StartForTest();
            using (var hookProcess = new Process())
            {
                SetTrackedHooks(service, new Hook { OperationName = "Post-Launch", Process = hookProcess });

                // Act
                service.Stop();

                // Assert
                Assert.Equal(1, waits);
                scopedLogger.Verify(l => l.Info("Tracked hook 'Post-Launch' cleaned up successfully.", It.IsAny<Exception>()), Times.Once);
                scopedLogger.Verify(l => l.Warn(It.Is<string>(m => m.Contains("did not exit within")), It.IsAny<Exception>()), Times.Never);
            }
        }

        /// <summary>
        /// Builds a service wired to this fixture's mocks whose tracked-hook kill, exit check and
        /// bounded wait are served by the supplied delegates instead of a real process.
        /// </summary>
        /// <param name="kill">The stand-in for the kill request, called with the hook's process.</param>
        /// <param name="hasExited">The stand-in for the exit check, called with the hook's process.</param>
        /// <param name="waitForExit">The stand-in for the bounded wait, called with the hook's process and the wait in milliseconds.</param>
        /// <returns>A service whose three tracked-hook seams are served by the supplied delegates.</returns>
        private HookKillingService BuildHookKillingService(
            Action<Process> kill,
            Func<Process, bool> hasExited,
            Func<Process, int, bool> waitForExit)
        {
            return new HookKillingService(
                kill,
                hasExited,
                waitForExit,
                _ctx.Helper.Object,
                _ctx.Logger.Object,
                _ctx.StreamWriterFactory.Object,
                _ctx.TimerFactory.Object,
                _ctx.ProcessFactory.Object,
                _ctx.PathValidator.Object,
                _ctx.ServiceRepository.Object);
        }

        /// <summary>
        /// Serves the tracked-hook kill, exit check and bounded wait from supplied delegates. With a
        /// real <see cref="Process"/> no user-mode test can make the kill throw on a process it owns,
        /// nor make one survive the kill for the whole cleanup budget, so the kill-failure warning,
        /// the wait loop's pulse and budget arithmetic and the "did not exit within the budget" arm
        /// were never executed under test.
        /// </summary>
        private sealed class HookKillingService : TestableService
        {
            private readonly Action<Process> _kill;
            private readonly Func<Process, bool> _hasExited;
            private readonly Func<Process, int, bool> _waitForExit;

            /// <summary>
            /// Initializes a new instance of the <see cref="HookKillingService"/> class.
            /// </summary>
            /// <param name="kill">The stand-in for the kill request.</param>
            /// <param name="hasExited">The stand-in for the exit check.</param>
            /// <param name="waitForExit">The stand-in for the bounded wait.</param>
            /// <param name="serviceHelper">The SCM helper the base service reports through.</param>
            /// <param name="logger">The logger the base service writes to.</param>
            /// <param name="streamWriterFactory">The factory for the redirected output writers.</param>
            /// <param name="timerFactory">The factory for the health-check and rotation timers.</param>
            /// <param name="processFactory">The factory for the child process wrappers.</param>
            /// <param name="pathValidator">The validator the base service checks configured paths with.</param>
            /// <param name="serviceRepository">The repository the base service reads its configuration from.</param>
            public HookKillingService(
                Action<Process> kill,
                Func<Process, bool> hasExited,
                Func<Process, int, bool> waitForExit,
                Servy.Service.Helpers.IServiceHelper serviceHelper,
                IServyLogger logger,
                Servy.Service.StreamWriters.IStreamWriterFactory streamWriterFactory,
                Servy.Service.Timers.ITimerFactory timerFactory,
                IProcessFactory processFactory,
                Servy.Service.Validation.IPathValidator pathValidator,
                Servy.Core.Data.IServiceRepository serviceRepository)
                : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, serviceRepository)
            {
                _kill = kill;
                _hasExited = hasExited;
                _waitForExit = waitForExit;
            }

            /// <summary>
            /// Serves the kill request from the supplied delegate instead of killing a real tree.
            /// </summary>
            /// <param name="process">The tracked hook's process.</param>
            protected override void KillTrackedHook(Process process)
            {
                _kill(process);
            }

            /// <summary>
            /// Serves the exit check from the supplied delegate instead of reading the real process.
            /// </summary>
            /// <param name="process">The tracked hook's process.</param>
            /// <returns>Whatever the supplied delegate returns.</returns>
            protected override bool HasTrackedHookExited(Process process)
            {
                return _hasExited(process);
            }

            /// <summary>
            /// Serves the bounded wait from the supplied delegate instead of really waiting.
            /// </summary>
            /// <param name="process">The tracked hook's process.</param>
            /// <param name="timeoutMs">The maximum time to wait, in milliseconds.</param>
            /// <returns>Whatever the supplied delegate returns.</returns>
            protected override bool WaitForTrackedHookExit(Process process, int timeoutMs)
            {
                return _waitForExit(process, timeoutMs);
            }
        }

        /// <summary>
        /// Disposes the service instances the context built for this test.
        /// </summary>
        public void Dispose() => _ctx.Dispose();
    }
}
