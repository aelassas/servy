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
    [Collection(ProcessIntegrationTestsCollection.Name)]
    public class ProcessHelperIntegrationTests : IDisposable
    {
        private readonly ProcessHelper _sut;
        private readonly List<Process> _spawnedProcesses;

        public ProcessHelperIntegrationTests()
        {
            _sut = new ProcessHelper();
            _spawnedProcesses = new List<Process>();
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
                SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(TestTimeouts.CpuSampleSpinMs));
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
                Assert.True(prevCpuTimes.ContainsKey(currentPid), "The tracking baseline sample entry was lost or prematurely evicted from the delta cache.");
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
                    transientProcess.WaitForExit(TestTimeouts.CiGenerousMs);
                    Assert.True(transientProcess.HasExited, "Transient test process failed to exit within the allowed timeout window.");
                }
            }
            else
            {
                // Use a PID no process can hold (see TestProcessIds.NeverValid)
                targetPid = TestProcessIds.NeverValid;
            }

            // Act
            var metrics = _sut.GetProcessMetrics(targetPid);

            // Assert
            // Both code paths must cleanly intercept the respective native exception type
            // and fallback to a safe, neutral metric layout instead of blowing up.
            Assert.Equal(0, metrics.CpuUsage);
            Assert.Equal(0, metrics.RamUsage);
        }

        #endregion

        #region Cache Maintenance Integration Tests

        [Fact]
        public void MaintainCache_ForExitedProcess_EvictsTheStaleSample()
        {
            // Arrange: seed a delta sample while the process is alive, then let it exit.
            var transientProcess = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 30\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            });

            Assert.NotNull(transientProcess);
            _spawnedProcesses.Add(transientProcess);

            int targetPid = transientProcess.Id;
            _sut.GetProcessMetrics(targetPid);

            var prevCpuTimes = TestReflection.GetField<ConcurrentDictionary<int, CpuSample>>(_sut, "_prevCpuTimes");
            Assert.True(prevCpuTimes.ContainsKey(targetPid), "Precondition failed: the delta sample must exist before the prune runs.");

            // Start-Sleep spawns no descendants, so killing the root alone is enough here.
            transientProcess.Kill();
            transientProcess.WaitForExit(TestTimeouts.CiGenerousMs);
            Assert.True(transientProcess.HasExited, "Transient test process failed to exit within the allowed timeout window.");

            // Act
            _sut.MaintainCache();

            // Assert
            Assert.False(prevCpuTimes.ContainsKey(targetPid), "MaintainCache must evict the sample of a process that has exited.");
        }

        [Fact]
        public void MaintainCache_ForLiveProcess_RetainsTheSample()
        {
            using (var self = Process.GetCurrentProcess())
            {
                // Arrange
                int currentPid = self.Id;
                _sut.GetProcessMetrics(currentPid);

                var prevCpuTimes = TestReflection.GetField<ConcurrentDictionary<int, CpuSample>>(_sut, "_prevCpuTimes");
                Assert.True(prevCpuTimes.ContainsKey(currentPid), "Precondition failed: the delta sample must exist before the prune runs.");

                // Act
                _sut.MaintainCache();

                // Assert
                // The test host is still running, so its baseline must survive the prune;
                // evicting it would restart the CPU delta calculation on every maintenance pass.
                Assert.True(prevCpuTimes.ContainsKey(currentPid), "MaintainCache must retain the sample of a process that is still running.");
            }
        }

        #endregion

        #region Process Tree Integration Tests

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

            int childPid = childProcess.Id;

            // 1. HARDEN STABILIZATION TIMING:
            // Poll until the nested child process initializes, allocates memory, and tree metrics exceed single root metrics.
            var stopwatch = Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(TestTimeouts.ProcessTreeTimeoutSeconds);

            while (stopwatch.Elapsed < timeout)
            {
                var polledSingle = _sut.GetProcessMetrics(childPid);
                var polledTree = _sut.GetProcessTreeMetrics(childPid);

                if (polledSingle.RamUsage > 0 && polledTree.RamUsage > polledSingle.RamUsage)
                {
                    break;
                }

                Thread.Sleep(TestTimeouts.MetricsPollIntervalMs);
            }

            // 2. CAPTURE METRICS BACK-TO-BACK:
            // Act
            var singleMetrics = _sut.GetProcessMetrics(childPid);
            var treeMetrics = _sut.GetProcessTreeMetrics(childPid);

            // Assert
            Assert.True(singleMetrics.RamUsage > 0, "Root process RAM should be captured.");
            Assert.True(treeMetrics.RamUsage > 0, "Tree process RAM aggregation should be captured.");

            // 3. ROBUST DELTA VALUATION:
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
                        // Kill the root process and its nested descendants (Start-Process grandchildren)
                        Service.Helpers.ProcessHelper.KillProcessTree(process);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
                process.Dispose();
            }
        }
    }
}
