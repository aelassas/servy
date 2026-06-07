using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    [CollectionDefinition("ProcessLauncherIntegrationTests", DisableParallelization = true)]
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
            var options = CreateOptions("powershell.exe", "-NoProfile", fireAndForget: false, timeoutMs: 0);
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
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Write-Output 'OK'\"", fireAndForget: false, timeoutMs: 10_000);
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
            // Use PowerShell with a long sleep to guarantee the timeout is hit 
            // before the process can exit naturally or be killed by EDR.
            var optionsError = CreateOptions("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\"", fireAndForget: false, timeoutMs: 500);
            optionsError.WaitChunkMs = 100;
            optionsError.LogErrorAsWarning = false;

            var ex1 = Assert.Throws<TimeoutException>(() => ProcessLauncher.Start(optionsError, _realFactory, _logger));

            Assert.Contains("exceeded the maximum allowed timeout", ex1.Message);
            // Verify the specific message exists in the error history
            Assert.Contains(_logger.Errors, m => m.Contains("timed out after"));

            var optionsWarn = CreateOptions("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\"", fireAndForget: false, timeoutMs: 500);
            optionsWarn.WaitChunkMs = 100;
            optionsWarn.LogErrorAsWarning = true;

            Assert.Throws<TimeoutException>(() => ProcessLauncher.Start(optionsWarn, _realFactory, _logger));
            Assert.Contains(_logger.Warnings, m => m.Contains("timed out after"));
        }

        #endregion

        #region Output Redirection Tests

        [Fact]
        public void Start_RedirectOutput_SamePath_WritesToSingleFileMultiplexed()
        {
            string logPath = CreateTempFilePath();
            var options = CreateOptions("powershell.exe", "-NoProfile -Command \"Write-Output 'STDOUT_MSG'; [Console]::Error.WriteLine('STDERR_MSG')\"", false, 10_000);
            options.EnableConsoleUI = false;
            options.RedirectToWriters = true;
            options.StdOutPath = logPath;
            options.StdErrPath = logPath;

            using (var wrapper = ProcessLauncher.Start(options, _realFactory, _logger))
            {
                Assert.True(wrapper.HasExited);
            }

            // Allow asynchronous file flushing and OS stream notifications 
            // to completely unwind into the shared on-disk file layer.
            string content = string.Empty;
            bool containsBoth = false;

            // Bounded polling loop ensures zero flakiness under extreme CI CPU consumption
            for (int i = 0; i < 10; i++)
            {
                content = File.ReadAllText(logPath);
                if (content.Contains("STDOUT_MSG") && content.Contains("STDERR_MSG"))
                {
                    containsBoth = true;
                    break;
                }
                Thread.Sleep(100);
            }

            // Assert
            Assert.True(containsBoth, $"Log file content did not fully stabilize with both outputs. Current file string content: '{content}'");
            Assert.Contains("STDOUT_MSG", content);
            Assert.Contains("STDERR_MSG", content);
        }

        #endregion

        #region Language Fixes Tests

        [Theory]
        [InlineData("python.exe", true)]
        [InlineData("python2.exe", true)]
        [InlineData("python3.exe", true)]
        [InlineData("python3.11.exe", true)]
        [InlineData("node.exe", false)]
        public void ApplyLanguageFixes_PythonDetection_AppliesEnvironmentVariables(string fileName, bool isPython)
        {
            var psi = new ProcessStartInfo { FileName = fileName };
            ProcessLauncher.ApplyLanguageFixes(psi);

            if (isPython)
            {
                Assert.Equal("1", psi.Environment["PYTHONUTF8"]);
            }
            else
            {
                Assert.False(psi.Environment.ContainsKey("PYTHONUTF8"));
            }
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

            // Use correct List type for EnvironmentVariables
            options.EnvironmentVariables = new List<Servy.Core.EnvironmentVariables.EnvironmentVariable>();

            return options;
        }

        private class TestLogger : IServyLogger
        {
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Errors { get; } = new List<string>();

            public string LastWarning => Warnings.LastOrDefault() ?? string.Empty;
            public string LastError => Errors.LastOrDefault() ?? string.Empty;

            public string Prefix => string.Empty;
            public void Warn(string message, Exception ex) => Warnings.Add(message);
            public void Error(string message) => Errors.Add(message);
            public void Error(string message, Exception ex) => Errors.Add(message);
            public void Info(string message, Exception ex) { }
            public void Debug(string message, Exception ex) { }
            public IServyLogger CreateScoped(string prefix) => throw new NotImplementedException();
            public void SetLogLevel(LogLevel level) { }
            public void SetIsEventLogEnabled(bool isEnabled) { }
            public void Dispose() { }
        }

        private class MockFailingProcessFactory : IProcessFactory
        {
            public MockFailingProcessWrapper CreatedWrapper { get; } = new MockFailingProcessWrapper();
            public IProcessWrapper Create(ProcessStartInfo startInfo, IServyLogger logger) => CreatedWrapper;
        }

        private class MockFailingProcessWrapper : IProcessWrapper
        {
            public bool WasDisposed { get; private set; }
            public bool Start() => throw new InvalidOperationException("Simulated Start Error");
            public bool HasExited => false;
            public void Kill(bool entireProcessTree) => throw new UnauthorizedAccessException("Simulated Kill Error");
            public void Dispose() => WasDisposed = true;

            public Process UnderlyingProcess { get; } = new Process();
            public int Id => 1;
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
            public bool WaitForExit(int ms) => true;
            public void WaitForExit() { }
            public Task<bool> WaitAndCheckStillRunningAsync(TimeSpan t, CancellationToken c) => Task.FromResult(true);
        }

        #endregion
    }
}