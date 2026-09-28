using Moq;
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
            var tracked = TestReflection.GetField<List<Hook>>(_service, "_trackedHooks");
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

            // Each dispose failure is caught individually, so the finally still reaches the child.
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

        /// <summary>
        /// Disposes the service instances the context built for this test.
        /// </summary>
        public void Dispose() => _ctx.Dispose();
    }
}
