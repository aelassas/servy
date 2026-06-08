using Servy.Core.Helpers;
using System.Diagnostics;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Represents an exhaustive integration test suite for the ProcessKiller class, validating process tree termination, safelist protections, and file-lock release mechanisms.
    /// </summary>
    public class ProcessKillerIntegrationTests : IDisposable
    {
        private readonly string _handleExePath;
        private readonly ProcessKiller _processKiller;
        private readonly List<Process> _trackedProcesses;
        private readonly List<string> _tempFiles;

        /// <summary>
        /// Initializes a new instance of the ProcessKillerIntegrationTests class, configuring tracking lists for safe teardown and ensuring necessary diagnostic utilities are extracted.
        /// </summary>
        public ProcessKillerIntegrationTests()
        {
            _processKiller = new ProcessKiller();
            _trackedProcesses = new List<Process>();
            _tempFiles = new List<string>();

            // 1. Force execution asset extraction to disk
            Testing.Helper.ExtractHandleExe();

            // 2. Fetch the resolved cross-architecture path string token
            _handleExePath = Testing.Helper.HandleExePath;

            // 3. CRITICAL DEFECT GUARD: Assert file physically exists right now
            // If extraction fails due to directory locks, this stops the test context immediately with an explicit error.
            Assert.True(File.Exists(_handleExePath), $"Lifecycle Extraction Fault: '{_handleExePath}' could not be verified on the local disk file table.");

            // Auto-accept Sysinternals EULA in the registry hive context to prevent headless runner hangs
            Testing.Helper.AcceptSysinternalsEula();
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

            // DO NOT delete handle64.exe here.
            // Deleting an executable while another test's constructor is initializing causes the IOException.
            // Leaving it in the bin folder is completely safe for integration tests.
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

            // 1. Spawn the process
            var lockingProcess = SpawnFileLockingProcess(testFile);

            // Stabilization Step A: Ensure the process hasn't crashed before we proceed
            if (lockingProcess == null || lockingProcess.HasExited)
            {
                throw new InvalidOperationException("Failed to spawn a stable file-locking process.");
            }

            // CRITICAL - Stabilization Step B: Wait for the child process to physically claim the file lock handle.
            // We poll by trying to open the file exclusively. Once an IOException triggers, the lock is live.
            bool lockConfirmed = SpinWait.SpinUntil(() =>
            {
                if (lockingProcess.HasExited) return false;
                try
                {
                    using (var stream = File.Open(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // If we successfully open it, the child hasn't acquired its lock yet. Keep polling.
                        return false;
                    }
                }
                catch (IOException)
                {
                    // Lock is officially verified in the OS handle table!
                    return true;
                }
                catch
                {
                    return false;
                }
            }, TimeSpan.FromSeconds(10)); // Generous timeout for slow CI environments

            if (!lockConfirmed)
            {
                throw new InvalidOperationException("The background process failed to acquire the file lock within the allowed timeout window.");
            }

            // 2 & 3. Act & Backoff/Retry Phase for Process Termination
            bool result = false;
            bool exited = false;
            int killAttempts = 0;
            const int maxKillAttempts = 3;

            while (killAttempts < maxKillAttempts && !exited)
            {
                // Attempt to kill processes holding the lock
                result = _processKiller.KillProcessesUsingFile(testFile);

                // GitHub Actions runners can be slow; wait up to 3 seconds per attempt for the process to actually exit
                exited = SpinWait.SpinUntil(() => lockingProcess.HasExited, TimeSpan.FromSeconds(3));

                if (!exited)
                {
                    killAttempts++;
                    if (killAttempts < maxKillAttempts)
                    {
                        // Give the OS handle table a moment to update before attempting the kill again
                        Thread.Sleep(1000);
                    }
                }
            }

            // 4. Assertions
            Assert.True(result, "KillProcessesUsingFile should return true.");
            Assert.True(exited, $"The background process holding the file lock should have been terminated after {killAttempts + 1} attempts.");

            // 5. Backoff/Retry Phase for File Deletion
            bool deleted = false;
            int retries = 0;
            while (retries < 10 && !deleted)
            {
                try
                {
                    File.Delete(testFile);
                    deleted = true;
                }
                catch (IOException)
                {
                    // Lock still held by the OS kernel; back off and try again
                    Thread.Sleep(200);
                    retries++;
                }
            }

            Assert.True(deleted, $"Failed to delete '{testFile}' after process termination. The lock was not genuinely released.");
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
            // 1. Locate the absolute path for PowerShell
            // This avoids issues where the test runner might not have permissions to execute shims 
            // or relative binaries from the bin folder.
            string psPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");

            // The payload for the child process to keep it alive
            string childScript = "while ($true) { Start-Sleep -Seconds 1 }";
            string encodedChildScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(childScript));

            // Use a neutral temp path for all working directories
            string tempPath = Path.GetTempPath();

            // 2. Inject absolute paths and explicit working directories into the inner script
            string psScript = $@"
                $psi = New-Object System.Diagnostics.ProcessStartInfo
                $psi.FileName = '{psPath.Replace(@"\", @"\\")}'
                $psi.Arguments = '-NoProfile -NonInteractive -EncodedCommand {encodedChildScript}'
                $psi.UseShellExecute = $false
                $psi.CreateNoWindow = $true
                $psi.WorkingDirectory = '{tempPath.Replace(@"\", @"\\")}'
                $child = [System.Diagnostics.Process]::Start($psi)
                Write-Output ""CHILD_PID:$($child.Id)""
                while ($true) {{ Start-Sleep -Seconds 1 }}
            ";

            var psi = new ProcessStartInfo
            {
                FileName = psPath,
                Arguments = $"-NoProfile -NonInteractive -Command \"{psScript}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,

                // 3. FIX: Move execution context to a neutral location to bypass restricted bin folders
                WorkingDirectory = tempPath
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
    }
}