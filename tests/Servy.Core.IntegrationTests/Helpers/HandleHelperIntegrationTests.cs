using Servy.Core.Helpers;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Integration tests for <see cref="HandleHelper"/> against the real Windows Restart Manager: files held by this
    /// process, by a child process, and an executable held by the process running it.
    /// </summary>
    [Collection(ProcessIntegrationTestsCollection.Name)]
    public class HandleHelperIntegrationTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();
        private readonly List<Process> _processes = new List<Process>();

        /// <summary>
        /// Kills the child processes and deletes the temporary files the tests created.
        /// </summary>
        public void Dispose()
        {
            foreach (var process in _processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(TestTimeouts.ProcessKillerPerAttemptExitWaitSeconds * 1000);
                    }
                }
                catch { /* Ignore cleanup errors */ }
                finally { process.Dispose(); }
            }

            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        private static int CurrentPid
        {
            get { using (var self = Process.GetCurrentProcess()) return self.Id; }
        }

        private string CreateTempFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid()}.tmp");
            File.WriteAllText(path, "Integration Test Content");
            _tempFiles.Add(path);
            return path;
        }

        /// <summary>
        /// Starts a PowerShell child that opens <paramref name="filePath"/> without sharing and keeps it open, and waits
        /// until it reports that it holds the file.
        /// </summary>
        /// <param name="filePath">The file the child opens.</param>
        /// <returns>The running child process.</returns>
        private Process StartFileHolder(string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = TestProcessStarter.PowerShellPath,
                Arguments = $"-NoProfile -NonInteractive -Command \"$fs = [System.IO.File]::Open('{filePath}', 'Open', 'Read', 'None'); Write-Output 'LOCKED'; while ($true) {{ Start-Sleep -Seconds 1 }}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // A cold powershell.exe on a loaded runner occasionally stalls before its first line (#7128): the shared
            // helper replaces a stalled or exited instance rather than waiting longer, and reports every attempt.
            return TestProcessStarter.StartWithHandshake(
                psi,
                line => line.Contains("LOCKED"),
                TimeSpan.FromSeconds(TestTimeouts.ProcessKillerFileLockTimeoutSeconds),
                _processes.Add).Process;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetProcessesUsingFile_NullOrBlankPath_ThrowsArgumentException(string filePath)
        {
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile(filePath));
        }

        [Fact]
        public void GetProcessesUsingFile_NoProcessHoldsTheFile_DoesNotListThisProcess()
        {
            // Arrange
            string testFile = CreateTempFile();
            int currentPid = CurrentPid;

            // Act
            var results = HandleHelper.GetProcessesUsingFile(testFile);

            // Assert
            // Not Assert.Empty: an antivirus or indexer may open a fresh file for a moment. What must hold is that
            // a file this process wrote and closed is not reported as held by it.
            Assert.DoesNotContain(results, r => r.ProcessId == currentPid);
        }

        [Fact]
        public void GetProcessesUsingFile_FileDoesNotExist_ReturnsEmptyList()
        {
            // Arrange
            string missing = Path.Combine(Path.GetTempPath(), $"ServyMissing_{Guid.NewGuid():N}.tmp");

            // Act
            var results = HandleHelper.GetProcessesUsingFile(missing);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetProcessesUsingFile_ThisProcessHoldsTheFile_ListsThisProcessByExecutableName()
        {
            using (var self = Process.GetCurrentProcess())
            {
                // Arrange
                string testFile = CreateTempFile();

                using (new FileStream(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    // Act
                    var results = HandleHelper.GetProcessesUsingFile(testFile);

                    // Assert
                    Assert.Single(results, r => r.ProcessId == self.Id);
                    var match = results.First(r => r.ProcessId == self.Id);
                    Assert.Equal(self.ProcessName, match.ProcessName, ignoreCase: true);
                }
            }
        }

        [Fact]
        public void GetProcessesUsingFile_TwoHandlesInThisProcess_ListsThisProcessOnce()
        {
            // Arrange
            string testFile = CreateTempFile();
            int currentPid = CurrentPid;

            using (new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // Act
                var results = HandleHelper.GetProcessesUsingFile(testFile);

                // Assert
                // ProcessKiller kills each listed process tree; listing a process once per handle would kill it twice.
                Assert.Single(results, r => r.ProcessId == currentPid);
            }
        }

        [Fact]
        public void GetProcessesUsingFile_HandleClosed_NoLongerListsThisProcess()
        {
            // Arrange
            string testFile = CreateTempFile();
            int currentPid = CurrentPid;

            using (new FileStream(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                Assert.Contains(HandleHelper.GetProcessesUsingFile(testFile), r => r.ProcessId == currentPid);
            }

            // Act
            var results = HandleHelper.GetProcessesUsingFile(testFile);

            // Assert
            Assert.DoesNotContain(results, r => r.ProcessId == currentPid);
        }

        [Fact]
        public void GetProcessesUsingFile_ChildProcessHoldsTheFile_ListsTheChild()
        {
            // Arrange
            string testFile = CreateTempFile();
            var holder = StartFileHolder(testFile);

            // Act
            var results = HandleHelper.GetProcessesUsingFile(testFile);

            // Assert
            Assert.Single(results, r => r.ProcessId == holder.Id);
            var match = results.First(r => r.ProcessId == holder.Id);
            Assert.Equal("powershell", match.ProcessName, ignoreCase: true);
        }

        [Fact]
        public void GetProcessesUsingFile_RunningExecutable_ListsTheProcessRunningIt()
        {
            // Arrange
            // The case Servy depends on: a running Servy.Service.exe holds its own image, which is what blocks the
            // extraction of a new version. powershell.exe stands in for it.
            string testFile = CreateTempFile();
            var holder = StartFileHolder(testFile);

            // Act
            var results = HandleHelper.GetProcessesUsingFile(TestProcessStarter.PowerShellPath);

            // Assert
            Assert.Contains(results, r => r.ProcessId == holder.Id);
        }
    }
}
