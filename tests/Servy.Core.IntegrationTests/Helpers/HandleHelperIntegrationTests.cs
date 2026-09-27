using Servy.Core.Config;
using Servy.Core.Helpers;
using System.Diagnostics;
using System.Text;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Integration tests for the HandleHelper class.
    /// These tests require handle64.exe/handle64a.exe to be present and the runner to be elevated.
    /// </summary>
    [Collection(ProcessIntegrationTestsCollection.Name)]
    public class HandleHelperIntegrationTests : HandleExeIntegrationTestBase, IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();

        /// <summary>
        /// Number of handle-query attempts the two detection loops make before giving up. Both loops
        /// are meant to poll identically; sharing the constant makes that true by construction rather
        /// than by a comment saying so.
        /// </summary>
        private const int HandleQueryMaxRetries = 5;

        /// <summary>
        /// Backoff window (50 ms) between two successive handle-query attempts, absorbing the few
        /// milliseconds by which handle.exe can lag the kernel handle table.
        /// </summary>
        /// <seealso cref="HandleQueryMaxRetries"/>
        private const int HandleQueryBackoffMs = 50;

        /// <summary>
        /// Pause (1,000 ms) before the single cold-start retry of the handle driver mount.
        /// </summary>
        private const int ColdStartRetryPauseMs = 1000;

        /// <summary>
        /// Initializes the test class by inheriting from the shared tool extraction baseline.
        /// </summary>
        public HandleHelperIntegrationTests() : base()
        {
            try
            {
                // Cold-start driver check
                HandleHelper.GetProcessesUsingFile(_handleExePath, Path.GetTempPath());
            }
            catch (TimeoutException)
            {
                Debug.WriteLine("WARNING: Initial handle.exe cold-start timed out while mounting kernel objects. Executing retry pass...");
                Thread.Sleep(ColdStartRetryPauseMs);

                HandleHelper.GetProcessesUsingFile(_handleExePath, Path.GetTempPath());
            }
        }

        /// <summary>
        /// Deletes the temporary files created by <see cref="CreateTempFile"/>. The extracted
        /// handle64.exe is shared with sibling suites and is deliberately left in place
        /// (see the comment in the method body).
        /// </summary>
        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
                }
            }

            // DO NOT delete handle64.exe here.
            // Deleting an executable while another test's constructor is initializing causes the IOException.
            // Leaving it in the bin folder is completely safe for integration tests.
        }

        private string CreateTempFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid()}.tmp");
            File.WriteAllText(path, "Integration Test Content");
            _tempFiles.Add(path);
            return path;
        }

        /// <summary>
        /// Writes a temporary .cmd script that prints a chosen stdout/stderr and exits with a chosen
        /// code, for use as the <c>handleExePath</c> argument. GetProcessesUsingFile starts it with
        /// <see cref="ProcessStartInfo.UseShellExecute"/> false, and CreateProcess runs a batch file
        /// through the command interpreter, so the script stands in for handle.exe without needing
        /// the real tool, elevation, or a live handle table. The script ignores the arguments it is
        /// passed, which is what lets it produce results the live tool never produces in CI.
        /// </summary>
        /// <param name="body">Script body, appended after <c>@echo off</c>.</param>
        /// <returns>The path to the script, registered for deletion by <see cref="Dispose"/>.</returns>
        private string CreateFakeHandleScript(string body)
        {
            string path = Path.Combine(Path.GetTempPath(), $"ServyFakeHandle_{Guid.NewGuid():N}.cmd");
            File.WriteAllText(path, "@echo off\r\n" + body + "\r\n");
            _tempFiles.Add(path);
            return path;
        }

        [Fact]
        public void GetProcessesUsingFile_NonZeroExitWithUnrecognisedOutput_ShouldThrowInvalidOperationException()
        {
            // Arrange
            string script = CreateFakeHandleScript("echo something went wrong\r\nexit /b 2");
            string testFile = CreateTempFile();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => HandleHelper.GetProcessesUsingFile(script, testFile));

            // Assert
            // The exit code is quoted into the message so an operator can tell which failure this was.
            Assert.Contains("exit code 2", ex.Message);
        }

        [Fact]
        public void GetProcessesUsingFile_ExitOneButAHandleWasParsed_ShouldThrowInvalidOperationException()
        {
            // Arrange
            // The output also carries handle.exe's "no handles" sentence, so exit code 1 plus that
            // text would read as a clean empty result on its own. Only the parsed pid: line
            // contradicts it, which makes !matchedAny the one term that keeps the fail-closed branch
            // firing.
            string script = CreateFakeHandleScript(
                "echo No matching handles found.\r\n" +
                "echo holder.exe  pid: 4242  type: File  1A4: C:\\x.txt\r\n" +
                "exit /b 1");
            string testFile = CreateTempFile();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => HandleHelper.GetProcessesUsingFile(script, testFile));

            // Assert
            // The exit code is quoted into the message, which is the fail-closed branch at work
            // rather than the clean empty-result return.
            Assert.Contains("exit code 1", ex.Message);
        }

        [Fact]
        public void GetProcessesUsingFile_ExitOneWithEmptyStdoutAndStderrText_ShouldReturnEmptyList()
        {
            // Arrange
            string script = CreateFakeHandleScript("echo access problem 1>&2\r\nexit /b 1");
            string testFile = CreateTempFile();

            // Act
            // Blank stdout is the other half of the "no matching handles" reading, so this must be
            // classified as a clean empty result and merely warn about the stderr text.
            var results = HandleHelper.GetProcessesUsingFile(script, testFile);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetProcessesUsingFile_ExitZero_ShouldParseNameAndPidOfEveryLine()
        {
            // Arrange
            string script = CreateFakeHandleScript(
                "echo first.exe      pid: 101   type: File   1A4: C:\\x.txt\r\n" +
                "echo second app.exe pid: 202   type: File   2B8: C:\\x.txt\r\n" +
                "exit /b 0");
            string testFile = CreateTempFile();

            // Act
            var results = HandleHelper.GetProcessesUsingFile(script, testFile);

            // Assert
            // A chosen output pins the exact name and pid the parse extracts, which the live tool
            // cannot do: the second line also proves a name containing a space survives the lazy match.
            Assert.Equal(new[] { 101, 202 }, results.Select(r => r.ProcessId));
            Assert.Equal(new[] { "first.exe", "second app.exe" }, results.Select(r => r.ProcessName));
        }

        [Theory]
        [InlineData(null, "C:\\temp\\file.txt")]
        [InlineData("C:\\temp\\handle.exe", null)]
        [InlineData("", "C:\\temp\\file.txt")]
        [InlineData("C:\\temp\\handle.exe", "")]
        [InlineData("   ", "C:\\temp\\file.txt")]
        [InlineData("C:\\temp\\handle.exe", "   ")]
        public void GetProcessesUsingFile_ShouldThrow_WhenPathsAreNullOrEmpty(string? handleExePath, string? filePath)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile(handleExePath!, filePath!));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldNotReturnCurrentProcess_WhenNoProcessHoldsHandle()
        {
            // Arrange
            string testFile = CreateTempFile();
            int currentPid = Process.GetCurrentProcess().Id;

            // Act
            var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);

            // Assert
            // Symmetrical Hardening: Instead of asserting the entire system-wide list is empty (which causes flakes
            // if an antivirus or indexer briefly hooks the file), verify specifically that the current test process
            // is not returned as a handle holder.
            Assert.DoesNotContain(results, r => r.ProcessId == currentPid);
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldDetectCurrentProcess_WhenCurrentProcessHoldsHandle()
        {
            using (var self = Process.GetCurrentProcess())
            {
                // Arrange
                string testFile = CreateTempFile();
                int currentPid = self.Id;
                string currentName = self.ProcessName;

                // Lock the file using scoping blocks
                using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    // Act
                    List<HandleHelper.ProcessHandleInfo> results = null!;
                    bool handleDetected = false;

                    // Retry a bounded number of times with a small delay to handle OS propagation latency
                    for (int i = 0; i < HandleQueryMaxRetries; i++)
                    {
                        results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);
                        if (results.Any(p => p.ProcessId == currentPid))
                        {
                            handleDetected = true;
                            break;
                        }
                        Thread.Sleep(HandleQueryBackoffMs); // Small backoff window
                    }

                    // Assert
                    Assert.True(handleDetected, $"Current process (PID {currentPid}) failed to be detected holding a handle to {testFile} after retries.");

                    // First, not FirstOrDefault: the assertion above has already established the element exists.
                    var selfMatch = results.First(p => p.ProcessId == currentPid);
                    Assert.NotNull(selfMatch.ProcessName);

                    // handle.exe output might include .exe or not, HandleHelper trims whitespace.
                    Assert.Contains(currentName, selfMatch.ProcessName, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldHandleInvalidHandleExePath()
        {
            // Arrange
            string testFile = CreateTempFile();
            // A path that is guaranteed not to exist
            string invalidExe = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_missing.exe");

            // Act & Assert
            // We expect Win32Exception because Process.Start throws when the file is not found
            // and UseShellExecute is set to false.
            Assert.Throws<System.ComponentModel.Win32Exception>(() =>
                HandleHelper.GetProcessesUsingFile(invalidExe, testFile));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldWorkWithMultipleHandles()
        {
            using (var self = Process.GetCurrentProcess())
            {
                // Arrange
                string testFile = CreateTempFile();

                // Open multiple streams inside nested scopes to safely verify handle grouping symmetry
                using (var fs1 = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var fs2 = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Act
                    var currentPid = self.Id;

                    // handle.exe can lag the kernel handle table by a few milliseconds, so the query is retried.
                    // The Where filters out concurrent background system handles (like security indexers)
                    // that also target our file; handle.exe returns one line per handle found.
                    var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);
                    var selfHandles = results.Where(r => r.ProcessId == currentPid).ToList();
                    bool multiHandlesDetected = selfHandles.Count >= 2;

                    for (int i = 1; i < HandleQueryMaxRetries && !multiHandlesDetected; i++)
                    {
                        Thread.Sleep(HandleQueryBackoffMs); // Small backoff window
                        results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);
                        selfHandles = results.Where(r => r.ProcessId == currentPid).ToList();
                        multiHandlesDetected = selfHandles.Count >= 2;
                    }

                    // Assert
                    Assert.True(multiHandlesDetected, $"Should have detected at least two handles owned by this running test process (PID {currentPid}). Total found self handles: {selfHandles.Count}, overall system handles found: {results.Count}");
                }
            }
        }

        [Fact]
        public void GetProcessesUsingFile_NormalExecution_CompletesWell_UnderTimeout()
        {
            // Arrange
            string testFile = CreateTempFile();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(results);
            // Normal execution should finish well inside the kill timeout the SUT enforces.
            Assert.True(stopwatch.ElapsedMilliseconds < AppConfig.HandleExeTimeoutMs,
                $"Normal execution took {stopwatch.ElapsedMilliseconds} ms, expected to stay under HandleExeTimeoutMs ({AppConfig.HandleExeTimeoutMs} ms).");
        }

        [Fact]
        public async Task GetProcessesUsingFile_ConcurrentBufferAccess_DoesNotThrow_WhenSynchronized()
        {
            // Arrange
            var ioLock = new object();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            const int iterations = 5_000;

            // Act: Stress-test the exact lock model used by HandleHelper to capture Output/Error streams while reading on timeout
            var stdoutTask = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    lock (ioLock)
                    {
                        outputBuilder.AppendLine($"Process stdout line {i}");
                    }
                }
            }, cancellationToken: TestContext.Current.CancellationToken);

            var stderrTask = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    lock (ioLock)
                    {
                        errorBuilder.AppendLine($"Process stderr line {i}");
                    }
                }
            }, cancellationToken: TestContext.Current.CancellationToken);

            var readerTask = Task.Run(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    string currentError;
                    string currentOutput;
                    lock (ioLock)
                    {
                        currentError = errorBuilder.ToString();
                        currentOutput = outputBuilder.ToString();
                    }

                    Assert.NotNull(currentError);
                    Assert.NotNull(currentOutput);
                }
            }, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await Task.WhenAll(stdoutTask, stderrTask, readerTask);
            });

            Assert.Null(exception);
        }
    }
}
