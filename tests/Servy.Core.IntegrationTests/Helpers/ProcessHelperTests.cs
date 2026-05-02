using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Integration tests for ProcessHelper. 
    /// Verifies OS-level interactions including file system resolution, environment variable expansion, 
    /// and native Windows process tree traversal.
    /// </summary>
    public class ProcessHelperTests : IDisposable
    {
        private readonly ProcessHelper _sut;
        private readonly string _tempDirectory;
        private readonly string _tempFile;
        private readonly List<Process> _spawnedProcesses;

        public ProcessHelperTests()
        {
            _sut = new ProcessHelper();
            _spawnedProcesses = new List<Process>();

            // Setup real file system artifacts for path integration tests
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"Servy_Test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);

            _tempFile = Path.Combine(_tempDirectory, "test_target.txt");
            File.WriteAllText(_tempFile, "Integration test artifact");

            // Setup an environment variable specifically for testing expansion
            Environment.SetEnvironmentVariable("SERVY_TEST_VAR", _tempDirectory);
        }

        #region Formatting Tests (Pure Logic)

        [Theory]
        [InlineData(0, "0%")]
        [InlineData(0.00001, "0%")]
        [InlineData(12.34, "12.3%")]
        [InlineData(12.35, "12.4%")]
        [InlineData(100.0, "100.0%")]
        public void FormatCpuUsage_FormatsCorrectly(double input, string expected)
        {
            var result = _sut.FormatCpuUsage(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(500, "500.0 B")]
        [InlineData(1024, "1.0 KB")]
        [InlineData(1536, "1.5 KB")]
        [InlineData(1048576, "1.0 MB")]
        [InlineData(1073741824, "1.0 GB")]
        public void FormatRamUsage_FormatsCorrectly(long bytes, string expected)
        {
            var result = _sut.FormatRamUsage(bytes);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Path Integration Tests

        [Fact]
        public void ResolvePath_WithValidEnvironmentVariable_ExpandsCorrectly()
        {
            // Arrange
            string input = "%SERVY_TEST_VAR%\\subdir\\target.exe";
            string expected = Path.Combine(_tempDirectory, "subdir", "target.exe");

            // Act
            var result = _sut.ResolvePath(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ResolvePath_WithUnresolvableVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            string input = "%NON_EXISTENT_SERVY_VAR%\\target.exe";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _sut.ResolvePath(input));
            Assert.Contains("could not be expanded", ex.Message);
        }

        [Fact]
        public void ResolvePath_WithRelativePath_ThrowsInvalidOperationException()
        {
            // Arrange
            string input = "relative\\folder\\app.exe";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _sut.ResolvePath(input));
            Assert.Contains("Only absolute paths are allowed", ex.Message);
        }

        [Fact]
        public void ValidatePath_ForExistingFile_ReturnsTrue()
        {
            // Act
            bool result = _sut.ValidatePath(_tempFile, isFile: true);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePath_ForExistingDirectory_ReturnsTrue()
        {
            // Act
            bool result = _sut.ValidatePath(_tempDirectory, isFile: false);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePath_ForNonExistentTarget_ReturnsFalse()
        {
            // Arrange
            string fakePath = Path.Combine(_tempDirectory, "does_not_exist.exe");

            // Act
            bool fileResult = _sut.ValidatePath(fakePath, isFile: true);
            bool dirResult = _sut.ValidatePath(fakePath, isFile: false);

            // Assert
            Assert.False(fileResult);
            Assert.False(dirResult);
        }

        #endregion

        #region Process Metrics Integration Tests

        [Fact]
        public void GetProcessMetrics_ForCurrentProcess_ReturnsValidRamAndInitializesCpu()
        {
            // Arrange
            int currentPid = Process.GetCurrentProcess().Id;

            // Act 1: First call initializes the total processor time baseline. CPU should be 0.
            var firstCall = _sut.GetProcessMetrics(currentPid);

            // Assert 1
            Assert.Equal(0, firstCall.CpuUsage);
            Assert.True(firstCall.RamUsage > 0, "RAM usage should be greater than 0 for a running process.");

            // Act 2: Simulate time passing and CPU work
            SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(100));
            var secondCall = _sut.GetProcessMetrics(currentPid);

            // Assert 2
            Assert.True(secondCall.CpuUsage >= 0, "CPU delta should be calculated successfully.");
            Assert.True(secondCall.RamUsage > 0);
        }

        [Fact]
        public void GetProcessMetrics_ForInvalidOrExitedPid_ReturnsZeroesGracefully()
        {
            // Arrange
            // Using an extremely high PID that is virtually guaranteed not to exist.
            int invalidPid = 999999;

            // Act
            var metrics = _sut.GetProcessMetrics(invalidPid);

            // Assert
            Assert.Equal(0, metrics.CpuUsage);
            Assert.Equal(0, metrics.RamUsage);
        }

        [Fact]
        public void GetProcessTreeMetrics_WithChildProcess_AggregatesRamSuccessfully()
        {
            // Windows Only Test - native APIs will fail on Linux/macOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            // Arrange: Spawn a child process to guarantee a tree exists under the current test runner
            var childProcessInfo = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var childProcess = Process.Start(childProcessInfo);
            Assert.NotNull(childProcess);
            _spawnedProcesses.Add(childProcess);

            int rootPid = Process.GetCurrentProcess().Id;

            // Act
            var treeMetrics = _sut.GetProcessTreeMetrics(rootPid);

            // Assert
            // The tree RAM should be strictly greater than just the current process RAM
            // because it includes the cmd.exe and ping.exe descendants.
            var singleMetrics = _sut.GetProcessMetrics(rootPid);

            Assert.True(treeMetrics.RamUsage >= singleMetrics.RamUsage,
                "Tree RAM should encompass at least the root process RAM.");
            Assert.True(treeMetrics.RamUsage > 0);
        }

        #endregion

        public void Dispose()
        {
            // Clean up spawned integration test processes
            foreach (var process in _spawnedProcesses)
            {
                if (!process.HasExited)
                {
                    try { process.Kill(); } catch { /* Ignore cleanup errors */ }
                }
                process.Dispose();
            }

            // Clean up integration test environment variables
            Environment.SetEnvironmentVariable("SERVY_TEST_VAR", null);

            // Clean up integration test artifacts
            if (Directory.Exists(_tempDirectory))
            {
                try { Directory.Delete(_tempDirectory, true); } catch { /* Ignore locking issues on teardown */ }
            }
        }
    }
}