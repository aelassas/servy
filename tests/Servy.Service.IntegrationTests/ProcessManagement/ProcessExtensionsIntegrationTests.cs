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
    [CollectionDefinition("ProcessExtensionsIntegrationTests", DisableParallelization = true)]
    public class ProcessExtensionsIntegrationTests : IDisposable
    {
        private readonly List<Process> _processesToCleanup = new List<Process>();

        [Fact]
        public void Format_ActiveProcess_ReturnsProcessNameAndId()
        {
            using (var currentProcess = Process.GetCurrentProcess())
            {
                string formatted = currentProcess.Format();
                Assert.Contains(currentProcess.ProcessName, formatted);
                Assert.Contains(currentProcess.Id.ToString(), formatted);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void GetChildren_InvalidPid_ReturnsEmptyList(int invalidPid)
        {
            var children = ProcessExtensions.GetChildren(invalidPid, DateTime.Now);
            Assert.Empty(children);
        }

        [Fact]
        public void GetChildren_ValidParent_ReturnsOnlyImmediateChildren()
        {
            // Arrange: Spawn powershell -> powershell
            var root = SpawnProcessTree(1);
            List<Process> children = new List<Process>();

            try
            {
                // Poll for "powershell"
                children = WaitForProcessName(root, "powershell", ProcessExtensions.GetChildren);

                var targetProcess = children.FirstOrDefault(p =>
                    p.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(targetProcess);
                Assert.Equal(root.Id, GetParentPidViaNative(targetProcess));
            }
            finally
            {
                foreach (var child in children) child.Dispose();
            }
        }

        [Fact]
        public void GetAllDescendants_ValidParent_ReturnsEntireTree()
        {
            // Arrange: Spawn powershell -> powershell -> powershell (Depth 2 = 2 nested descendants)
            var root = SpawnProcessTree(2);
            List<Process> descendants = new List<Process>();

            try
            {
                bool treeStabilized = SpinWait.SpinUntil(() =>
                {
                    if (root.HasExited) return true;

                    foreach (var d in descendants) d.Dispose();
                    descendants = ProcessExtensions.GetAllDescendants(root.Id, root.StartTime);

                    // At depth 2, we expect exactly 2 nested powershell instances
                    int psCount = descendants.Count(d => d.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase));
                    return descendants.Count >= 2 && psCount >= 2;

                }, TimeSpan.FromSeconds(20));

                if (root.HasExited)
                {
                    Assert.Fail($"Root process exited prematurely (ExitCode: {root.ExitCode}). Found: {string.Join(", ", descendants.Select(d => d.ProcessName))}");
                }

                Assert.True(treeStabilized, $"Tree failed to stabilize within 20s. Found {descendants.Count} descendants.");

                // Assert both the intermediate and the leaf are present
                int finalPsCount = descendants.Count(d => d.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase));
                Assert.True(finalPsCount >= 2, $"Expected at least 2 nested powershell processes. Found: {finalPsCount}");
            }
            finally
            {
                foreach (var d in descendants) d.Dispose();
                CleanupRoot(root);
            }
        }

        [Fact]
        public void GetChildren_SimulatedPidReuse_ReturnsEmptyList()
        {
            var root = SpawnProcessTree(1);
            var invalidStartTime = DateTime.Now.AddHours(1);
            var children = ProcessExtensions.GetChildren(root.Id, invalidStartTime);
            Assert.Empty(children);
        }

        #region Integration Test Helpers

        /// <summary>
        /// Orchestrates a guaranteed strict PPID native tree using PowerShell.
        /// Uses Base64 EncodedCommands to allow infinite nesting depth without quote-escaping bugs.
        /// </summary>
        private Process SpawnProcessTree(int depth)
        {
            // Recursive function to build nested PowerShell payloads
            string BuildScript(int currentDepth)
            {
                // The absolute leaf node just keeps the process tree alive
                if (currentDepth == 0)
                    return "Start-Sleep -Seconds 15";

                // Get the script for the level below us
                string innerScript = BuildScript(currentDepth - 1);

                // PowerShell requires Unicode (UTF-16LE) for EncodedCommand
                string encodedInner = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(innerScript));

                // Create a script that launches the encoded inner script and waits for it
                return $@"
                    $psi = New-Object System.Diagnostics.ProcessStartInfo
                    $psi.FileName = 'powershell.exe'
                    $psi.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedInner}'
                    $psi.UseShellExecute = $false
                    $psi.CreateNoWindow = $true
                    $p = [System.Diagnostics.Process]::Start($psi)
                    $p.WaitForExit()
                ";
            }

            // Build the script for the requested depth and encode it for the root process
            string rootScript = BuildScript(depth);
            string encodedRoot = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(rootScript));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedRoot}",
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            var rootProcess = Process.Start(psi);
            Assert.NotNull(rootProcess);

            _processesToCleanup.Add(rootProcess);

            return rootProcess;
        }

        private List<Process> WaitForProcessName(Process root, string targetName, Func<int, DateTime, List<Process>> fetchMethod)
        {
            List<Process> lastResults = new List<Process>();

            bool found = SpinWait.SpinUntil(() =>
            {
                if (root.HasExited) return true;

                foreach (var r in lastResults) r.Dispose();
                lastResults = fetchMethod(root.Id, root.StartTime);

                return lastResults.Any(p => p.ProcessName.Equals(targetName, StringComparison.OrdinalIgnoreCase));

            }, TimeSpan.FromSeconds(15));

            if (root.HasExited)
            {
                throw new InvalidOperationException($"The root parent process exited prematurely (ExitCode: {root.ExitCode}). The process tree collapsed.");
            }

            if (!found)
            {
                string foundNames = string.Join(", ", lastResults.Select(p => p.ProcessName));
                throw new TimeoutException($"Failed to find '{targetName}' child process within 15 seconds. Found instead: [{foundNames}]");
            }

            return lastResults;
        }

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

        private void CleanupRoot(Process root)
        {
            try
            {
                if (root != null && !root.HasExited)
                    Helpers.ProcessHelper.KillProcessTree(root);
            }
            catch { }
            root?.Dispose();
        }

        public void Dispose()
        {
            foreach (var process in _processesToCleanup)
            {
                CleanupRoot(process);
            }
        }

        #endregion
    }
}