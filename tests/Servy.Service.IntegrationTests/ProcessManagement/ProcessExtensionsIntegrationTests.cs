using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    /// <summary>
    /// Integration tests for ProcessExtensions.
    /// These tests execute real OS processes and evaluate native Toolhelp32 enumerations.
    /// </summary>
    public class ProcessExtensionsIntegrationTests : IDisposable
    {
        private readonly List<Process> _processesToCleanup = new List<Process>();

        [Fact]
        public void Format_ActiveProcess_ReturnsProcessNameAndId()
        {
            // Arrange
            using (var currentProcess = Process.GetCurrentProcess())
            {

                // Act
                string formatted = currentProcess.Format();

                // Assert
                Assert.Contains(currentProcess.ProcessName, formatted);
                Assert.Contains(currentProcess.Id.ToString(), formatted);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void GetChildren_InvalidPid_ReturnsEmptyList(int invalidPid)
        {
            // Act
            var children = ProcessExtensions.GetChildren(invalidPid, DateTime.Now);

            // Assert
            Assert.Empty(children);
        }

        [Fact]
        public void GetChildren_ValidParent_ReturnsOnlyImmediateChildren()
        {
            // Arrange: Spawn cmd -> timeout
            var root = SpawnProcessTree(1);
            List<Process> children = new List<Process>();

            try
            {
                // FIX: Poll until "timeout" specifically appears in the children list.
                // We use a timeout of 1 (any child) but filter inside our wait loop.
                children = WaitForProcessName(root, "timeout", ProcessExtensions.GetChildren);

                // Assert
                var timeoutProcess = children.FirstOrDefault(p =>
                    p.ProcessName.Equals("timeout", StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(timeoutProcess);

                // Verify the child belongs strictly to the root
                Assert.Equal(root.Id, GetParentPidViaNative(timeoutProcess));
            }
            finally
            {
                // Ensure all captured handles are disposed
                foreach (var child in children) child.Dispose();
            }
        }

        /// <summary>
        /// Polls the extension method until a specific process name appears in the result set.
        /// This prevents the "conhost.exe" race condition.
        /// </summary>
        private List<Process> WaitForProcessName(Process root, string targetName, Func<int, DateTime, List<Process>> fetchMethod)
        {
            int retries = 0;
            while (retries < 20) // Wait up to ~4 seconds
            {
                var results = fetchMethod(root.Id, root.StartTime);

                if (results.Any(p => p.ProcessName.Equals(targetName, StringComparison.OrdinalIgnoreCase)))
                {
                    return results;
                }

                // Dispose handles before the next poll to prevent leak during the loop
                foreach (var r in results) r.Dispose();

                Thread.Sleep(200);
                retries++;
            }

            return fetchMethod(root.Id, root.StartTime);
        }

        [Fact]
        public void GetAllDescendants_ValidParent_ReturnsEntireTree()
        {
            // Arrange: Spawn cmd -> cmd -> timeout (Tree depth of 3, meaning 2 descendants)
            var root = SpawnProcessTree(2);
            List<Process> descendants = new List<Process>();

            try
            {
                // Act
                descendants = WaitForDescendants(root, expectedCount: 2, fetchMethod: ProcessExtensions.GetAllDescendants);

                // Assert
                Assert.True(descendants.Count >= 2, $"Expected at least 2 descendants, found {descendants.Count}");

                bool hasCmdChild = descendants.Any(d => d.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase));
                bool hasTimeoutGrandchild = descendants.Any(d => d.ProcessName.Equals("timeout", StringComparison.OrdinalIgnoreCase));

                Assert.True(hasCmdChild, "Tree did not contain the intermediate 'cmd' child process.");
                Assert.True(hasTimeoutGrandchild, "Tree did not contain the leaf 'timeout' grandchild process.");
            }
            finally
            {
                foreach (var d in descendants) d.Dispose();
            }
        }

        [Fact]
        public void GetChildren_SimulatedPidReuse_ReturnsEmptyList()
        {
            // Arrange
            var root = SpawnProcessTree(1);

            // Act: Simulate PID reuse by passing a future StartTime that the child couldn't possibly satisfy
            var invalidStartTime = DateTime.Now.AddHours(1);
            var children = ProcessExtensions.GetChildren(root.Id, invalidStartTime);

            // Assert
            Assert.Empty(children);
        }

        #region Integration Test Helpers

        /// <summary>
        /// Spawns a nested process tree using cmd.exe and timeout.exe to simulate a complex service workload.
        /// </summary>
        /// <param name="depth">
        /// Depth 1 = cmd.exe -> timeout.exe
        /// Depth 2 = cmd.exe -> cmd.exe -> timeout.exe
        /// </param>
        private Process SpawnProcessTree(int depth)
        {
            // The leaf node that just waits
            string args = "/c timeout /t 10 /nobreak";

            // Wrap the leaf in intermediate cmd.exe shells based on desired depth
            for (int i = 1; i < depth; i++)
            {
                args = $"/c cmd.exe {args}";
            }

            var psi = new ProcessStartInfo("cmd.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var rootProcess = Process.Start(psi);
            Assert.NotNull(rootProcess);

            _processesToCleanup.Add(rootProcess);

            return rootProcess;
        }

        /// <summary>
        /// Native OS process creation is asynchronous. This polls the snapshot extension until the tree fully forms.
        /// </summary>
        private List<Process> WaitForDescendants(Process root, int expectedCount, Func<int, DateTime, List<Process>> fetchMethod)
        {
            int retries = 0;
            List<Process> results;

            while (retries < 15) // Wait up to ~3 seconds
            {
                results = fetchMethod(root.Id, root.StartTime);

                if (results.Count >= expectedCount)
                {
                    return results; // Target tree formed
                }

                // If not formed yet, dispose the partial handles and wait
                foreach (var r in results) r.Dispose();

                Thread.Sleep(200);
                retries++;
            }

            // Fallback: return whatever we managed to find so assertions can fail meaningfully
            return fetchMethod(root.Id, root.StartTime);
        }

        /// <summary>
        /// Grabs the ParentId out of the PerformanceCounters or WMI safely for assertions.
        /// (Uses a quick PerformanceCounter lookup for test assertion independence).
        /// </summary>
        private int GetParentPidViaNative(Process process)
        {
            using (var mo = new System.Management.ManagementObject($"win32_process.handle='{process.Id}'"))
            {
                mo.Get();
                return Convert.ToInt32(mo["ParentProcessId"]);
            }
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            // Ensure we don't leave lingering cmd.exe or timeout.exe processes on the host
            foreach (var process in _processesToCleanup)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        // Kill the entire tree
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore access denied or race condition exits during teardown
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        #endregion
    }
}