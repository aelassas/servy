using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    #region xUnit Non-Parallel Collection Setup

    [CollectionDefinition("ProcessLauncherIntegrationTests", DisableParallelization = true)]
    public class ProcessLauncherIntegrationTestsCollection
    {
        // Enforces strict sequential isolation across the integration suite runs
    }

    #endregion

    [Collection("ProcessLauncherIntegrationTests")]
    public class ProcessLauncherIntegrationTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();
        private readonly List<IProcessWrapper> _spawnedWrappers = new List<IProcessWrapper>();
        private readonly TestLogger _logger = new TestLogger();
        private readonly IProcessFactory _realFactory = new ProcessFactory();

        public void Dispose()
        {
            foreach (var wrapper in _spawnedWrappers)
            {
                TestProcessCleanup.KillAndDispose(wrapper);
            }

            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        private string CreateTempFilePath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"Servy_ProcessLauncherTest_{Guid.NewGuid()}.log");
            _tempFiles.Add(path);
            return path;
        }

        #region Precondition Validation Tests

        [Fact]
        public void Start_NullOptions_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => ProcessLauncher.Start(null, _realFactory, _logger));
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        [InlineData(null)]
        public void Start_EmptyExecutable_ThrowsArgumentException(string exePath)
        {
            // Arrange
            var options = CreateOptions(exePath, string.Empty, false, TestTimeouts.ProcessLauncherTimeoutMs);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => ProcessLauncher.Start(options, _realFactory, _logger));
        }

        [Fact]
        public void Start_SynchronousWithZeroTimeout_ThrowsArgumentException()
        {
            // Arrange
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"exit 0\"", fireAndForget: false, timeoutMs: 0);

            // Act
            var ex = Assert.Throws<ArgumentException>(() => ProcessLauncher.Start(options, _realFactory, _logger));

            // Assert
            Assert.Contains("Synchronous launch requires TimeoutMs > 0", ex.Message);
        }

        [Fact]
        public void Start_SynchronousWithZeroWaitChunk_ThrowsArgumentException()
        {
            // Arrange
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"exit 0\"", fireAndForget: false, timeoutMs: TestTimeouts.ProcessLauncherTimeoutMs);
            options.WaitChunkMs = 0; // Violate rule requirement: WaitChunkMs <= 0

            // Act
            var ex = Assert.Throws<ArgumentException>(() => ProcessLauncher.Start(options, _realFactory, _logger));

            // Assert
            Assert.Contains("Synchronous launch requires WaitChunkMs > 0", ex.Message);
        }

        #endregion

        #region Execution Mode & Timeout Tests

        [Fact]
        public void Start_FireAndForget_ReturnsImmediately()
        {
            // Arrange
            var options = CreateOptions("powershell.exe", $"-NoProfile -Command \"Start-Sleep -Seconds {TestTimeouts.CiGenerousSeconds}\"", fireAndForget: true, timeoutMs: 0);

            // Act
            var wrapper = ProcessLauncher.Start(options, _realFactory, _logger);
            _spawnedWrappers.Add(wrapper);

            try
            {
                // Assert
                Assert.NotNull(wrapper);
                Assert.False(wrapper.HasExited);
            }
            finally
            {
                wrapper.Kill(true);
                wrapper.Dispose();
            }
        }

        [Fact]
        public void Start_Synchronous_WaitsForExit_And_Heartbeats()
        {
            // Arrange
            int heartbeats = 0;

            // Inject a short sleep inside the command loop string to guarantee the process outlasts
            // the 10ms chunk window, forcing the heartbeat callback to fire reliably.
            var options = CreateOptions(
                "powershell.exe",
                "-NoProfile -Command \"Start-Sleep -m 150; Write-Output 'OK'\"",
                fireAndForget: false,
                timeoutMs: TestTimeouts.ProcessLauncherTimeoutMs);

            options.WaitChunkMs = 10;
            options.OnScmHeartbeat = new Action<int>((time) => Interlocked.Increment(ref heartbeats));

            // Act
            using (var wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                // Assert
                Assert.True(wrapper.HasExited);
                Assert.True(heartbeats > 0, $"Expected SCM heartbeats to be reported, but captured count was {heartbeats}.");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Start_SynchronousTimeout_RoutesTimeoutToConfiguredChannel(bool logErrorAsWarning)
        {
            // Arrange
            // Child outlives the budget by design: 15s of work against a 2s budget.
            var options = CreateOptions(
                "powershell.exe",
                $"-NoProfile -Command \"Start-Sleep -Seconds {TestTimeouts.ProcessLauncherSynchronousTimeoutSeconds}\"",
                fireAndForget: false,
                timeoutMs: TestTimeouts.ProcessLauncherTimeoutTripBudgetMs);
            options.WaitChunkMs = 100;
            options.LogErrorAsWarning = logErrorAsWarning;

            var logger = new TestLogger();

            // Act
            var ex = Assert.Throws<TimeoutException>(() => ProcessLauncher.Start(options, _realFactory, logger));

            // Assert
            Assert.Contains("exceeded the maximum allowed timeout", ex.Message);
            var expectedChannel = logErrorAsWarning ? logger.Warnings : logger.Errors;
            var forbiddenChannel = logErrorAsWarning ? logger.Errors : logger.Warnings;

            Assert.Contains(expectedChannel, m => m.Contains("timed out after"));
            Assert.DoesNotContain(forbiddenChannel, m => m.Contains("timed out after"));
        }

        #endregion

        #region Path & Argument Normalization Variations

        [Fact]
        public void Start_NullWorkingDirectory_ResolvesToDefaultSafely()
        {
            // Arrange
            string exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            var options = CreateOptions(exe, "-NoProfile -Command \"exit 0\"", fireAndForget: false, timeoutMs: TestTimeouts.ProcessLauncherTimeoutMs);
            options.StartupDirectory = null; // Triggers Path.GetDirectoryName fallback branch

            // Act
            using (var wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                // Assert
                Assert.True(wrapper.HasExited);
                Assert.Equal(Path.GetDirectoryName(exe), wrapper.StartInfo.WorkingDirectory);
            }
        }

        [Fact]
        public void Start_EnvironmentVariablesMapping_PadsNullValuesToEmptyString()
        {
            // Arrange
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"exit 0\"", fireAndForget: false, timeoutMs: TestTimeouts.ProcessLauncherTimeoutMs);

            var envVarInstance = new EnvironmentVariable
            {
                Name = "CUSTOM_TEST_ENV_PADDED",
                Value = null // Triggers target coverage branch: envVar.Value ?? string.Empty
            };

            options.EnvironmentVariables.Add(envVarInstance);

            // Act
            using (IProcessWrapper wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                // Assert
                Assert.Equal(string.Empty, wrapper.StartInfo.Environment["CUSTOM_TEST_ENV_PADDED"]);
            }
        }

        #endregion

        #region Error Trapping & Fail-Safe Cleanup Branches

        [Fact]
        public void Start_ProcessStartThrowsWin32Exception_PropagatesException_AndCleansUp()
        {
            // Arrange
            var options = CreateOptions("powershell.exe", "-NoProfile", fireAndForget: false, timeoutMs: TestTimeouts.ProcessLauncherTimeoutMs);
            var mockFactory = new MockThrowingProcessFactory(new Win32Exception(2, "The system cannot find the file specified"));

            // Act
            var ex = Assert.Throws<Win32Exception>(() => ProcessLauncher.Start(options, mockFactory, _logger));

            // Assert
            Assert.Equal(2, ex.NativeErrorCode);
            Assert.True(mockFactory.CreatedWrapper.WasDisposed);
        }

        [Fact]
        public void Start_WritersGenerationFails_CatchesExceptionAndLogsError()
        {
            // Arrange
            // This format bypasses standard .NET path normalization validation
            // but guarantees an absolute failure when the file stream opens.
            string structuralFailurePath = @"\\?\C:\illegal|char.log";

            // powershell.exe needs headroom for startup hooks on cold CI hosts.
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Write-Output 'TRIGGER'\"", fireAndForget: false, timeoutMs: TestTimeouts.ProcessLauncherTimeoutMs);
            options.EnableConsoleUI = false;
            options.RedirectToWriters = true;
            options.StdoutPath = structuralFailurePath;

            // Act
            using (IProcessWrapper wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                wrapper.WaitForExit();

                bool logged = SpinWait.SpinUntil(
                    () => _logger.Errors.Any(m => m.Contains("Disabling stdout capture for")),
                    TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds));

                // Assert
                Assert.True(wrapper.HasExited);
                Assert.True(logged, $"Expected 'Disabling stdout capture' error. Got: [{string.Join("; ", _logger.Errors)}]");
            }
        }

        #endregion

        #region Language Fixes & Regex Timeout Coverage

        [Theory]
        [InlineData("python.exe", true, "1", "utf-8", "0", "1", "-version")]
        [InlineData("pythonw.exe", true, "1", "utf-8", "0", "1", "-version")]
        [InlineData("python3.exe", true, "1", "utf-8", "0", "1", "-version")]
        [InlineData("py.exe", true, "1", "utf-8", "0", "1", "-version")]
        [InlineData("java.exe", false, null, null, null, null, "-Dfile.encoding=UTF-8 -version")]
        [InlineData("javaw.exe", false, null, null, null, null, "-Dfile.encoding=UTF-8 -version")]
        [InlineData("javac.exe", false, null, null, null, null, "-J-Dfile.encoding=UTF-8 -version")]
        public void ApplyLanguageFixes_RuntimesDetection_AppliesExpectedArgumentsAndVariables(
            string fileName,
            bool isPython,
            string expectedUtf8,
            string expectedIoEncoding,
            string expectedLegacyStdio,
            string expectedUnbuffered,
            string expectedArguments)
        {
            // Arrange
            var psi = new ProcessStartInfo { FileName = fileName, Arguments = "-version" };

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger: null);

            // Assert
            Assert.Equal(expectedArguments, psi.Arguments);

            if (isPython)
            {
                Assert.Equal(expectedUtf8, psi.Environment["PYTHONUTF8"]);
                Assert.Equal(expectedIoEncoding, psi.Environment["PYTHONIOENCODING"]);
                Assert.Equal(expectedLegacyStdio, psi.Environment["PYTHONLEGACYWINDOWSSTDIO"]);
                Assert.Equal(expectedUnbuffered, psi.Environment["PYTHONUNBUFFERED"]);
            }
        }

        [Fact]
        public void ApplyLanguageFixes_NullOrEmptyPsiFileName_ReturnsEarlySafely()
        {
            // Arrange
            var psiNull = new ProcessStartInfo { FileName = null };
            var psiEmpty = new ProcessStartInfo { FileName = string.Empty };

            // Act
            var exceptionNull = Record.Exception(() => ProcessLauncher.ApplyLanguageFixes(psiNull, logger: null));
            var exceptionEmpty = Record.Exception(() => ProcessLauncher.ApplyLanguageFixes(psiEmpty, logger: null));

            // Assert
            Assert.Null(exceptionNull);
            Assert.Null(exceptionEmpty);
        }

        [Fact]
        public void ApplyLanguageFixes_JavaWithExistingEncodingProperty_DoesNotOverwriteArguments()
        {
            // Arrange
            var psi = new ProcessStartInfo { FileName = "java.exe", Arguments = "-Dfile.encoding=ISO-8859-1 -jar target.jar" };

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger: null);

            // Assert
            // Logic should skip prepending UTF-8 properties if a definition is already matched
            Assert.StartsWith("-Dfile.encoding=ISO-8859-1", psi.Arguments);
            Assert.DoesNotContain("UTF-8", psi.Arguments);
        }

        [Fact]
        public void ApplyLanguageFixes_ExplicitEnvValueAlreadySet_DoesNotOverwrite()
        {
            // Arrange
            var psi = new ProcessStartInfo { FileName = "python.exe" };
            psi.Environment["PYTHONUTF8"] = "CUSTOM_USER_VALUE"; // Explicit definition

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger: null);

            // Assert
            // Verify the helper branch rule 'if (!psi.Environment.ContainsKey(key))' bypassed replacing it
            Assert.Equal("CUSTOM_USER_VALUE", psi.Environment["PYTHONUTF8"]);
        }

        #endregion

        #region TryOpenAppendWriter Tests

        private static StreamWriter InvokeTryOpenAppendWriter(string path, Encoding encoding, string exePath, string scope, IServyLogger logger)
        {
            var method = typeof(ProcessLauncher).GetMethod("TryOpenAppendWriter", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (StreamWriter)method.Invoke(null, new object[] { path, encoding, exePath, scope, logger });
        }

        [Fact]
        public void TryOpenAppendWriter_ValidPath_SuccessfullyOpensWriter()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), $"Servy_Test_{Guid.NewGuid():N}");
            string logPath = Path.Combine(tempDir, "hooks", "pre-launch.log");
            var mockLogger = new Mock<IServyLogger>();

            try
            {
                // Act
                using (var writer = InvokeTryOpenAppendWriter(logPath, Encoding.UTF8, "test.exe", "stdout", mockLogger.Object))
                {
                    // Assert
                    Assert.NotNull(writer);
                    Assert.True(File.Exists(logPath));
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void TryOpenAppendWriter_EmptyOrNullPath_ReturnsNullAndLogsError()
        {
            // Arrange
            var mockLogger = new Mock<IServyLogger>();

            // Act
            using (var writer = InvokeTryOpenAppendWriter("", Encoding.UTF8, "test.exe", "stdout", mockLogger.Object))
            {
                // Assert
                Assert.Null(writer);
                mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("log path is empty")), It.IsAny<Exception>()), Times.Once);
            }
        }

        [Fact]
        public void TryOpenAppendWriter_TargetFileIsReparsePoint_RefusesToOpenAndLogsError()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), $"Servy_Test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string targetFile = Path.Combine(tempDir, "target.log");
            string linkFile = Path.Combine(tempDir, "symlink.log");
            File.WriteAllText(targetFile, "content");

            var mockLogger = new Mock<IServyLogger>();

            try
            {
                // Create file reparse point/symlink
                Helper.CreateFileSymlink(linkFile, targetFile);

                // Act
                using (var writer = InvokeTryOpenAppendWriter(linkFile, Encoding.UTF8, "test.exe", "stdout", mockLogger.Object))
                {
                    // Assert
                    Assert.Null(writer);
                    mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("is a junction or symbolic link")), It.IsAny<Exception>()), Times.Once);
                }
            }
            catch (IOException)
            {
                // Symbolic link creation on Windows may require elevated permissions in non-developer mode environments.
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        #endregion

        #region Output Redirection Tests

        [Fact]
        public void Start_RedirectOutput_SamePath_WritesToSingleFileMultiplexed()
        {
            // Arrange
            string logPath = CreateTempFilePath();
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Write-Output 'STDOUT_MSG'; [Console]::Error.WriteLine('STDERR_MSG')\"", false, TestTimeouts.ProcessLauncherTimeoutMs);
            options.EnableConsoleUI = false;
            options.RedirectToWriters = true;
            options.StdoutPath = logPath;
            options.StderrPath = logPath;

            // Act
            using (var wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                Assert.True(wrapper.HasExited);
            }

            string content = string.Empty;
            bool containsBoth = false;

            for (int i = 0; i < TestTimeouts.MaxPollAttempts; i++)
            {
                try { content = File.ReadAllText(logPath); }
                catch (IOException) { /* writer still holds the handle - retry */ }
                if (content.Contains("STDOUT_MSG") && content.Contains("STDERR_MSG"))
                {
                    containsBoth = true;
                    break;
                }
                Thread.Sleep(TestTimeouts.PollIntervalMs);
            }

            // Assert
            Assert.True(containsBoth, $"Log file content did not fully stabilize with both outputs. Current file string content: '{content}'");
        }

        #endregion

        #region Helpers & Mocks

        private ProcessLaunchOptions CreateOptions(string exe, string args, bool fireAndForget, int timeoutMs)
        {
            return new ProcessLaunchOptions
            {
                ExecutablePath = exe,
                Arguments = args,
                FireAndForget = fireAndForget,
                TimeoutMs = timeoutMs,
                WaitChunkMs = 100,
                EnvironmentVariables = new List<EnvironmentVariable>(),
            };
        }

        private class MockThrowingProcessFactory : IProcessFactory
        {
            private readonly Exception _exceptionToThrow;

            public MockThrowingProcessFactory(Exception exceptionToThrow)
            {
                _exceptionToThrow = exceptionToThrow;
                CreatedWrapper = new MockThrowingProcessWrapper(_exceptionToThrow);
            }

            public MockThrowingProcessWrapper CreatedWrapper { get; }
            public IProcessWrapper Create(ProcessStartInfo startInfo, IServyLogger logger) => CreatedWrapper;
        }

        /// <summary>
        /// Shared no-op implementation of the IProcessWrapper members that the mocks in this file
        /// do not exercise, so each mock only overrides the behaviour it is testing.
        /// </summary>
        private abstract class BaseMockProcessWrapper : IProcessWrapper
        {
            public abstract bool Start();
            public abstract bool HasExited { get; }
            public virtual void Kill(bool entireProcessTree) { }

            public virtual void Dispose()
            {
                UnderlyingProcess?.Dispose();
            }

            public Process UnderlyingProcess { get; } = new Process();
            public virtual int Id => 9999;
            public IntPtr Handle => IntPtr.Zero;
            public virtual int ExitCode => 0;
            public bool EnableRaisingEvents { get; set; }
            public DateTime StartTime => DateTime.Now;
            public StreamReader StandardOutput => StreamReader.Null;
            public StreamReader StandardError => StreamReader.Null;
            public ProcessStartInfo StartInfo => new ProcessStartInfo();
            public IntPtr MainWindowHandle => IntPtr.Zero;
            public ProcessPriorityClass PriorityClass { get; set; }
            public IntPtr ProcessorAffinity { get; set; }
            public event DataReceivedEventHandler OutputDataReceived { add { } remove { } }
            public event DataReceivedEventHandler ErrorDataReceived { add { } remove { } }
            public event EventHandler Exited { add { } remove { } }
            public void BeginErrorReadLine() { }
            public void BeginOutputReadLine() { }
            public void CancelErrorRead() { }
            public void CancelOutputRead() { }
            public bool CloseMainWindow() => true;
            public virtual string Format() => "MockBase";
            public bool? Stop(int t) => true;
            public void StopDescendants(int p, DateTime s, int t) { }
            public abstract bool WaitForExit(int ms);
            public void WaitForExit() { }
            public Task<bool> WaitAndCheckStillRunningAsync(TimeSpan t, CancellationToken c) => Task.FromResult(true);
        }

        private class MockThrowingProcessWrapper : BaseMockProcessWrapper
        {
            private readonly Exception _exceptionToThrow;

            public MockThrowingProcessWrapper(Exception exceptionToThrow)
            {
                _exceptionToThrow = exceptionToThrow;
            }

            public bool WasDisposed { get; private set; }
            public override int Id => int.MaxValue;
            public override int ExitCode => -1;

            public override bool Start()
            {
                throw _exceptionToThrow;
            }

            public override bool HasExited => true;
            public override string Format() => "MockThrowing";
            public override bool WaitForExit(int ms) => true;

            public override void Dispose()
            {
                base.Dispose();
                WasDisposed = true;
            }
        }

        #endregion
    }
}
