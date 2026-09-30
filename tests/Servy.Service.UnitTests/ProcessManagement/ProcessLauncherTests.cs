using Microsoft.Win32.SafeHandles;
using Moq;
using Servy.Core.Logging;
using Servy.Core.Validation;
using Servy.Service.ProcessManagement;
using Servy.Service.UnitTests.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;

namespace Servy.Service.UnitTests.ProcessManagement
{
    /// <summary>
    /// Unit tests for <see cref="ProcessLauncher"/>'s language-fix null guard and detection fallbacks, for the
    /// security refusals and the post-open failure dispose of
    /// <see cref="ProcessLauncher.TryOpenAppendWriter(string, Encoding, string, string, IServyLogger)"/>, and for
    /// <see cref="ProcessLauncher.Start(ProcessLaunchOptions, IProcessFactory, IServyLogger)"/>'s final exit poll once its
    /// synchronous wait budget has run out, its redirect-handler open and write failures, its best-effort output
    /// drain and its orphaned-child kill.
    /// </summary>
    /// <remarks>
    /// The two detection patterns are anchored, compiled and private, so no input can make them time out.
    /// These tests drive the internal seam overload with matchers that throw
    /// <see cref="RegexMatchTimeoutException"/> directly, which is the only way to reach either catch arm.
    /// The writer refusals are driven the same way: the ancestor reparse-point check and the handle-path
    /// resolver are supplied by the test, because a real junction swap between two lines and a mismatching
    /// final path cannot be arranged from a test.
    /// </remarks>
    public class ProcessLauncherTests : IDisposable
    {
        private readonly string _tempDir =
            Path.Combine(Path.GetTempPath(), "servy-pl-" + Guid.NewGuid().ToString("N"));

        private readonly Mock<IServyLogger> _logger = new Mock<IServyLogger>();

        /// <summary>
        /// Removes the per-test temporary directory.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test.
            }
            catch (UnauthorizedAccessException)
            {
                // Same: teardown is best effort.
            }
        }

        /// <summary>
        /// A handle resolver that always fails to resolve, which is the fail-closed branch.
        /// </summary>
        /// <param name="handle">The open file handle, ignored.</param>
        /// <param name="finalPath">Always set to an empty string.</param>
        /// <returns>Always <c>false</c>.</returns>
        private static bool Unresolvable(SafeFileHandle handle, out string finalPath)
        {
            finalPath = string.Empty;
            return false;
        }

        /// <summary>
        /// A handle resolver that throws, standing in for a native failure after the stream is already open.
        /// </summary>
        /// <param name="handle">The open file handle, ignored.</param>
        /// <param name="finalPath">Never assigned; the method always throws.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="IOException">Always thrown.</exception>
        private static bool Throws(SafeFileHandle handle, out string finalPath)
        {
            throw new IOException("resolver failed");
        }

        /// <summary>
        /// The path <see cref="ResolvesElsewhere"/> reports, standing in for a symlink swap won by an attacker.
        /// </summary>
        private string _swapTarget = string.Empty;

        /// <summary>
        /// A handle resolver that reports <see cref="_swapTarget"/> rather than the requested path.
        /// </summary>
        /// <param name="handle">The open file handle, ignored.</param>
        /// <param name="finalPath">Always set to <see cref="_swapTarget"/>.</param>
        /// <returns>Always <c>true</c>.</returns>
        private bool ResolvesElsewhere(SafeFileHandle handle, out string finalPath)
        {
            finalPath = _swapTarget;
            return true;
        }

        private static RegexMatchTimeoutException Timeout(string input) =>
            new RegexMatchTimeoutException(input, "(pattern)", System.TimeSpan.FromMilliseconds(1));

        [Fact]
        public void ApplyLanguageFixes_PythonRegexTimesOut_WarnsAndSetsNoPythonVariables()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var psi = new ProcessStartInfo { FileName = @"C:\py\python.exe", UseShellExecute = false };
            foreach (var key in new[] { "PYTHONUTF8", "PYTHONIOENCODING", "PYTHONLEGACYWINDOWSSTDIO", "PYTHONUNBUFFERED" })
            {
                psi.Environment.Remove(key);
            }

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger.Object, _ => throw Timeout("python"), _ => false);

            // Assert
            Assert.False(psi.Environment.ContainsKey("PYTHONUTF8"));
            Assert.False(psi.Environment.ContainsKey("PYTHONIOENCODING"));
            Assert.False(psi.Environment.ContainsKey("PYTHONLEGACYWINDOWSSTDIO"));
            Assert.False(psi.Environment.ContainsKey("PYTHONUNBUFFERED"));
            logger.Verify(
                l => l.Warn(It.Is<string>(m => m.Contains("Python detection regex timed out")), It.IsAny<System.Exception?>()),
                Times.Once);
        }

        [Theory]
        [InlineData("java.exe", "-Dfile.encoding=UTF-8 -jar app.jar")]
        [InlineData("javaw.exe", "-Dfile.encoding=UTF-8 -jar app.jar")]
        [InlineData("javac.exe", "-J-Dfile.encoding=UTF-8 -jar app.jar")]
        public void ApplyLanguageFixes_JavaEncodingRegexTimesOut_WarnsAndPrependsTheFlag(string exe, string expectedArgs)
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var psi = new ProcessStartInfo { FileName = exe, Arguments = "-jar app.jar", UseShellExecute = false };

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger.Object, _ => false, _ => throw Timeout("-jar app.jar"));

            // Assert
            Assert.Equal(expectedArgs, psi.Arguments);
            logger.Verify(
                l => l.Warn(It.Is<string>(m => m.Contains("-Dfile.encoding detection regex timed out")), It.IsAny<System.Exception?>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_AncestorIsReparsePoint_RefusesBeforeCreatingTheDirectory()
        {
            // Arrange
            var subDir = Path.Combine(_tempDir, "sub");
            var path = Path.Combine(subDir, "out.log");
            var calls = 0;

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stdout", _logger.Object,
                _ => { calls++; return true; }, PathSecurityGuard.TryGetFinalPathByHandle);

            // Assert
            Assert.Null(writer);
            Assert.Equal(1, calls);
            Assert.False(Directory.Exists(subDir));
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.StartsWith("Refusing to write stdout") && m.Contains("junction")),
                    It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_AncestorBecomesReparsePointAfterCreate_RefusesAndLogs()
        {
            // Arrange
            var path = Path.Combine(_tempDir, "sub", "out.log");
            var calls = 0;

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stdout", _logger.Object,
                _ => ++calls == 2, PathSecurityGuard.TryGetFinalPathByHandle);

            // Assert
            Assert.Null(writer);
            Assert.Equal(2, calls);
            Assert.False(File.Exists(path));
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.StartsWith("Refusing to write stdout") && m.Contains("junction")),
                    It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_HandleCannotBeResolved_FailsClosedAndReleasesTheFile()
        {
            // Arrange
            var path = Path.Combine(_tempDir, "out.log");

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stderr", _logger.Object, _ => false, Unresolvable);

            // Assert
            Assert.Null(writer);
            Assert.True(File.Exists(path));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // An exclusive open throws IOException while the refused writer's handle is still open,
                // so it witnesses the fs.Dispose() in this arm. File.Delete does not: the writer is
                // opened with FileShare.Delete, so a delete succeeds with the handle still open.
            }
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.Contains("could not resolve the opened handle")),
                    It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_HandleResolvesElsewhere_RefusesAndNamesBothPaths()
        {
            // Arrange
            var path = Path.Combine(_tempDir, "out.log");
            _swapTarget = Path.Combine(_tempDir, "elsewhere.log");

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stdout", _logger.Object, _ => false, ResolvesElsewhere);

            // Assert
            Assert.Null(writer);
            Assert.True(File.Exists(path));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // An exclusive open throws IOException while the refused writer's handle is still open,
                // so it witnesses the fs.Dispose() in this arm. File.Delete does not: the writer is
                // opened with FileShare.Delete, so a delete succeeds with the handle still open.
            }
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.Contains(_swapTarget) && m.Contains(Path.GetFullPath(path))),
                    It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_FailureAfterOpen_DisposesTheStreamAndLogsTheException()
        {
            // Arrange
            var path = Path.Combine(_tempDir, "out.log");

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stdout", _logger.Object, _ => false, Throws);

            // Assert
            Assert.Null(writer);
            Assert.True(File.Exists(path));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // An exclusive open throws IOException while the failed writer's handle is still open, so it
                // witnesses the fs?.Dispose() in the catch-all. This is the only arm that reaches that call
                // with fs non-null: every earlier refusal returns before the FileStream is opened.
            }
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.StartsWith("Disabling stdout capture") && m.Contains("after open failure")),
                    It.Is<Exception?>(e => e is IOException && e.Message == "resolver failed")),
                Times.Once);
        }

        [Fact]
        public void ApplyLanguageFixes_NullStartInfo_ReturnsWithoutThrowing()
        {
            // Arrange, Act & Assert
            Assert.Null(Record.Exception(() => ProcessLauncher.ApplyLanguageFixes(null!, logger: null)));
        }

        [Fact]
        public void ApplyLanguageFixes_DetectionTimesOutWithNullLogger_DoesNotThrow()
        {
            // Arrange
            var python = new ProcessStartInfo { FileName = @"C:\py\python.exe", UseShellExecute = false };
            var java = new ProcessStartInfo { FileName = "java.exe", Arguments = "-jar app.jar", UseShellExecute = false };

            // Act
            var pythonEx = Record.Exception(
                () => ProcessLauncher.ApplyLanguageFixes(python, null, _ => throw Timeout("python"), _ => false));
            var javaEx = Record.Exception(
                () => ProcessLauncher.ApplyLanguageFixes(java, null, _ => false, _ => throw Timeout("-jar app.jar")));

            // Assert
            Assert.Null(pythonEx);
            Assert.Null(javaEx);
            Assert.Equal("-Dfile.encoding=UTF-8 -jar app.jar", java.Arguments);
        }

        [Fact]
        public void Start_ChildExitsOnTheFinalPoll_ReturnsWithoutTimeoutOrKill()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var process = new Mock<IProcessWrapper>();
            process.Setup(p => p.Start()).Returns(true);
            process.Setup(p => p.WaitForExit(It.Is<int>(ms => ms > 0)))
                   .Callback<int>(ms => Thread.Sleep(ms))
                   .Returns(false);                                 // never exits inside a slice
            process.Setup(p => p.WaitForExit(0)).Returns(true);     // ...but has exited by the final poll
            var factory = new Mock<IProcessFactory>();
            factory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                   .Returns(process.Object);
            int heartbeats = 0;
            var options = new ProcessLaunchOptions
            {
                ExecutablePath = @"C:\tools\hook.exe",
                TimeoutMs = 50,
                WaitChunkMs = 10,
                OnScmHeartbeat = _ => heartbeats++,
            };

            // Act
            var result = ProcessLauncher.Start(options, factory.Object, logger.Object);

            // Assert
            Assert.Same(process.Object, result);
            Assert.True(heartbeats > 0);
            process.Verify(p => p.WaitForExit(0), Times.Once);
            process.Verify(p => p.Kill(It.IsAny<bool>()), Times.Never);
            logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception?>()), Times.Never);
        }

        #region Start - redirect handler failure paths, drain and orphan cleanup (#7187)

        /// <summary>
        /// The unopenable path the integration suite already uses to force
        /// <see cref="ProcessLauncher.TryOpenAppendWriter(string, Encoding, string, string, IServyLogger)"/> into its
        /// catch arm. The extended-length prefix bypasses managed path validation and guarantees the open fails.
        /// </summary>
        private const string UnopenablePath = @"\\?\C:\illegal|char.log";

        /// <summary>
        /// Builds a <see cref="Mock{T}"/> of <see cref="IProcessWrapper"/> that starts successfully, captures the two
        /// redirect handlers <see cref="ProcessLauncher.Start(ProcessLaunchOptions, IProcessFactory, IServyLogger)"/>
        /// attaches, and runs <paramref name="duringWait"/> inside the synchronous wait - which is where the real
        /// output pump raises them, before the finally block disposes the writers.
        /// </summary>
        /// <param name="duringWait">
        /// Receives the captured stdout and stderr handlers (either may be <see langword="null"/> when that stream is
        /// not redirected) and is invoked once, from inside <see cref="IProcessWrapper.WaitForExit(int)"/>.
        /// </param>
        /// <returns>The configured mock, whose <c>Object</c> the test hands to a fake <see cref="IProcessFactory"/>.</returns>
        private static Mock<IProcessWrapper> ScriptedWrapper(
            Action<DataReceivedEventHandler?, DataReceivedEventHandler?> duringWait)
        {
            DataReceivedEventHandler? outHandler = null;
            DataReceivedEventHandler? errHandler = null;

            var wrapper = new Mock<IProcessWrapper>();
            wrapper.Setup(p => p.Start()).Returns(true);
            wrapper.SetupAdd(p => p.OutputDataReceived += It.IsAny<DataReceivedEventHandler>())
                   .Callback<DataReceivedEventHandler>(h => outHandler = h);
            wrapper.SetupAdd(p => p.ErrorDataReceived += It.IsAny<DataReceivedEventHandler>())
                   .Callback<DataReceivedEventHandler>(h => errHandler = h);
            wrapper.Setup(p => p.WaitForExit(It.IsAny<int>()))
                   .Returns(() => { duringWait(outHandler, errHandler); return true; });
            wrapper.Setup(p => p.Kill(It.IsAny<bool>())).Returns(true);
            return wrapper;
        }

        /// <summary>
        /// Builds a fake <see cref="IProcessFactory"/> that always hands back <paramref name="wrapper"/>.
        /// </summary>
        /// <param name="wrapper">The scripted wrapper the launcher should receive.</param>
        /// <returns>The factory instance to pass to <see cref="ProcessLauncher.Start(ProcessLaunchOptions, IProcessFactory, IServyLogger)"/>.</returns>
        private static IProcessFactory FactoryFor(Mock<IProcessWrapper> wrapper)
        {
            var factory = new Mock<IProcessFactory>();
            factory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                   .Returns(wrapper.Object);
            return factory.Object;
        }

        /// <summary>
        /// Builds the synchronous, redirecting launch options the handler tests need.
        /// </summary>
        /// <param name="stdout">The stdout log path, or <see langword="null"/> to leave stdout unredirected.</param>
        /// <param name="stderr">The stderr log path, or <see langword="null"/> to leave stderr unredirected.</param>
        /// <returns>Options with <c>FireAndForget = false</c>, <c>RedirectToWriters = true</c> and a bounded wait budget.</returns>
        private static ProcessLaunchOptions SyncOptions(string? stdout, string? stderr) =>
            new ProcessLaunchOptions
            {
                ExecutablePath = @"C:\tools\hook.exe",
                FireAndForget = false,
                EnableConsoleUI = false,
                RedirectToWriters = true,
                TimeoutMs = 5000,
                WaitChunkMs = 50,
                StdoutPath = stdout,
                StderrPath = stderr,
            };

        [Fact]
        public void Start_SamePath_StderrOpensSharedWriterFirst_WritesBothLinesToOneFile()
        {
            // Arrange
            var log = Path.Combine(_tempDir, "both.log");
            var wrapper = ScriptedWrapper((o, e) =>
            {
                e!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("err-first"));
                o!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("out-second"));
            });

            // Act
            ProcessLauncher.Start(SyncOptions(log, log), FactoryFor(wrapper), _logger.Object);

            // Assert
            Assert.Equal(new[] { "err-first", "out-second" }, File.ReadAllLines(log));
            _logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception?>()), Times.Never);
        }

        [Fact]
        public void Start_SamePath_SharedWriterOpenFails_LatchesAfterOneLogLine()
        {
            // Arrange
            var wrapper = ScriptedWrapper((o, e) =>
            {
                e!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("one"));
                e!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("two"));
            });

            // Act
            ProcessLauncher.Start(SyncOptions(UnopenablePath, UnopenablePath), FactoryFor(wrapper), _logger.Object);

            // Assert
            _logger.Verify(
                l => l.Error(It.Is<string>(m => m.StartsWith("Disabling multiplexed stdout/stderr capture")), It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void Start_IndependentStderrOpenFails_LatchesAfterOneLogLine()
        {
            // Arrange
            var wrapper = ScriptedWrapper((o, e) =>
            {
                e!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("one"));
                e!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("two"));
            });

            // Act
            ProcessLauncher.Start(
                SyncOptions(Path.Combine(_tempDir, "out.log"), UnopenablePath),
                FactoryFor(wrapper),
                _logger.Object);

            // Assert
            _logger.Verify(
                l => l.Error(It.Is<string>(m => m.StartsWith("Disabling stderr capture")), It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void Start_StdoutOpenFails_LatchesAfterOneLogLine()
        {
            // Arrange
            var wrapper = ScriptedWrapper((o, e) =>
            {
                o!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("one"));
                o!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("two"));
            });

            // Act
            ProcessLauncher.Start(SyncOptions(UnopenablePath, null), FactoryFor(wrapper), _logger.Object);

            // Assert
            _logger.Verify(
                l => l.Error(It.Is<string>(m => m.StartsWith("Disabling stdout capture")), It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void Start_StdoutLineArrivesAfterTheWriterIsDisposed_WarnsAndDoesNotThrow()
        {
            // Arrange
            DataReceivedEventHandler? captured = null;
            var wrapper = ScriptedWrapper((o, e) =>
            {
                captured = o;
                o!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("first"));
            });
            ProcessLauncher.Start(
                SyncOptions(Path.Combine(_tempDir, "out.log"), null),
                FactoryFor(wrapper),
                _logger.Object);   // the finally block has now disposed the writer

            // Act
            var ex = Record.Exception(() =>
                captured!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("late")));

            // Assert
            Assert.Null(ex);
            _logger.Verify(
                l => l.Warn(It.Is<string>(m => m.StartsWith("Failed to write stdout line")), It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void Start_StderrLineArrivesAfterTheWriterIsDisposed_WarnsAndDoesNotThrow()
        {
            // Arrange
            DataReceivedEventHandler? captured = null;
            var wrapper = ScriptedWrapper((o, e) =>
            {
                captured = e;
                e!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("first"));
            });
            ProcessLauncher.Start(
                SyncOptions(Path.Combine(_tempDir, "out.log"), Path.Combine(_tempDir, "err.log")),
                FactoryFor(wrapper),
                _logger.Object);   // the finally block has now disposed both writers

            // Act
            var ex = Record.Exception(() =>
                captured!(null, DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("late")));

            // Assert
            Assert.Null(ex);
            _logger.Verify(
                l => l.Warn(It.Is<string>(m => m.StartsWith("Failed to write stderr line")), It.IsAny<Exception?>()),
                Times.Once);
        }

        [Fact]
        public void Start_DrainWaitThrows_IsSwallowedAndTheWrapperIsStillReturned()
        {
            // Arrange
            var wrapper = ScriptedWrapper((o, e) => { });
            wrapper.Setup(p => p.WaitForExit()).Throws(new InvalidOperationException("drain failed"));

            // Act
            var result = ProcessLauncher.Start(SyncOptions(null, null), FactoryFor(wrapper), _logger.Object);

            // Assert
            Assert.Same(wrapper.Object, result);
            _logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception?>()), Times.Never);
            wrapper.Verify(p => p.Dispose(), Times.Never);
        }

        [Fact]
        public void Start_WaitThrowsAndOrphanKillThrows_WarnsDisposesAndRethrowsTheOriginal()
        {
            // Arrange
            var wrapper = ScriptedWrapper((o, e) => throw new InvalidOperationException("wait failed"));
            wrapper.Setup(p => p.Kill(It.IsAny<bool>())).Throws(new InvalidOperationException("kill failed"));

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => ProcessLauncher.Start(SyncOptions(null, null), FactoryFor(wrapper), _logger.Object));

            // Assert
            Assert.Equal("wait failed", ex.Message);
            _logger.Verify(
                l => l.Warn(It.Is<string>(m => m.StartsWith("Failed to kill orphaned child after launch failure")
                                            && m.Contains("kill failed")), It.IsAny<Exception?>()),
                Times.Once);
            wrapper.Verify(p => p.Dispose(), Times.Once);
        }

        #endregion
    }
}
