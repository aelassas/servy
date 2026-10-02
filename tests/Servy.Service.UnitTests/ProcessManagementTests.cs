using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace Servy.Service.UnitTests
{
    public class ProcessManagementTests : IDisposable
    {
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        [Fact]
        public void StartProcess_StartsProcess()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(123);
            mockProcess.Setup(p => p.Start()).Returns(true);

            ProcessStartInfo? seenPsi = null;
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Callback<ProcessStartInfo, IServyLogger>((psi, _) => seenPsi = psi)
                .Returns(mockProcess.Object);

            // Act
            service.InvokeStartProcess("C:\\myapp.exe", "--arg", "C:\\workdir", new List<EnvironmentVariable>(), TestContext.Current.CancellationToken);

            // Assert
            var childProcess = service.GetChildProcess();
            Assert.Equal(mockProcess.Object, childProcess);
            mockProcess.Verify(p => p.Start(), Times.Once);

            // Verify ProcessStartInfo propagation
            Assert.NotNull(seenPsi);
            Assert.Equal("C:\\myapp.exe", seenPsi.FileName);
            Assert.Equal("--arg", seenPsi.Arguments);
            Assert.Equal("C:\\workdir", seenPsi.WorkingDirectory);

            // Verify event handler wiring
            mockProcess.VerifySet(p => p.EnableRaisingEvents = true, Times.Once);
            mockProcess.VerifyAdd(p => p.OutputDataReceived += It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyAdd(p => p.ErrorDataReceived += It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyAdd(p => p.Exited += It.IsAny<EventHandler>(), Times.Once);

            // Verify asynchronous stream reading calls
            mockProcess.Verify(p => p.BeginOutputReadLine(), Times.Once);
            mockProcess.Verify(p => p.BeginErrorReadLine(), Times.Once);
        }

        [Fact]
        public void StartProcess_StartFails_LogsCleansUpAndRethrows()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Start()).Throws(new Win32Exception(2)); // file not found

            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(mockProcess.Object);

            // Act
            var ex = Assert.Throws<TargetInvocationException>(() =>
                service.InvokeStartProcess("C:\\missing.exe", "", "C:\\", new List<EnvironmentVariable>(), TestContext.Current.CancellationToken));

            // Assert: the failure is rethrown to the caller so OnStart can signal the SCM
            Assert.IsType<Win32Exception>(ex.InnerException);

            // The single source of truth for start-up failure logging
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.StartsWith("Failed to start process")), It.IsAny<Exception>()), Times.Once);

            // CleanupFailedProcess detaches every handler attached before Start()
            mockProcess.VerifyRemove(p => p.OutputDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyRemove(p => p.ErrorDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyRemove(p => p.Exited -= It.IsAny<EventHandler>(), Times.Once);

            // ... disposes the failed wrapper and clears the field
            mockProcess.Verify(p => p.Dispose(), Times.Once);
            Assert.Null(service.GetChildProcess());

            // ... and the stream pumps are never started for a process that did not start
            mockProcess.Verify(p => p.BeginOutputReadLine(), Times.Never);
            mockProcess.Verify(p => p.BeginErrorReadLine(), Times.Never);
        }

        [Fact]
        public void StartProcess_StartFailsAndDisposeThrows_WarnsAndStillClearsChildProcess()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Start()).Throws(new Win32Exception(5)); // access denied
            mockProcess.Setup(p => p.Dispose()).Throws(new InvalidOperationException("dispose failed"));

            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(mockProcess.Object);

            // Act
            var ex = Assert.Throws<TargetInvocationException>(() =>
                service.InvokeStartProcess("C:\\denied.exe", "", "C:\\", new List<EnvironmentVariable>(), TestContext.Current.CancellationToken));

            // Assert: the secondary failure is downgraded to a warning and never masks the original one
            Assert.IsType<Win32Exception>(ex.InnerException);
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.StartsWith("Secondary error during failed process cleanup")), It.IsAny<Exception>()), Times.Once);

            // The finally arm clears the field even when disposal threw
            Assert.Null(service.GetChildProcess());
        }

        [Fact]
        public void SafeKillProcess_KillsProcessGracefully()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert
            mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("stopped gracefully")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SafeKillProcess_LogsErrorOnException()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Throws(new Exception("Boom!"));

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert
            _ctx.Logger.Verify(l => l.Error("SafeKillProcess background task failed: Boom!", It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SafeKillProcess_LineageReadThrows_WarnsAndStillStopsDescendantsWithUnknownLineage()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Format()).Returns("child (0)");
            mockProcess.Setup(p => p.Id).Throws(new InvalidOperationException("gone"));
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert: an unreadable lineage is a warning, not a failure
            _ctx.Logger.Verify(l => l.Warn(
                "SafeKillProcess error while getting process PID and StartTime: gone", It.IsAny<Exception>()), Times.Once);

            // ... and the cleanup still runs, with the unknown lineage passed through as it stands
            mockProcess.Verify(p => p.StopDescendants(
                0, DateTime.MinValue, TestTimeouts.ProcessWrapperProcessTimeoutMs), Times.Once);
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("stopped gracefully")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SafeKillProcess_FormatThrows_LogsTheOuterCatchWarningAndDoesNotPropagate()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Format()).Throws(new InvalidOperationException("no handle"));

            // Act: the opening log line formats the process, so the failure lands in the outer catch
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert
            _ctx.Logger.Verify(l => l.Warn("SafeKillProcess error: no handle", It.IsAny<Exception>()), Times.Once);

            // ... and the stop sequence never started, so nothing is left half-killed
            mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Never);
            mockProcess.Verify(p => p.StopDescendants(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void SafeKillProcess_StopExceedsSafetyBudget_PulsesScmThenAbandonsAndLogsTheLaterFault()
        {
            // Arrange: timeoutMs 0 and a PID that owns no descendants make the safety budget
            // AppConfig.SafeKillProcessSafetyBufferMs (10 s), which two pulse intervals cross.
            var service = _ctx.Build();

            using var release = new ManualResetEventSlim(false);
            using var laterFaultLogged = new ManualResetEventSlim(false);
            _ctx.Logger
                .Setup(l => l.Warn(It.Is<string>(s => s.Contains("later faulted")), It.IsAny<Exception>()))
                .Callback(() => laterFaultLogged.Set());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Format()).Returns("hung (999999)");
            mockProcess.Setup(p => p.Id).Returns(TestProcessIds.NeverValid);
            mockProcess.Setup(p => p.StartTime).Returns(DateTime.Now);
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns<int>(_ =>
            {
                release.Wait(TestTimeouts.CiGenerous);
                throw new InvalidOperationException("kernel refused the kill");
            });

            try
            {
                // Act
                service.InvokeSafeKillProcess(mockProcess.Object, 0);

                // Assert: the wait pulsed the SCM before it gave up
                _ctx.Helper.Verify(h => h.RequestAdditionalTime(
                    service, It.IsAny<int>(), null), Times.AtLeastOnce);
                _ctx.Logger.Verify(l => l.Error(
                    "Stop operation exceeded safety limit. The process tree may be hung at the kernel level.",
                    It.IsAny<Exception>()), Times.Once);

                // ... and the still-running task was abandoned rather than waited out
                _ctx.Logger.Verify(l => l.Warn(
                    It.Is<string>(s => s.Contains("stop sequence did not complete within the safety budget")),
                    It.IsAny<Exception>()), Times.Once);

                // ... and no stop outcome was reported for a task that never produced one
                _ctx.Logger.Verify(l => l.Info(
                    It.Is<string>(s => s.Contains("stopped gracefully")), It.IsAny<Exception>()), Times.Never);
            }
            finally
            {
                release.Set();
            }

            // Assert: the abandoned task is still observed, so its fault reaches the log
            Assert.True(
                laterFaultLogged.Wait(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken),
                "the abandoned stop task's fault was never logged");
            _ctx.Logger.Verify(l => l.Warn(
                "Abandoned stop task for 'hung (999999)' later faulted: kernel refused the kill",
                It.IsAny<Exception>()), Times.Once);
        }


        private const string PreStopExe = @"C:\hooks\prestop.exe";

        /// <summary>
        /// Builds the options a pre-stop test needs, with a configured hook and a distinct service startup directory
        /// so the working-directory fallback is observable.
        /// </summary>
        private static StartOptions CreatePreStopOptions(
            string? preStopStartupDirectory = @"C:\hooks",
            int preStopTimeoutInSeconds = 5,
            bool preStopLogAsError = false) => new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                StartupDirectory = @"C:\svc",
                PreStopExecutablePath = PreStopExe,
                PreStopStartupDirectory = preStopStartupDirectory,
                PreStopTimeoutInSeconds = preStopTimeoutInSeconds,
                PreStopLogAsError = preStopLogAsError,
            };

        [Fact]
        public void StartPreStopProcess_NoExecutableConfigured_SkipsAndReturnsTrue()
        {
            // Arrange
            var service = _ctx.Build();
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };

            // Act
            var result = service.InvokeStartPreStopProcess(options);

            // Assert
            Assert.True(result);
            _ctx.Logger.Verify(l => l.Info("No pre-stop executable configured. Skipping.", It.IsAny<Exception>()), Times.Once);
            _ctx.ProcessFactory.Verify(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()), Times.Never);
        }

        [Fact]
        public void StartPreStopProcess_FireAndForget_ReturnsTrueWithoutWaitingOnTheProcess()
        {
            // Arrange
            var service = _ctx.Build();

            // A zero timeout clamps to 0 ms, which is what selects fire-and-forget
            var options = CreatePreStopOptions(preStopTimeoutInSeconds: 0);

            var preStop = new Mock<IProcessWrapper>();
            preStop.Setup(p => p.Start()).Returns(true);
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(preStop.Object);

            // Act
            var result = service.InvokeStartPreStopProcess(options);

            // Assert
            Assert.True(result);
            _ctx.Logger.Verify(l => l.Info(
                "Pre-stop configured as fire-and-forget. Continuing service stop immediately.",
                It.IsAny<Exception>()), Times.Once);

            // ... and the stop sequence is not held up by the hook
            preStop.Verify(p => p.WaitForExit(It.IsAny<int>()), Times.Never);
            preStop.VerifyGet(p => p.ExitCode, Times.Never);
        }

        [Theory]
        [InlineData(null, @"C:\svc")]
        [InlineData("", @"C:\svc")]
        [InlineData("   ", @"C:\svc")]
        [InlineData(@"C:\hooks", @"C:\hooks")]
        public void StartPreStopProcess_WorkingDirectory_FallsBackToStartupDirectoryWhenPreStopDirectoryIsBlank(
            string? preStopStartupDirectory, string expectedWorkingDirectory)
        {
            // Arrange
            var service = _ctx.Build();

            var options = CreatePreStopOptions(preStopStartupDirectory: preStopStartupDirectory);

            ProcessStartInfo? seenPsi = null;
            var preStop = new Mock<IProcessWrapper>();
            preStop.Setup(p => p.Start()).Returns(true);
            preStop.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            preStop.Setup(p => p.ExitCode).Returns(0);
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Callback<ProcessStartInfo, IServyLogger>((psi, _) => seenPsi = psi)
                .Returns(preStop.Object);

            // Act
            // The return value is the other theories' subject; here the only claim is which directory the
            // launch was configured with, so nothing else is asserted and no other arm can redden this case.
            service.InvokeStartPreStopProcess(options);

            // Assert
            Assert.NotNull(seenPsi);
            Assert.Equal(expectedWorkingDirectory, seenPsi.WorkingDirectory);
            Assert.Equal(PreStopExe, seenPsi.FileName);
        }

        [Theory]
        [InlineData(0, false, true)]
        [InlineData(0, true, true)]
        [InlineData(3, false, true)]
        [InlineData(3, true, false)]
        public void StartPreStopProcess_SynchronousExit_LogsPerPolicyAndReturnsPerExitCode(
            int exitCode, bool logAsError, bool expectedResult)
        {
            // Arrange
            var service = _ctx.Build();
            var options = CreatePreStopOptions(preStopLogAsError: logAsError);

            var preStop = new Mock<IProcessWrapper>();
            preStop.Setup(p => p.Start()).Returns(true);
            preStop.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            preStop.Setup(p => p.ExitCode).Returns(exitCode);
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(preStop.Object);

            // Act
            var result = service.InvokeStartPreStopProcess(options);

            // Assert
            Assert.Equal(expectedResult, result);

            var exitMessage = $"Pre-stop process '{PreStopExe}' exited with code {exitCode}.";
            if (exitCode == 0)
            {
                _ctx.Logger.Verify(l => l.Info("Pre-stop process completed successfully.", It.IsAny<Exception>()), Times.Once);
                _ctx.Logger.Verify(l => l.Warn("Ignoring pre-stop failure and continuing service stop.", It.IsAny<Exception>()), Times.Never);
            }
            else if (logAsError)
            {
                // A failure carrying no exception takes LogIssue's single-argument Error overload
                _ctx.Logger.Verify(l => l.Error(exitMessage, null), Times.Once);
                _ctx.Logger.Verify(l => l.Warn(exitMessage, It.IsAny<Exception>()), Times.Never);
                _ctx.Logger.Verify(l => l.Warn("Ignoring pre-stop failure and continuing service stop.", It.IsAny<Exception>()), Times.Never);
            }
            else
            {
                _ctx.Logger.Verify(l => l.Warn(exitMessage, null), Times.Once);
                _ctx.Logger.Verify(l => l.Error(exitMessage, It.IsAny<Exception>()), Times.Never);
                _ctx.Logger.Verify(l => l.Warn("Ignoring pre-stop failure and continuing service stop.", It.IsAny<Exception>()), Times.Once);
            }
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void StartPreStopProcess_LaunchThrows_LogsAtThePolicyLevelWithTheExceptionAndReturnsPerPolicy(
            bool logAsError, bool expectedResult)
        {
            // Arrange
            var service = _ctx.Build();
            var options = CreatePreStopOptions(preStopLogAsError: logAsError);

            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Throws(new Win32Exception(2, "The system cannot find the file specified."));

            // Act
            var result = service.InvokeStartPreStopProcess(options);

            // Assert
            Assert.Equal(expectedResult, result);
            if (logAsError)
            {
                // The catch arm carries the exception, so LogIssue takes the two-argument Error overload
                _ctx.Logger.Verify(l => l.Error("Pre-stop process failed.", It.IsNotNull<Exception>()), Times.Once);
                _ctx.Logger.Verify(l => l.Warn("Ignoring pre-stop failure and continuing service stop.", It.IsAny<Exception>()), Times.Never);
            }
            else
            {
                _ctx.Logger.Verify(l => l.Warn("Pre-stop process failed.", It.IsNotNull<Exception>()), Times.Once);
                _ctx.Logger.Verify(l => l.Error("Pre-stop process failed.", It.IsAny<Exception>()), Times.Never);
                _ctx.Logger.Verify(l => l.Warn("Ignoring pre-stop failure and continuing service stop.", It.IsAny<Exception>()), Times.Once);
            }
        }

        [Fact]
        public void StartProcess_ConsoleUIEnabled_LogsNoticeAndSkipsRedirection()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", new StartOptions { EnableConsoleUI = true });

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Start()).Returns(true);
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(mockProcess.Object);

            // Act
            service.InvokeStartProcess("C:\\myapp.exe", "--arg", "C:\\workdir", new List<EnvironmentVariable>(), TestContext.Current.CancellationToken);

            // Assert
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(msg => msg.Contains("Console UI support enabled")), It.IsAny<Exception>()), Times.Once);

            // The same flag bypasses stdout/stderr redirection, which is what the notice announces
            mockProcess.VerifyAdd(p => p.OutputDataReceived += It.IsAny<DataReceivedEventHandler>(), Times.Never);
            mockProcess.Verify(p => p.BeginOutputReadLine(), Times.Never);
        }

        /// <summary>
        /// The parent PID the pre-stop scan tests report. It is only ever echoed into the scan's log
        /// lines: the scan itself is served by the overridden seam, so nothing resolves it.
        /// </summary>
        private const int ScannedParentPid = 4321;

        [Fact]
        public void SafeKillProcess_PreStopScanFindsDescendants_LogsEachOneAndDisposesItImmediately()
        {
            // Arrange
            using var child = new DisposeRecordingProcess();
            using var service = BuildScanningService((pid, startTime) => new List<Process> { child });

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Format()).Returns("app.exe (4321)");
            mockProcess.Setup(p => p.Id).Returns(ScannedParentPid);
            mockProcess.Setup(p => p.StartTime).Returns(DateTime.Now);
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert: the found arm reports the count it will charge the stop budget for
            _ctx.Logger.Verify(l => l.Info(
                $"Pre-stop scan found 1 active descendants for PID {ScannedParentPid}:", It.IsAny<Exception>()), Times.Once);

            // ... and each descendant is listed
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.StartsWith("  - ")), It.IsAny<Exception>()), Times.Once);

            // ... and its handle is released right after logging, which is what the loop promises
            Assert.Equal(1, child.DisposeCount);

            // ... and the empty arm did not also run
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("found no active descendants")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void SafeKillProcess_PreStopScanFindsNothing_LogsTheEmptyScanLine()
        {
            // Arrange
            using var service = BuildScanningService((pid, startTime) => new List<Process>());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Format()).Returns("app.exe (4321)");
            mockProcess.Setup(p => p.Id).Returns(ScannedParentPid);
            mockProcess.Setup(p => p.StartTime).Returns(DateTime.Now);
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                $"Pre-stop scan found no active descendants for PID {ScannedParentPid}.", It.IsAny<Exception>()), Times.Once);

            // ... and nothing was listed, so the found arm stayed out of it
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.StartsWith("  - ")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void SafeKillProcess_PreStopScanThrows_WarnsAndStillRunsTheStop()
        {
            // Arrange
            using var service = BuildScanningService(
                (pid, startTime) => throw new Win32Exception(5, "snapshot refused"));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Format()).Returns("app.exe (4321)");
            mockProcess.Setup(p => p.Id).Returns(ScannedParentPid);
            mockProcess.Setup(p => p.StartTime).Returns(DateTime.Now);
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert: a failed scan is a warning, not a failure
            _ctx.Logger.Verify(l => l.Warn(
                "Could not complete pre-stop scan: snapshot refused", It.IsAny<Exception>()), Times.Once);

            // ... and the stop sequence still ran, with the budget charged for zero descendants
            mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("stopped gracefully")), It.IsAny<Exception>()), Times.Once);
        }

        /// <summary>
        /// Builds a service wired to this fixture's mocks whose pre-stop descendant scan is served by
        /// <paramref name="scan"/> instead of walking the real process table.
        /// </summary>
        /// <param name="scan">The stand-in for the descendant enumeration, called with the parent PID and start time.</param>
        /// <returns>A service whose <c>GetProcessDescendants</c> seam is served by <paramref name="scan"/>.</returns>
        private ScanningService BuildScanningService(Func<int, DateTime, List<Process>> scan) =>
            new ScanningService(
                scan,
                _ctx.Helper.Object,
                _ctx.Logger.Object,
                _ctx.StreamWriterFactory.Object,
                _ctx.TimerFactory.Object,
                _ctx.ProcessFactory.Object,
                _ctx.PathValidator.Object,
                _ctx.NamedPipesService.Object);

        /// <summary>
        /// Serves the pre-stop descendant scan from a supplied delegate. The real scan walks the live
        /// process table, so with the mocked process wrappers these tests use it matches nothing and
        /// always ends in its own catch, leaving the found arm, the empty arm and the per-child
        /// disposal unexecuted.
        /// </summary>
        private sealed class ScanningService : TestableService
        {
            private readonly Func<int, DateTime, List<Process>> _scan;

            /// <summary>
            /// Initializes a new instance of the <see cref="ScanningService"/> class.
            /// </summary>
            /// <param name="scan">The stand-in for the descendant enumeration.</param>
            /// <param name="serviceHelper">The SCM helper the base service reports through.</param>
            /// <param name="logger">The logger the base service writes to.</param>
            /// <param name="streamWriterFactory">The factory for the redirected output writers.</param>
            /// <param name="timerFactory">The factory for the health-check and rotation timers.</param>
            /// <param name="processFactory">The factory for the child process wrappers.</param>
            /// <param name="pathValidator">The validator the base service checks configured paths with.</param>
            /// <param name="namedPipesService">The named pipes service the base service uses.</param>
            public ScanningService(
                Func<int, DateTime, List<Process>> scan,
                Servy.Service.Helpers.IServiceHelper serviceHelper,
                IServyLogger logger,
                Servy.Service.StreamWriters.IStreamWriterFactory streamWriterFactory,
                Servy.Service.Timers.ITimerFactory timerFactory,
                IProcessFactory processFactory,
                Servy.Service.Validation.IPathValidator pathValidator,
                Servy.Core.NamedPipes.INamedPipesService namedPipesService)
                : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, namedPipesService)
            {
                _scan = scan;
            }

            /// <summary>
            /// Serves the scan from the supplied delegate instead of the real process table.
            /// </summary>
            /// <param name="parentPid">The process ID of the parent whose descendants are enumerated.</param>
            /// <param name="parentStartTime">The start time of the parent process.</param>
            /// <returns>Whatever the supplied delegate returns.</returns>
            protected override List<Process> GetProcessDescendants(int parentPid, DateTime parentStartTime) =>
                _scan(parentPid, parentStartTime);
        }

        /// <summary>
        /// A process object that records how often it was disposed. The pre-stop scan's contract is that
        /// the caller owns every returned process and disposes it, and a real <see cref="Process"/> gives
        /// a test no way to observe that.
        /// </summary>
        private sealed class DisposeRecordingProcess : Process
        {
            /// <summary>Gets the number of times this process was disposed.</summary>
            public int DisposeCount { get; private set; }

            /// <summary>
            /// Records the disposal and then disposes as the base class does.
            /// </summary>
            /// <param name="disposing">
            /// <see langword="true"/> when called from <see cref="Component.Dispose()"/>;
            /// <see langword="false"/> when called from the finalizer.
            /// </param>
            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
                base.Dispose(disposing);
            }
        }

        public void Dispose() => _ctx.Dispose();
    }
}
