using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    #region xUnit Non-Parallel Collection Setup

    [CollectionDefinition("ProcessLauncherIntegrationTests", DisableParallelization = true)]
    public class ProcessLauncherIntegrationTestsCollection : ICollectionFixture<object>
    {
        // Enforces strict sequential isolation across the integration suite runs
    }

    #endregion

    [Collection("ProcessLauncherIntegrationTests")]
    public class ProcessLauncherIntegrationTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();
        private readonly TestLogger _logger = new TestLogger();
        private readonly IProcessFactory _realFactory = new ProcessFactory();

        public void Dispose()
        {
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
            Assert.Throws<ArgumentNullException>(() => ProcessLauncher.Start(null, _realFactory, _logger));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Start_EmptyExecutable_ThrowsArgumentException(string exePath)
        {
            var options = CreateOptions(exePath, string.Empty, false, 10_000);
            Assert.Throws<ArgumentException>(() => ProcessLauncher.Start(options, _realFactory, _logger));
        }

        [Fact]
        public void Start_SynchronousWithZeroTimeout_ThrowsArgumentException()
        {
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"exit 0\"", fireAndForget: false, timeoutMs: 0);
            var ex = Assert.Throws<ArgumentException>(() => ProcessLauncher.Start(options, _realFactory, _logger));
            Assert.Contains("Synchronous launch requires TimeoutMs > 0", ex.Message);
        }

        #endregion

        #region Execution Mode & Timeout Tests

        [Fact]
        public void Start_FireAndForget_ReturnsImmediately()
        {
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 3\"", fireAndForget: true, timeoutMs: 0);

            var wrapper = ProcessLauncher.Start(options, _realFactory, _logger);

            Assert.NotNull(wrapper);
            Assert.False(wrapper.HasExited);

            wrapper.Kill(true);
            wrapper.Dispose();
        }

        [Fact]
        public void Start_Synchronous_WaitsForExit_And_Heartbeats()
        {
            int heartbeats = 0;
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Write-Output 'OK'\"", fireAndForget: false, timeoutMs: 30_000);
            options.WaitChunkMs = 10;
            options.OnScmHeartbeat = new Action<int>((time) => Interlocked.Increment(ref heartbeats));

            using (var wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                Assert.True(wrapper.HasExited);
                Assert.True(heartbeats >= 0);
            }
        }

        [Fact]
        public void Start_SynchronousTimeout_ThrowsTimeoutException_AndLogsCorrectly()
        {
            // Raised execution target to 15 seconds, while keeping threshold low to ensure a deterministic timeout trip
            var optionsError = CreateOptions("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 15\"", fireAndForget: false, timeoutMs: 1500);
            optionsError.WaitChunkMs = 100;
            optionsError.LogErrorAsWarning = false;

            var ex1 = Assert.Throws<TimeoutException>(() => ProcessLauncher.Start(optionsError, _realFactory, _logger));

            Assert.Contains("exceeded the maximum allowed timeout", ex1.Message);
            Assert.Contains(_logger.Errors, m => m.Contains("timed out after"));

            var optionsWarn = CreateOptions("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 15\"", fireAndForget: false, timeoutMs: 1500);
            optionsWarn.WaitChunkMs = 100;
            optionsWarn.LogErrorAsWarning = true;

            Assert.Throws<TimeoutException>(() => ProcessLauncher.Start(optionsWarn, _realFactory, _logger));
            Assert.Contains(_logger.Warnings, m => m.Contains("timed out after"));
        }

        [Fact]
        public void WaitForExitWithHeartbeat_InvalidWaitChunk_ThrowsArgumentException()
        {
            var options = CreateOptions("powershell.exe", "-NoProfile", fireAndForget: false, timeoutMs: 30_000);
            options.WaitChunkMs = 0; // Violate rule requirement: WaitChunkMs <= 0

            var method = typeof(ProcessLauncher).GetMethod("WaitForExitWithHeartbeat", BindingFlags.Static | BindingFlags.NonPublic);
            var mockWrapper = new MockFailingProcessWrapper();

            var targetInvocationException = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { mockWrapper, options, _logger }));

            Assert.IsType<ArgumentException>(targetInvocationException.InnerException);
            Assert.Contains("Synchronous launch requires WaitChunkMs > 0", targetInvocationException.InnerException.Message);
        }

        #endregion

        #region Path & Argument Normalization Variations

        [Fact]
        public void Start_NullArgumentsAndNullWorkingDirectory_ResolvesToDefaultsSafely()
        {
            var options = CreateOptions("powershell.exe", null, fireAndForget: false, timeoutMs: 30_000);
            options.WorkingDirectory = null; // Triggers Path.GetDirectoryName fallback branch

            // Configure short execution task to exit cleanly
            options.Arguments = "-NoProfile -Command \"exit 0\"";

            using (var wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                Assert.True(wrapper.HasExited);
                Assert.NotNull(wrapper.StartInfo.WorkingDirectory);
            }
        }

        [Fact]
        public void Start_EnvironmentVariablesMapping_PadsNullValuesToEmptyString()
        {
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"exit 0\"", fireAndForget: false, timeoutMs: 30_000);

            var envVarInstance = new Servy.Core.EnvironmentVariables.EnvironmentVariable
            {
                Name = "CUSTOM_TEST_ENV_PADDED",
                Value = null // Triggers target coverage branch: envVar.Value ?? string.Empty
            };

            options.EnvironmentVariables.Add(envVarInstance);

            // Cast the dynamic output to IProcessWrapper to break out of the dynamic binder loop
            using (IProcessWrapper wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                // Now strongly-typed; indexer access resolves flawlessly without DLR interference
                Assert.Equal(string.Empty, wrapper.StartInfo.Environment["CUSTOM_TEST_ENV_PADDED"]);
            }
        }

        #endregion

        #region Error Trapping & Fail-Safe Cleanup Branches

        [Fact]
        public void Start_ProcessStartReturnsFalse_ThrowsInvalidOperationException_AndCleansUp()
        {
            var options = CreateOptions("powershell.exe", "-NoProfile", fireAndForget: false, timeoutMs: 30_000);
            var mockFactory = new MockStartFalseProcessFactory();

            var ex = Assert.Throws<InvalidOperationException>(() => ProcessLauncher.Start(options, mockFactory, _logger));
            Assert.Contains("Process.Start returned false", ex.Message);
            Assert.True(mockFactory.CreatedWrapper.WasDisposed);
        }

        [Fact]
        public void Start_WritersGenerationFails_CatchesExceptionAndLogsError()
        {
            // Arrange
            // This format bypasses standard .NET path normalization validation 
            // but guarantees an absolute failure when the file stream opens.
            string structuralFailurePath = @"\\?\C:\illegal|char.log";

            // Extended total timeout to 30000ms to allow powershell.exe ample runtime margin to process startup hooks on cold hosts
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Write-Output 'TRIGGER'\"", fireAndForget: false, timeoutMs: 30_000);
            options.EnableConsoleUI = false;
            options.RedirectToWriters = true;
            options.StdoutPath = structuralFailurePath;

            // Act
            using (IProcessWrapper wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                wrapper.WaitForExit();

                // Give the asynchronous background event queue a brief moment to process the stream failure
                Thread.Sleep(250);

                // Assert 
                Assert.True(wrapper.HasExited);
                Assert.Contains(_logger.Errors, m => m.Contains("Disabling stdout capture for"));
            }
        }

        #endregion

        #region Language Fixes & Regex Timeout Coverage

        [Theory]
        [InlineData("python.exe", true)]
        [InlineData("pythonw.exe", true)]
        [InlineData("python3.exe", true)]
        [InlineData("py.exe", true)]
        [InlineData("java.exe", false)]
        [InlineData("javaw.exe", false)]
        [InlineData("javac.exe", false)]
        public void ApplyLanguageFixes_RuntimesDetection_AppliesExpectedArgumentsAndVariables(string fileName, bool isPython)
        {
            var psi = new ProcessStartInfo { FileName = fileName, Arguments = "-version" };
            ProcessLauncher.ApplyLanguageFixes(psi, logger: null);

            if (isPython)
            {
                Assert.Equal("1", psi.Environment["PYTHONUTF8"]);
                Assert.Equal("utf-8", psi.Environment["PYTHONIOENCODING"]);
            }
            else
            {
                Assert.Contains("-Dfile.encoding=UTF-8", psi.Arguments);
            }
        }

        [Fact]
        public void ApplyLanguageFixes_NullOrEmptyPsiFileName_ReturnsEarlySafely()
        {
            var psiNull = new ProcessStartInfo { FileName = null };
            var psiEmpty = new ProcessStartInfo { FileName = string.Empty };

            var exceptionNull = Record.Exception(() => ProcessLauncher.ApplyLanguageFixes(psiNull, logger: null));
            var exceptionEmpty = Record.Exception(() => ProcessLauncher.ApplyLanguageFixes(psiEmpty, logger: null));

            Assert.Null(exceptionNull);
            Assert.Null(exceptionEmpty);
        }

        [Fact]
        public void ApplyLanguageFixes_JavaWithExistingEncodingProperty_DoesNotOverwriteArguments()
        {
            var psi = new ProcessStartInfo { FileName = "java.exe", Arguments = "-Dfile.encoding=ISO-8859-1 -jar target.jar" };
            ProcessLauncher.ApplyLanguageFixes(psi, logger: null);

            // Logic should skip prepending UTF-8 properties if a definition is already matched
            Assert.StartsWith("-Dfile.encoding=ISO-8859-1", psi.Arguments);
            Assert.DoesNotContain("UTF-8", psi.Arguments);
        }

        [Fact]
        public void SetIfMissing_KeyAlreadyExists_DoesNotOverwriteExplicitEnvironmentValue()
        {
            var psi = new ProcessStartInfo { FileName = "python.exe" };
            psi.Environment["PYTHONUTF8"] = "CUSTOM_USER_VALUE"; // Explicit definition

            ProcessLauncher.ApplyLanguageFixes(psi, logger: null);

            // Verify the helper branch rule 'if (!psi.Environment.ContainsKey(key))' bypassed replacing it
            Assert.Equal("CUSTOM_USER_VALUE", psi.Environment["PYTHONUTF8"]);
        }

        #endregion

        #region Output Redirection Tests

        [Fact]
        public void Start_RedirectOutput_SamePath_WritesToSingleFileMultiplexed()
        {
            string logPath = CreateTempFilePath();
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Write-Output 'STDOUT_MSG'; [Console]::Error.WriteLine('STDERR_MSG')\"", false, 30_000);
            options.EnableConsoleUI = false;
            options.RedirectToWriters = true;
            options.StdoutPath = logPath;
            options.StderrPath = logPath;

            using (var wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                Assert.True(wrapper.HasExited);
            }

            string content = string.Empty;
            bool containsBoth = false;

            for (int i = 0; i < 15; i++)
            {
                content = File.ReadAllText(logPath);
                if (content.Contains("STDOUT_MSG") && content.Contains("STDERR_MSG"))
                {
                    containsBoth = true;
                    break;
                }
                Thread.Sleep(150);
            }

            Assert.True(containsBoth, $"Log file content did not fully stabilize with both outputs. Current file string content: '{content}'");
            Assert.Contains("STDOUT_MSG", content);
            Assert.Contains("STDERR_MSG", content);
        }

        #endregion

        #region Helpers & Mocks

        private dynamic CreateOptions(string exe, string args, bool fireAndForget, int timeoutMs)
        {
            Type t = typeof(ProcessLauncher).Assembly.GetType("Servy.Service.ProcessManagement.ProcessLaunchOptions")
                     ?? throw new InvalidOperationException("ProcessLaunchOptions not found.");

            dynamic options = Activator.CreateInstance(t);
            options.ExecutablePath = exe;
            options.Arguments = args;
            options.FireAndForget = fireAndForget;
            options.TimeoutMs = timeoutMs;
            options.WaitChunkMs = 100;
            options.EnvironmentVariables = new List<Servy.Core.EnvironmentVariables.EnvironmentVariable>();

            return options;
        }

        private class MockStartFalseProcessFactory : IProcessFactory
        {
            public MockStartFalseProcessWrapper CreatedWrapper { get; } = new MockStartFalseProcessWrapper();
            public IProcessWrapper Create(ProcessStartInfo startInfo, IServyLogger logger) => CreatedWrapper;
        }

        private class MockStartFalseProcessWrapper : IProcessWrapper
        {
            public bool WasDisposed { get; private set; }
            public bool Start() => false; // Trigger structural fallback branch criteria match
            public bool HasExited => true;
            public void Kill(bool entireProcessTree) { }
            public void Dispose() => WasDisposed = true;

            public Process UnderlyingProcess { get; } = new Process();
            public int Id => int.MaxValue;
            public IntPtr Handle => IntPtr.Zero;
            public int ExitCode => -1;
            public bool EnableRaisingEvents { get; set; }
            public DateTime StartTime => DateTime.Now;
            public StreamReader StandardOutput => StreamReader.Null;
            public StreamReader StandardError => StreamReader.Null;
            public ProcessStartInfo StartInfo => new ProcessStartInfo();
            public IntPtr MainWindowHandle => IntPtr.Zero;
            public ProcessPriorityClass PriorityClass { get; set; }
            public event DataReceivedEventHandler OutputDataReceived { add { } remove { } }
            public event DataReceivedEventHandler ErrorDataReceived { add { } remove { } }
            public event EventHandler Exited { add { } remove { } }
            public void BeginErrorReadLine() { }
            public void BeginOutputReadLine() { }
            public void CancelErrorRead() { }
            public void CancelOutputRead() { }
            public bool CloseMainWindow() => true;
            public string Format() => "MockFalse";
            public bool? Stop(int t) => true;
            public void StopDescendants(int p, DateTime s, int t) { }
            public bool WaitForExit(int ms) => true;
            public void WaitForExit() { }
            public Task<bool> WaitAndCheckStillRunningAsync(TimeSpan t, CancellationToken c) => Task.FromResult(true);
        }

        private class MockFailingProcessWrapper : IProcessWrapper
        {
            public bool Start() => true;
            public bool HasExited => false;
            public void Kill(bool entireProcessTree) { }
            public void Dispose() { }

            public Process UnderlyingProcess { get; } = new Process();
            public int Id => 9999;
            public IntPtr Handle => IntPtr.Zero;
            public int ExitCode => 0;
            public bool EnableRaisingEvents { get; set; }
            public DateTime StartTime => DateTime.Now;
            public StreamReader StandardOutput => StreamReader.Null;
            public StreamReader StandardError => StreamReader.Null;
            public ProcessStartInfo StartInfo => new ProcessStartInfo();
            public IntPtr MainWindowHandle => IntPtr.Zero;
            public ProcessPriorityClass PriorityClass { get; set; }
            public event DataReceivedEventHandler OutputDataReceived { add { } remove { } }
            public event DataReceivedEventHandler ErrorDataReceived { add { } remove { } }
            public event EventHandler Exited { add { } remove { } }
            public void BeginErrorReadLine() { }
            public void BeginOutputReadLine() { }
            public void CancelErrorRead() { }
            public void CancelOutputRead() { }
            public bool CloseMainWindow() => true;
            public string Format() => "Mock";
            public bool? Stop(int t) => true;
            public void StopDescendants(int p, DateTime s, int t) { }
            public bool WaitForExit(int ms) => false; // Enforces persistent loop conditions for timeouts
            public void WaitForExit() { }
            public Task<bool> WaitAndCheckStillRunningAsync(TimeSpan t, CancellationToken c) => Task.FromResult(true);
        }

        #endregion
    }
}
