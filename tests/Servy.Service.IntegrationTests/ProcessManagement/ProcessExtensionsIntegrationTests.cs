using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    /// <summary>
    /// Integration tests for ProcessExtensions.
    /// These tests execute real OS processes and evaluate native Toolhelp32 enumerations.
    /// </summary>
    [CollectionDefinition("ProcessExtensionsIntegrationTests", DisableParallelization = true)]
    public class ProcessExtensionsIntegrationTestsCollection : ICollectionFixture<object>
    {
        // Enforces strict sequential isolation across the execution suite
    }

    [Collection("ProcessExtensionsIntegrationTests")]
    public class ProcessExtensionsIntegrationTests : IDisposable
    {
        private readonly List<Process> _processesToCleanup = new List<Process>();

        #region Format Tests

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

        [Fact]
        public void Format_Win32ExceptionThrownOnAccess_CatchesAndReturnsFallbackString()
        {
            // Arrange - Use a mock of a non-sealed component pattern or an uninitialized Process model
            var process = new Process();

            // Forcing an operations execution onto an unallocated Process object native handle
            // throws an internal Win32Exception or InvalidOperationException depending on runtime state.
            string formatted = process.Format();

            // Assert
            Assert.NotNull(formatted);
            Assert.True(formatted.Contains("PID") || formatted.Contains("Process"));
        }

        #endregion

        #region GetChildren & GetAllDescendants Boundary Tests

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void GetChildren_InvalidPid_ReturnsEmptyList(int invalidPid)
        {
            var children = ProcessExtensions.GetChildren(invalidPid, DateTime.Now);
            Assert.Empty(children);
        }

        [Fact]
        public void GetChildren_InvalidStartTime_ReturnsEmptyList()
        {
            var children = ProcessExtensions.GetChildren(123, DateTime.MinValue);
            Assert.Empty(children);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void GetAllDescendants_InvalidParameters_ReturnsEmptyList(int invalidPid)
        {
            var descendantsNullTime = ProcessExtensions.GetAllDescendants(invalidPid, DateTime.MinValue);
            var descendantsValidTime = ProcessExtensions.GetAllDescendants(invalidPid, DateTime.Now);

            Assert.Empty(descendantsNullTime);
            Assert.Empty(descendantsValidTime);
        }

        [Fact]
        public void GetAllDescendants_InvalidStartTime_ReturnsEmptyList()
        {
            var descendants = ProcessExtensions.GetAllDescendants(1, DateTime.MinValue);
            Assert.Empty(descendants);
        }

        [Fact]
        public void GetChildren_ValidParent_ReturnsOnlyImmediateChildren()
        {
            var root = SpawnProcessTree(1);
            List<Process> children = new List<Process>();

            try
            {
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
            var root = SpawnProcessTree(2);
            List<Process> descendants = new List<Process>();

            try
            {
                bool treeStabilized = SpinWait.SpinUntil(() =>
                {
                    if (root.HasExited) return true;

                    foreach (var d in descendants) d.Dispose();
                    descendants = ProcessExtensions.GetAllDescendants(root.Id, root.StartTime);

                    int psCount = descendants.Count(d => d.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase));
                    return descendants.Count >= 2 && psCount >= 2;

                }, TimeSpan.FromSeconds(20));

                if (root.HasExited)
                {
                    Assert.Fail($"Root process exited prematurely (ExitCode: {root.ExitCode}). Found: {string.Join(", ", descendants.Select(d => d.ProcessName))}");
                }

                Assert.True(treeStabilized, $"Tree failed to stabilize within 20s. Found {descendants.Count} descendants.");

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

        #endregion

        #region TryResolveValidChild Private Method Reflection Tests

        [Fact]
        public void TryResolveValidChild_ArgumentExceptionThrown_ReturnsNullSafely()
        {
            // Arrange - Use an absolute out-of-bounds PID that can never belong to an active windows process allocation
            int nonExistentPid = 999999;

            var method = typeof(ProcessExtensions).GetMethod("TryResolveValidChild", BindingFlags.Static | BindingFlags.NonPublic);

            // Act - Invoke the verification flow; ArgumentException gets caught when Process.GetProcessById falls out
            var result = method.Invoke(null, new object[] { nonExistentPid, DateTime.Now, DateTime.UtcNow });

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TryResolveValidChild_ProcessFailsLifetimeValidationBounds_ReturnsNullAndDisposes()
        {
            // Arrange - Use a guaranteed active process (current process)
            using (var current = Process.GetCurrentProcess())
            {
                var method = typeof(ProcessExtensions).GetMethod("TryResolveValidChild", BindingFlags.Static | BindingFlags.NonPublic);

                // Intentionally alter constraints layout: Pass an execution threshold time set completely in the future
                // to force 'startedAfterParent' or 'startedBeforeSnapshot' conditional validations to return false.
                var skewedParentTime = DateTime.Now.AddDays(10);
                var snapshotTime = DateTime.UtcNow.AddDays(-10);

                // Act
                var result = method.Invoke(null, new object[] { current.Id, skewedParentTime, snapshotTime });

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void TryResolveValidChild_UnexpectedGenericException_LogsTraceAndReturnsNull()
        {
            // Arrange - Passing a negative PID into a raw system process mapping pipeline 
            // forces an implicit runtime validation layer ArgumentException or Win32Exception layout.
            var method = typeof(ProcessExtensions).GetMethod("TryResolveValidChild", BindingFlags.Static | BindingFlags.NonPublic);

            // Act
            var result = method.Invoke(null, new object[] { -99, DateTime.Now, DateTime.UtcNow });

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Integration Test Helpers

        /// <summary>
        /// Orchestrates a guaranteed strict PPID native tree using PowerShell.
        /// Uses Base64 EncodedCommands to allow infinite nesting depth without quote-escaping bugs.
        /// </summary>
        private Process SpawnProcessTree(int depth)
        {
            string BuildScript(int currentDepth)
            {
                if (currentDepth == 0)
                    return "Start-Sleep -Seconds 15";

                string innerScript = BuildScript(currentDepth - 1);
                string encodedInner = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(innerScript));

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
                    ProcessHelper.KillProcessTree(root);
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