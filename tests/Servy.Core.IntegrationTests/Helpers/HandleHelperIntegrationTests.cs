using Microsoft.Win32;
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
    public class HandleHelperIntegrationTestsCollection : ICollectionFixture<object>
    {
        // Enforces strict sequential isolation across the execution suite
    }

    /// <summary>
    /// Integration tests for the HandleHelper class.
    /// These tests require handle.exe to be present and the runner to be elevated.
    /// </summary>
    [Collection("HandleHelperIntegrationTests")]
    public class HandleHelperIntegrationTests : IDisposable
    {
        private readonly string _handleExePath;
        private readonly List<string> _tempFiles = new List<string>();
        private readonly List<FileStream> _openedStreams = new List<FileStream>();

        /// <summary>
        /// Initializes the test class by ensuring handle64.exe is extracted from resources.
        /// </summary>
        public HandleHelperIntegrationTests()
        {
            Testing.Helper.ExtractHandleExe();

            // Safely read path assignment tokens once extraction checks pass
            _handleExePath = Testing.Helper.HandleExePath;

            if (!File.Exists(_handleExePath))
            {
                Debug.WriteLine($"WARNING: handle64.exe not found and extraction failed at {_handleExePath}");
            }
            else
            {
                // Auto-accept Sysinternals EULA in the registry hive context to prevent the headless runner from hanging
                AcceptSysinternalsEula();

                // Guard against Sysinternals kernel-driver load timeouts on cold CI/CD virtual nodes.
                // If the dynamic driver creation takes longer than the strict execution timeout limits,
                // catch the exception, allow conhost to catch up, and attempt a secondary deterministic pass.
                try
                {
                    // A dummy run ensures the driver is extracted and loaded 
                    // before the actual timing-sensitive tests run.
                    HandleHelper.GetProcessesUsingFile(_handleExePath, Path.GetTempPath());
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine("WARNING: Initial handle.exe cold-start timed out while mounting kernel objects. Executing retry pass...");
                    Thread.Sleep(1000);

                    // Second pass: the driver is now unpacked or registered, so this execution path should complete immediately.
                    HandleHelper.GetProcessesUsingFile(_handleExePath, Path.GetTempPath());
                }
            }
        }

        /// <summary>
        /// Programs the current user registry environment to suppress the Sysinternals graphical license box prompt.
        /// </summary>
        private void AcceptSysinternalsEula()
        {
            try
            {
                // Sysinternals tools check for acceptance under HKCU\Software\Sysinternals\Handle
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Sysinternals\Handle"))
                {
                    if (key != null)
                    {
                        key.SetValue("EulaAccepted", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to pre-seed EulaAccepted registry key. Details: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up any open file streams, temporary files, and the extracted executable.
        /// </summary>
        public void Dispose()
        {
            foreach (var stream in _openedStreams)
            {
                stream.Close();
                stream.Dispose();
            }

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

        [Fact]
        public void GetProcessesUsingFile_ShouldThrow_WhenPathsAreNullOrEmpty()
        {
            // Assert
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile("", "C:\\test.txt"));
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile("handle.exe", ""));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldReturnEmptyList_WhenNoProcessHoldsHandle()
        {
            // Arrange
            string testFile = CreateTempFile();

            // Act
            var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldDetectCurrentProcess_WhenCurrentProcessHoldsHandle()
        {
            // Arrange
            string testFile = CreateTempFile();
            int currentPid = Process.GetCurrentProcess().Id;
            string currentName = Process.GetCurrentProcess().ProcessName;

            // Lock the file
            using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                _openedStreams.Add(fs); // Keep track for disposal

                // Act
                var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);

                // Assert
                Assert.NotEmpty(results);
                var selfMatch = results.FirstOrDefault(p => p.ProcessId == currentPid);

                Assert.NotNull(selfMatch);
                // handle.exe output might include .exe or not, HandleHelper trims whitespace.
                Assert.Contains(currentName, selfMatch.ProcessName, StringComparison.OrdinalIgnoreCase);
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
            // Arrange
            string testFile = CreateTempFile();

            // Open multiple streams (this simulates multiple handles from the same or different threads)
            var fs1 = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var fs2 = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _openedStreams.Add(fs1);
            _openedStreams.Add(fs2);

            // Act
            var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);

            // Assert
            // handle.exe returns one line per handle found.
            // FIX: Filter out potential concurrent background system handles (like security indexers) targeting our file
            var currentPid = Process.GetCurrentProcess().Id;
            var selfHandles = results.Where(r => r.ProcessId == currentPid).ToList();

            Assert.True(selfHandles.Count >= 2, $"Should have detected at least two handles owned by this running test process. Total found: {results.Count}");
            Assert.All(selfHandles, r => Assert.Equal(currentPid, r.ProcessId));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldRespectAsyncTimeout()
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