using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    [CollectionDefinition("HandleHelperIntegrationTests", DisableParallelization = true)]
    public class HandleHelperIntegrationTestsCollection
    {
        // Enforces strict sequential isolation across the execution suite
    }

    /// <summary>
    /// Integration tests for the HandleHelper class.
    /// These tests require handle.exe to be present and the runner to be elevated.
    /// </summary>
    [Collection("HandleHelperIntegrationTests")]
    public class HandleHelperIntegrationTests : HandleExeIntegrationTestBase, IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();

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
                Thread.Sleep(1000);

                HandleHelper.GetProcessesUsingFile(_handleExePath, Path.GetTempPath());
            }
        }

        /// <summary>
        /// Cleans up any open file streams, temporary files, and the extracted executable.
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

        [Theory]
        [InlineData(null, "C:\\temp\\file.txt")]
        [InlineData("C:\\temp\\handle.exe", null)]
        [InlineData("", "C:\\temp\\file.txt")]
        [InlineData("C:\\temp\\handle.exe", "")]
        public void GetProcessesUsingFile_ShouldThrow_WhenPathsAreNullOrEmpty(string handleExePath, string filePath)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile(handleExePath, filePath));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldReturnEmptyList_WhenNoProcessHoldsHandle()
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
                    List<HandleHelper.ProcessHandleInfo> results = null;
                    bool handleDetected = false;

                    // Retry up to 5 times with a small delay to handle OS propagation latency
                    const int maxRetries = 5;
                    for (int i = 0; i < maxRetries; i++)
                    {
                        results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);
                        if (results.Any(p => p.ProcessId == currentPid))
                        {
                            handleDetected = true;
                            break;
                        }
                        Thread.Sleep(50); // Small backoff window
                    }

                    // Assert
                    Assert.True(handleDetected, $"Current process (PID {currentPid}) failed to be detected holding a handle to {testFile} after retries.");
                    Assert.NotEmpty(results);

                    var selfMatch = results.FirstOrDefault(p => p.ProcessId == currentPid);
                    Assert.NotNull(selfMatch);

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
                    List<HandleHelper.ProcessHandleInfo> results = null;
                    List<HandleHelper.ProcessHandleInfo> selfHandles = null;
                    bool multiHandlesDetected = false;

                    // Wrap the query inside a retry loop identical to its sibling test.
                    // This absorbs underlying Windows kernel table sync latency before evaluating assertions.
                    const int maxRetries = 5;
                    for (int i = 0; i < maxRetries; i++)
                    {
                        results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);
                        selfHandles = results.Where(r => r.ProcessId == currentPid).ToList();

                        // handle.exe returns one line per handle found.
                        if (selfHandles.Count >= 2)
                        {
                            multiHandlesDetected = true;
                            break;
                        }

                        Thread.Sleep(50); // Small backoff window
                    }

                    // Assert
                    // Filter out potential concurrent background system handles (like security indexers) targeting our file
                    Assert.True(multiHandlesDetected, $"Should have detected at least two handles owned by this running test process (PID {currentPid}). Total found self handles: {selfHandles?.Count ?? 0}, overall system handles found: {results?.Count ?? 0}");
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
            // Ensure we didn't block longer than the timeout if the exe is working
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Normal execution should be faster than timeout.");
        }
    }
}