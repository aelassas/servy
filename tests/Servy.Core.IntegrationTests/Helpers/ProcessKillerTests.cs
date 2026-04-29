using Servy.Core.Helpers;
using System.Diagnostics;
using System.Reflection;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Represents an exhaustive integration test suite for the ProcessKiller class, validating process tree termination, safelist protections, and file-lock release mechanisms.
    /// </summary>
    public class ProcessKillerIntegrationTests : IDisposable
    {
        private readonly ProcessKiller _processKiller;
        private readonly List<Process> _trackedProcesses;
        private readonly List<string> _tempFiles;
        private readonly string _handleExePath;

        /// <summary>
        /// Initializes a new instance of the ProcessKillerIntegrationTests class, configuring tracking lists for safe teardown and ensuring necessary diagnostic utilities are extracted.
        /// </summary>
        public ProcessKillerIntegrationTests()
        {
            _processKiller = new ProcessKiller();
            _trackedProcesses = new List<Process>();
            _tempFiles = new List<string>();
            _handleExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handle64.exe");

            ExtractHandleExe();
        }

        /// <summary>
        /// Cleans up any leaked processes or temporary files that were generated during the execution of the test methods.
        /// </summary>
        public void Dispose()
        {
            foreach (var process in _trackedProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    process.Dispose();
                }
                catch
                {
                    // Swallow cleanup errors to prevent test runner crashes
                }
            }

            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }

        /// <summary>
        /// Verifies that providing a null, empty, or whitespace process name to the termination method results in a safe bypass returning false.
        /// </summary>
        /// <param name="invalidName">The invalid string input simulating a malformed process name.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void KillProcessTreeAndParents_NullOrEmptyName_ReturnsFalse(string? invalidName)
        {
            bool result = _processKiller.KillProcessTreeAndParents(invalidName!, killParents: true);
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that attempting to terminate a critical Windows system process by name is actively blocked by the internal guardrails.
        /// </summary>
        /// <param name="protectedName">The name of the critical system process, optionally including the executable extension.</param>
        [Theory]
        [InlineData("svchost")]
        [InlineData("csrss.exe")]
        [InlineData("explorer")]
        public void KillProcessTreeAndParents_ProtectedProcessName_ReturnsFalse(string protectedName)
        {
            bool result = _processKiller.KillProcessTreeAndParents(protectedName, killParents: true);
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that providing an invalid or non-positive process identifier results in a safe bypass returning false.
        /// </summary>
        /// <param name="invalidPid">The numerical identifier simulating an invalid process ID.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-999)]
        public void KillProcessTreeAndParents_InvalidPid_ReturnsFalse(int invalidPid)
        {
            bool result = _processKiller.KillProcessTreeAndParents(invalidPid, killParents: true);
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that attempting to terminate the test runner's own process tree is blocked by the ancestor protection logic to prevent accidental suicide.
        /// </summary>
        [Fact]
        public void KillProcessTreeAndParents_SelfPid_ReturnsFalse()
        {
            int currentPid = Process.GetCurrentProcess().Id;
            bool result = _processKiller.KillProcessTreeAndParents(currentPid, killParents: true);
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that a valid process name that does not currently correspond to any running processes is handled gracefully and returns true.
        /// </summary>
        [Fact]
        public void KillProcessTreeAndParents_NonExistentProcessName_ReturnsTrue()
        {
            string nonExistentName = $"fake_process_{Guid.NewGuid()}";
            bool result = _processKiller.KillProcessTreeAndParents(nonExistentName, killParents: true);
            Assert.True(result);
        }

        /// <summary>
        /// Verifies that executing the child termination method successfully halts descendant processes while leaving the specified parent process intact.
        /// </summary>
        [Fact]
        public void KillChildren_TargetParent_KillsOnlyDescendants()
        {
            var (parent, child) = SpawnProcessTree();

            _processKiller.KillChildren(parent!.Id);

            // Allow OS time to process termination signals
            Thread.Sleep(500);

            Assert.True(child!.HasExited, "The child process should have been terminated.");
            Assert.False(parent!.HasExited, "The parent process should remain alive.");
        }

        /// <summary>
        /// Verifies that executing the tree termination method with the kill parents flag enabled successfully walks up the process hierarchy and terminates both the target and its creator.
        /// </summary>
        [Fact]
        public void KillProcessTreeAndParents_KillParentsTrue_KillsEntireChain()
        {
            var (parent, child) = SpawnProcessTree();
            int parentId = parent!.Id;
            int childId = child!.Id;

            // Execution: Start the upward/downward kill walk
            bool result = _processKiller.KillProcessTreeAndParents(childId, killParents: true);

            // Validation: Use a polling loop with refreshes to handle OS termination latency
            bool childExited = WaitForProcessExit(child, 5000);
            bool parentExited = WaitForProcessExit(parent, 5000);

            Assert.True(result, "The termination method should return true on success.");
            Assert.True(childExited, $"The child process (PID {childId}) should have exited within the timeout.");
            Assert.True(parentExited, $"The parent process (PID {parentId}) should have been terminated through the upward walk.");
        }

        /// <summary>
        /// Helper to poll for process exit with a timeout and explicit state refreshes.
        /// </summary>
        /// <param name="process">The process to monitor.</param>
        /// <param name="timeoutMs">Maximum wait time in milliseconds.</param>
        /// <returns>True if the process exited; otherwise, false.</returns>
        private bool WaitForProcessExit(Process? process, int timeoutMs)
        {
            if (process == null) return true;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                process.Refresh(); // CRITICAL: Discard cached state
                if (process.HasExited) return true;
                Thread.Sleep(200);
            }
            return false;
        }

        /// <summary>
        /// Verifies that executing the tree termination method with the kill parents flag disabled successfully halts the target and its descendants, but leaves its parent intact.
        /// </summary>
        [Fact]
        public void KillProcessTreeAndParents_KillParentsFalse_LeavesParentAlive()
        {
            var (parent, child) = SpawnProcessTree();

            bool result = _processKiller.KillProcessTreeAndParents(child!.Id, killParents: false);

            child.WaitForExit(3000);

            child.Refresh();
            parent!.Refresh();

            Assert.True(result);
            Assert.True(child.HasExited, "The target child process should have been terminated.");
            Assert.False(parent.HasExited, "The parent process should remain alive because killParents was false.");
        }

        /// <summary>
        /// Verifies that attempting to release file locks on a non-existent file path safely bypasses the internal logic and returns true.
        /// </summary>
        [Fact]
        public void KillProcessesUsingFile_FileNotFound_ReturnsTrue()
        {
            string fakePath = Path.Combine(Path.GetTempPath(), $"missing_file_{Guid.NewGuid()}.txt");
            bool result = _processKiller.KillProcessesUsingFile(fakePath);
            Assert.True(result);
        }

        /// <summary>
        /// Verifies that the file locking resolution logic successfully identifies and terminates a background process holding a strict read lock on a target file.
        /// </summary>
        [Fact]
        public void KillProcessesUsingFile_FileLocked_TerminatesLockingProcess()
        {
            if (!File.Exists(_handleExePath))
            {
                // Cannot reliably test handle integration if the tool is missing from the environment
                return;
            }

            string testFile = Path.Combine(Path.GetTempPath(), $"lock_test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "Lock Data");
            _tempFiles.Add(testFile);

            var lockingProcess = SpawnFileLockingProcess(testFile);

            bool result = _processKiller.KillProcessesUsingFile(testFile);

            Thread.Sleep(1000);

            Assert.True(result);
            Assert.True(lockingProcess?.HasExited, "The background process holding the file lock should have been terminated.");

            // Verify lock is genuinely released by attempting deletion
            File.Delete(testFile);
            Assert.False(File.Exists(testFile));
        }

        /// <summary>
        /// Spawns a PowerShell instance that subsequently launches a nested PowerShell task.
        /// Utilizing a homogeneous process tree prevents native NtQueryInformationProcess struct mismatches
        /// that occur when a 32-bit process attempts to query a 64-bit process's parent.
        /// </summary>
        /// <returns>A tuple containing the initialized parent and child Process objects.</returns>
        private (Process? Parent, Process? Child) SpawnProcessTree()
        {
            // The payload for the child process to keep it alive
            string childScript = "while ($true) { Start-Sleep -Seconds 1 }";
            string encodedChildScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(childScript));

            string psScript = $@"
                $psi = New-Object System.Diagnostics.ProcessStartInfo
                $psi.FileName = 'powershell.exe'
                $psi.Arguments = '-NoProfile -NonInteractive -EncodedCommand {encodedChildScript}'
                $psi.UseShellExecute = $false
                $psi.CreateNoWindow = $true
                $child = [System.Diagnostics.Process]::Start($psi)
                Write-Output ""CHILD_PID:$($child.Id)""
                while ($true) {{ Start-Sleep -Seconds 1 }}
            ";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{psScript}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var parentProcess = Process.Start(psi);
            _trackedProcesses.Add(parentProcess!);

            int childPid = -1;
            while (parentProcess != null && !parentProcess.HasExited)
            {
                string? line = parentProcess.StandardOutput.ReadLine();
                if (line != null && line.StartsWith("CHILD_PID:"))
                {
                    childPid = int.Parse(line.Substring(10));
                    break;
                }
            }

            Assert.True(childPid > 0, "Failed to resolve child process ID from the orchestration script.");
            var childProcess = Process.GetProcessById(childPid);
            _trackedProcesses.Add(childProcess);

            return (parentProcess, childProcess);
        }

        /// <summary>
        /// Spawns a PowerShell instance that opens an exclusive read lock on the specified file path, simulating a background worker that refuses to release I/O resources.
        /// </summary>
        /// <param name="filePath">The absolute path of the file to lock.</param>
        /// <returns>The initialized Process object holding the lock.</returns>
        private Process? SpawnFileLockingProcess(string filePath)
        {
            string psScript = $@"
                $fs = [System.IO.File]::Open('{filePath}', 'Open', 'Read', 'None')
                Write-Output 'LOCKED'
                while ($true) {{ Start-Sleep -Seconds 1 }}
            ";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{psScript}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var lockingProcess = Process.Start(psi);
            _trackedProcesses.Add(lockingProcess!);

            while (lockingProcess != null && !lockingProcess.HasExited)
            {
                string? line = lockingProcess.StandardOutput.ReadLine();
                if (line != null && line.Contains("LOCKED"))
                {
                    break;
                }
            }

            return lockingProcess;
        }

        /// <summary>
        /// Extracts the handle64 executable from the test assembly's embedded resources directly to the execution directory to ensure file handle resolution functions optimally.
        /// </summary>
        private void ExtractHandleExe()
        {
            if (File.Exists(_handleExePath)) return;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("handle64.exe", StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
                    using (var fileStream = new FileStream(_handleExePath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream?.CopyTo(fileStream);
                    }
                }
            }
            catch
            {
                // Swallow extraction errors; tests relying on handle.exe will safely bypass
            }
        }
    }
}