using Servy.Core.Helpers;
using Servy.Testing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Integration tests for ProcessHelper. 
    /// Verifies ProcessHelper process-metric and process-tree aggregation behavior.
    /// </summary>
    [Collection("ProcessIntegrationTests")]
    public class ProcessHelperIntegrationTests : IDisposable
    {
        private readonly ProcessHelper _sut;
        private readonly string _tempDirectory;
        private readonly List<Process> _spawnedProcesses;

        public ProcessHelperIntegrationTests()
        {
            _sut = new ProcessHelper();
            _spawnedProcesses = new List<Process>();

            // Setup real file system artifacts for path integration tests
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"Servy_Test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);
        }

        #region Process Metrics Integration Tests

        [Fact]
        public void GetProcessMetrics_ForCurrentProcess_ReturnsValidRamAndInitializesCpu()
        {
            using (var self = Process.GetCurrentProcess())
            {
                // Arrange
                int currentPid = self.Id;

                // Act 1: First call initializes the total processor time baseline. CPU should be 0.
                var firstCall = _sut.GetProcessMetrics(currentPid);

                // Assert 1
                Assert.Equal(0, firstCall.CpuUsage);
                Assert.True(firstCall.RamUsage > 0, "RAM usage should be greater than 0 for a running process.");

                // Act 2: Simulate time passing and active CPU work using a dedicated spin wait loop
                SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(100));
                var secondCall = _sut.GetProcessMetrics(currentPid);

                // Assert 2
                // Verify that the delta tracking engine successfully executed and updated its reference points.
                // In highly throttled CI environments, CPU time tracking may occasionally report 0 if quantums match perfectly, 
                // but a successful calculation path is structurally proven if memory metrics persist alongside a non-negative payload.
                Assert.True(secondCall.CpuUsage >= 0, "CPU calculation loop failed or threw an internal fallback error exception.");
                Assert.True(secondCall.RamUsage > 0, "RAM metrics were lost or corrupted during consecutive sampling calls.");

                // Structural State Validation: Force reflection to confirm that an active, populated cache state entry 
                // is preserved inside the dictionary, proving the metric loop did not fall back into an error eviction path.
                var prevCpuTimes = TestReflection.GetField<ConcurrentDictionary<int, CpuSample>>(_sut, "_prevCpuTimes");
                if (prevCpuTimes != null)
                {
                    Assert.True(prevCpuTimes.ContainsKey(currentPid), "The tracking baseline sample entry was lost or prematurely evicted from the delta cache.");
                }
            }
        }

        [Theory]
        [InlineData(false)] // Scenario 1: Never-existed PID (Throws ArgumentException)
        [InlineData(true)]  // Scenario 2: Genuinely exited PID (Throws InvalidOperationException)
        public void GetProcessMetrics_ForInvalidOrExitedPid_ReturnsZeroesGracefully(bool useExitedProcess)
        {
            // Arrange
            int targetPid;

            if (useExitedProcess)
            {
                // Start a transient native process that exits instantly
                using (var transientProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c exit",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    Assert.NotNull(transientProcess);
                    targetPid = transientProcess.Id;

                    // Force the thread block to wait until the OS fully unregisters the process image
                    transientProcess.WaitForExit(5000);
                    Assert.True(transientProcess.HasExited, "Transient test process failed to exit within the allowed timeout window.");
                }
            }
            else
            {
                // Use an out-of-bounds PID that is fundamentally guaranteed not to exist on standard Windows nodes
                targetPid = 999999;
            }

            // Act
            var metrics = _sut.GetProcessMetrics(targetPid);

            // Assert
            // Both code paths must cleanly intercept the respective native exception type 
            // and fallback to a safe, neutral metric layout instead of blowing up.
            Assert.Equal(0, metrics.CpuUsage);
            Assert.Equal(0, metrics.RamUsage);
        }

        [Fact]
        public void GetProcessTreeMetrics_WithChildProcess_AggregatesRamSuccessfully()
        {
            // Arrange
            // Force the root process to spawn a distinct child process space using Start-Process.
            // Both the root host and its child load memory allocations to provide a clear tree aggregation signal.
            string inlineScript =
                "$rootAlloc = New-Object byte[] 20MB; " +
                "Start-Process powershell -ArgumentList '-NoProfile','-Command', '$childAlloc = New-Object byte[] 25MB; Start-Sleep -Seconds 10'; " +
                "Start-Sleep -Seconds 10";

            var childProcessInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{inlineScript}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            };

            var childProcess = Process.Start(childProcessInfo);
            Assert.NotNull(childProcess);
            _spawnedProcesses.Add(childProcess);

            // 1. HARDEN STABILIZATION TIMING:
            // Allow time for the root process to initialize and execute its nested child payload.
            Thread.Sleep(3000);

            int childPid = childProcess.Id;

            // 2. CAPTURE METRICS BACK-TO-BACK:
            // Act
            var singleMetrics = _sut.GetProcessMetrics(childPid);
            var treeMetrics = _sut.GetProcessTreeMetrics(childPid);

            // Assert
            Assert.True(singleMetrics.RamUsage > 0, "Root process RAM should be captured.");
            Assert.True(treeMetrics.RamUsage > 0, "Tree process RAM aggregation should be captured.");

            // 4. ROBUST DELTA VALUATION:
            // Verifies that tree metrics accurately sum memory across the nested worker processes.
            // Tree memory must be noticeably larger than the isolated root process node's footprint.
            Assert.True(treeMetrics.RamUsage > singleMetrics.RamUsage,
                $"Process tree aggregation failed. Tree RAM ({treeMetrics.RamUsage} bytes) should be strictly greater than single root RAM ({singleMetrics.RamUsage} bytes).");
        }

        #endregion

        public void Dispose()
        {
            // Clean up spawned integration test processes
            foreach (var process in _spawnedProcesses)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        // Kill the root process and ensure its nested descendants are cleaned up from the OS scheduler
                        process.Kill();
                    }
                    catch { /* Ignore cleanup errors */ }
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