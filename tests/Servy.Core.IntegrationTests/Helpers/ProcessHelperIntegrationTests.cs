using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Diagnostics;

namespace Servy.Core.IntegrationTests.Helpers
{
    [CollectionDefinition("ProcessHelperIntegrationTests", DisableParallelization = true)]
    public class ProcessHelperIntegrationTestsCollection : ICollectionFixture<object>
    {
        // Enforces strict sequential isolation across the execution suite
    }

    /// <summary>
    /// Integration tests for ProcessHelper. 
    /// Verifies OS-level interactions including file system resolution, environment variable expansion, 
    /// and native Windows process tree traversal.
    /// </summary>
    [Collection("ProcessHelperIntegrationTests")]
    public class ProcessHelperIntegrationTests : IDisposable
    {
        private readonly ProcessHelper _sut;
        private readonly string _tempDirectory;
        private readonly string _tempFile;
        private readonly List<Process> _spawnedProcesses;

        public ProcessHelperIntegrationTests()
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
            // Arrange: Use a longer-lived PowerShell command to ensure stability
            var childProcessInfo = new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"$data = New-Object byte[] 30MB; Start-Sleep -Seconds 5\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            };

            var childProcess = Process.Start(childProcessInfo);
            Assert.NotNull(childProcess);
            _spawnedProcesses.Add(childProcess);

            // 1. INCREASE STABILIZATION: Allow the PowerShell host engine to fully 
            // spin up and map its byte allocation inside the OS kernel.
            Thread.Sleep(1500);

            int childPid = childProcess.Id;

            // 2. CAPTURE SINGLE METRICS FIRST:
            var singleMetrics = _sut.GetProcessMetrics(childPid);

            // Act: Capture the tree aggregation metrics back-to-back
            var treeMetrics = _sut.GetProcessTreeMetrics(childPid);

            // 3. LOGGING FOR DEBUGGING:
            Logger.Info($"Single RAM: {singleMetrics.RamUsage}, Tree RAM: {treeMetrics.RamUsage}");

            // Assert
            Assert.True(singleMetrics.RamUsage > 0, "Root process RAM should be captured.");
            Assert.True(treeMetrics.RamUsage > 0, "Tree process RAM aggregation should be captured.");

            // 4. DELTA VALUATION: Ensure the two back-to-back reads do not drift 
            // beyond a standard runtime memory variance threshold (e.g., 15 MB).
            long memoryDeltaBytes = Math.Abs(treeMetrics.RamUsage - singleMetrics.RamUsage);
            long maxAllowedVarianceBytes = 15 * 1024 * 1024; // 15 Megabytes

            Assert.True(memoryDeltaBytes <= maxAllowedVarianceBytes,
                $"The memory drift between sequential reads ({memoryDeltaBytes} bytes) exceeded the maximum allowed runtime variance threshold ({maxAllowedVarianceBytes} bytes). Single RAM: {singleMetrics.RamUsage}, Tree RAM: {treeMetrics.RamUsage}");
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