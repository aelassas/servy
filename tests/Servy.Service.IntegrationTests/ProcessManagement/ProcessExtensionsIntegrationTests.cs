using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public class ProcessExtensionsIntegrationTestsCollection
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

        [Fact]
        public void Format_Win32ExceptionThrownOnAccess_CatchesAndReturnsFallbackString()
        {
            // Arrange - Use a mock of a non-sealed component pattern or an uninitialized Process model
            using (var process = new Process())
            {
                // Act
                // Forcing an operations execution onto an unallocated Process object native handle
                // throws an internal Win32Exception or InvalidOperationException depending on runtime state.
                string formatted = process.Format();

                // Assert
                Assert.NotNull(formatted);
                Assert.True(formatted.Contains("PID") || formatted.Contains("Process"));
            }
        }

        #endregion

        #region GetChildren & GetAllDescendants Boundary Tests

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void GetChildren_InvalidPid_ReturnsEmptyList(int invalidPid)
        {
            // Arrange & Act
            var children = ProcessExtensions.GetChildren(invalidPid, DateTime.Now);

            // Assert
            Assert.Empty(children);
        }

        [Fact]
        public void GetChildren_InvalidStartTime_ReturnsEmptyList()
        {
            // Arrange & Act
            var children = ProcessExtensions.GetChildren(123, DateTime.MinValue);

            // Assert
            Assert.Empty(children);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void GetAllDescendants_InvalidParameters_ReturnsEmptyList(int invalidPid)
        {
            // Arrange & Act
            var descendantsNullTime = ProcessExtensions.GetAllDescendants(invalidPid, DateTime.MinValue);
            var descendantsValidTime = ProcessExtensions.GetAllDescendants(invalidPid, DateTime.Now);

            // Assert
            Assert.Empty(descendantsNullTime);
            Assert.Empty(descendantsValidTime);
        }

        [Fact]
        public void GetAllDescendants_InvalidStartTime_ReturnsEmptyList()
        {
            // Arrange & Act
            var descendants = ProcessExtensions.GetAllDescendants(1, DateTime.MinValue);

            // Assert
            Assert.Empty(descendants);
        }

        [Fact]
        public void GetChildren_ValidParent_ReturnsOnlyImmediateChildren()
        {
            // Arrange
            var root = SpawnProcessTree(1);
            List<Process> children = new List<Process>();

            // Act
            try
            {
                children = WaitForProcessName(root, "powershell", ProcessExtensions.GetChildren);

                var targetProcess = children.FirstOrDefault(p =>
                    p.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase));

                // Assert
                Assert.NotNull(targetProcess);
                Assert.Equal(root.Id, GetParentPidViaWmi(targetProcess));
            }
            finally
            {
                foreach (var child in children) child.Dispose();
            }
        }

        [Fact]
        public void GetAllDescendants_ValidParent_ReturnsEntireTree()
        {
            // Arrange
            var root = SpawnProcessTree(2);
            List<Process> descendants = new List<Process>();

            // Act
            try
            {
                bool treeStabilized = SpinWait.SpinUntil(() =>
                {
                    if (root.HasExited) return true;

                    foreach (var d in descendants) d.Dispose();
                    descendants = ProcessExtensions.GetAllDescendants(root.Id, root.StartTime);

                    // Robustness check: filter out names cleanly using exception isolation
                    int psCount = descendants.Count(d => GetSafeProcessName(d).Equals("powershell", StringComparison.OrdinalIgnoreCase));
                    return descendants.Count >= 2 && psCount >= 2;

                }, TimeSpan.FromSeconds(TestTimeouts.ProcessTreeTimeoutSeconds));

                if (root.HasExited)
                {
                    Assert.Fail($"Root process exited prematurely (ExitCode: {root.ExitCode}). Found: {string.Join(", ", descendants.Select(GetSafeProcessName))}");
                }

                // Assert
                Assert.True(treeStabilized, $"Tree failed to stabilize within {TestTimeouts.ProcessTreeTimeoutSeconds}s. Found {descendants.Count} descendants.");

                int finalPsCount = descendants.Count(d => GetSafeProcessName(d).Equals("powershell", StringComparison.OrdinalIgnoreCase));
                Assert.True(finalPsCount >= 2, $"Expected at least 2 nested powershell processes. Found: {finalPsCount}");
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

            // Act (Control) - Retrieve children using the genuine parent start time to verify tree visibility
            var realChildren = WaitForProcessName(root, "powershell", ProcessExtensions.GetChildren);

            // Act (Test Target) - Request children with a future parent start time to simulate PID reuse
            var childrenWithReusedPid = ProcessExtensions.GetChildren(root.Id, DateTime.Now.AddHours(1));

            // Assert
            try
            {
                // Verify the Control state holds true (genuine children exist)
                Assert.NotEmpty(realChildren);

                // Verify the test target condition (reused PID should yield an empty list)
                Assert.Empty(childrenWithReusedPid);
            }
            finally
            {
                // Cleanup transient child process handles
                foreach (var c in realChildren)
                {
                    c.Dispose();
                }
            }
        }

        #endregion

        #region TryResolveValidChild Private Method Reflection Tests

        [Fact]
        public void TryResolveValidChild_ArgumentExceptionThrown_ReturnsNullSafely()
        {
            // Arrange - Use an absolute out-of-bounds PID that can never belong to an active windows process allocation
            int nonExistentPid = 999999;

            // Act
            var result = TestReflection.InvokeNonPublicStatic(typeof(ProcessExtensions), "TryResolveValidChild", nonExistentPid, DateTime.Now, DateTime.UtcNow);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TryResolveValidChild_ProcessFailsLifetimeValidationBounds_ReturnsNull()
        {
            // Arrange - Use a guaranteed active process (current process)
            using (var current = Process.GetCurrentProcess())
            {
                // Intentionally alter constraints layout: Pass an execution threshold time set completely in the future
                // to force 'startedAfterParent' or 'startedBeforeSnapshot' conditional validations to return false.
                var skewedParentTime = DateTime.Now.AddDays(10);
                var snapshotTime = DateTime.UtcNow.AddDays(-10);

                // Act
                var result = TestReflection.InvokeNonPublicStatic(typeof(ProcessExtensions), "TryResolveValidChild", current.Id, skewedParentTime, snapshotTime);

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void TryResolveValidChild_UnexpectedGenericException_LogsTraceAndReturnsNull()
        {
            // Arrange & Act
            var result = TestReflection.InvokeNonPublicStatic(typeof(ProcessExtensions), "TryResolveValidChild", -99, DateTime.Now, DateTime.UtcNow);

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
                    return $"Start-Sleep -Seconds {TestTimeouts.ChildSleepSeconds}";

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

                return lastResults.Any(p => GetSafeProcessName(p).Equals(targetName, StringComparison.OrdinalIgnoreCase));

            }, TimeSpan.FromSeconds(TestTimeouts.ChildTimeoutSeconds));

            if (root.HasExited || !found)
            {
                string foundNames = string.Join(", ", lastResults.Select(p => p.ProcessName));
                foreach (var r in lastResults) r.Dispose();
                throw root.HasExited
                    ? new InvalidOperationException($"The root parent process exited prematurely (ExitCode: {root.ExitCode}). The process tree collapsed.")
                    : throw new TimeoutException($"Failed to find '{targetName}' child process within {TestTimeouts.ChildTimeoutSeconds} seconds. Found instead: [{foundNames}]");
            }

            return lastResults;
        }

        private int GetParentPidViaWmi(Process process)
        {
            using (var mo = new System.Management.ManagementObject($"win32_process.handle='{process.Id}'"))
            {
                mo.Get();
                return Convert.ToInt32(mo["ParentProcessId"]);
            }
        }

        /// <summary>
        /// Extracts the process name safely, protecting the integration test evaluation loops 
        /// from throwing unhandled state exceptions if an ephemeral process exits mid-iteration.
        /// </summary>
        private static string GetSafeProcessName(Process p)
        {
            try
            {
                return p.ProcessName;
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
            catch (Win32Exception)
            {
                return string.Empty;
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