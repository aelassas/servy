using Servy.Core.Config;
using Servy.Core.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides methods to recursively kill a process tree by process name.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ProcessKiller : IProcessKiller
    {
        /// <summary>
        /// Safelist of processes that should NEVER be targeted by Servy's auto-killer
        /// </summary>
        private static readonly HashSet<string> CriticalSystemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Original core system
            "system", "idle", "csrss", "lsass", "wininit", "services",
            "winlogon", "smss", "svchost", "explorer", "runtimebroker",

            // New additions for host stability
            "dwm", "fontdrvhost", "audiodg", "MsMpEng", "MsSense", "LsaIso",
            "WUDFHost", "wmiprvse", "conhost", "taskhostw", "sihost",
            "ctfmon", "dllhost", "searchindexer", "searchhost"
        };

        /// <summary>
        /// Determines if a process is protected from termination based on its PID or its process name.
        /// </summary>
        /// <param name="pid">The process ID to check.</param>
        /// <param name="processName">The name of the process to check.</param>
        /// <param name="protectedPids">A set of PIDs that are considered protected.</param>
        private bool IsProtected(int pid, string processName, HashSet<int> protectedPids)
        {
            // 1. PID-based protection (Self, Ancestors, System PID 4)
            if (pid <= 4 || protectedPids.Contains(pid))
                return true;

            // 2. Name-based protection
            if (!string.IsNullOrEmpty(processName))
            {
                string cleanName = processName;
                if (cleanName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    cleanName = cleanName.Substring(0, cleanName.Length - 4);

                if (CriticalSystemProcesses.Contains(cleanName))
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void KillChildren(int parentPid)
        {
            // 1. Get self PID to prevent suicide
            int selfPid;
            using (var current = Process.GetCurrentProcess())
            {
                selfPid = current.Id;
            }

            // 2. Try to get parent start time, but DON'T FAIL if the parent is already dead.
            // If the parent is dead, we use a fallback time (DateTime.MinValue) 
            // to allow the first level of children to be found.
            DateTime parentStartTime = DateTime.MinValue;
            try
            {
                using (var parent = Process.GetProcessById(parentPid))
                {
                    parentStartTime = parent.StartTime;
                }
            }
            catch (InvalidOperationException) { /* Parent already exited */ }
            catch (Win32Exception) { /* Access denied */ }
            catch (Exception ex) { Logger.Warn($"Unexpected error getting parent start time: {ex.Message}"); }

            KillChildrenInternal(parentPid, parentStartTime, selfPid);
        }

        /// <summary>
        /// Internal recursive method that terminates child processes while validating 
        /// their identity against the parent's start time to prevent PID-reuse accidents.
        /// </summary>
        /// <param name="parentPid">The process ID of the parent currently being processed.</param>
        /// <param name="parentStartTime">The start time of the parent for identity validation.</param>
        /// <param name="selfPid">The PID of the current process to prevent accidental self-termination.</param>
        private void KillChildrenInternal(int parentPid, DateTime parentStartTime, int selfPid)
        {
            // 1. Collect PROCESS objects instead of just PIDs. 
            // Opening the handle early locks the PID from being reused for a *new* process.
            var childProcesses = new List<Process>();

            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
                return;

            try
            {
                PROCESSENTRY32 pe32 = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };

                if (Process32First(snapshot, ref pe32))
                {
                    do
                    {
                        string exeName = pe32.szExeFile;
                        int childPid = (int)pe32.th32ProcessID;

                        // PRE-CHECK: Skip if the name is in the critical list or it's us
                        if (CriticalSystemProcesses.Contains(Path.GetFileNameWithoutExtension(exeName)) || childPid == selfPid)
                            continue;

                        if (pe32.th32ParentProcessID == parentPid)
                        {
                            try
                            {
                                // Get the process object immediately. This opens a handle.
                                var proc = Process.GetProcessById((int)pe32.th32ProcessID);

                                // Identity Validation: Does this handle belong to a process 
                                // created after (or roughly at) our parent's start time?
                                if (parentStartTime == DateTime.MinValue || proc.StartTime >= parentStartTime.AddSeconds(-2))
                                {
                                    childProcesses.Add(proc);
                                }
                                else
                                {
                                    proc.Dispose(); // Not the process we're looking for.
                                }
                            }
                            catch (Exception ex) { Logger.Debug($"Could not inspect/kill child process.", ex); }
                        }
                    } while (Process32Next(snapshot, ref pe32));
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            // 2. Process the identified tree
            foreach (var child in childProcesses)
            {
                try
                {
                    // Recursion: Pass the verified start time down.
                    // We use the child's PID and its start time to lock the next level.
                    KillChildrenInternal(child.Id, child.StartTime, selfPid);

                    if (!child.HasExited)
                    {
                        child.Kill();
                        child.WaitForExit(AppConfig.KillChildWaitMs);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to kill child process {child.Id}: {ex.Message}");
                }
                finally
                {
                    child.Dispose(); // Release the handle so the PID can finally be recycled by Windows.
                }
            }
        }

        /// <inheritdoc/>
        public bool KillProcessTreeAndParents(string processName, bool killParents = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName)) return false;

                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                int selfPid = Process.GetCurrentProcess().Id;
                var protectedPids = GetAncestorPids();

                // 1. Initial Snapshot strictly for finding the root targets by name
                var allProcesses = Process.GetProcesses();
                try
                {
                    var targetProcesses = allProcesses
                        .Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                        .Where(p => !protectedPids.Contains(p.Id))
                        .ToList();

                    if (targetProcesses.Count == 0) return true;

                    foreach (var proc in targetProcesses)
                    {
                        KillProcessTree(proc, selfPid, protectedPids);
                    }

                    if (killParents)
                    {
                        foreach (var proc in targetProcesses)
                        {
                            KillParentProcesses(proc, protectedPids);
                        }
                    }

                    return true;
                }
                finally
                {
                    foreach (var p in allProcesses) p.Dispose();
                }
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public bool KillProcessTreeAndParents(int pid, bool killParents = true)
        {
            if (pid <= 0) return false;

            var protectedPids = GetAncestorPids();
            if (protectedPids.Contains(pid))
            {
                Logger.Warn($"Execution blocked: Attempted to kill protected process (PID {pid}).");
                return false;
            }

            int selfPid = Process.GetCurrentProcess().Id;

            Process target;
            try
            {
                // No need for a global snapshot just to find one PID
                target = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return true; // Process already gone
            }

            try
            {
                KillProcessTree(target, selfPid, protectedPids);

                if (killParents)
                {
                    KillParentProcesses(target, protectedPids);
                }

                return true;
            }
            finally
            {
                target.Dispose();
            }
        }

        /// <inheritdoc/>
        public bool KillProcessesUsingFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"File not found: {filePath}");
                return true;
            }

            var handleExePath = AppConfig.GetHandleExePath();
            if (!File.Exists(handleExePath))
            {
                Logger.Error($"Handle.exe not found at: {handleExePath}");
                return false;
            }

            bool success = true;

            try
            {
                var processes = HandleHelper.GetProcessesUsingFile(handleExePath, filePath);

                foreach (var procInfo in processes)
                {
                    // 1. Validate procInfo
                    if (procInfo.ProcessId <= 0) continue;

                    // 2. Secondary Safelist Guardrail
                    // Even if a system process locks a file, killing it is usually more 
                    // destructive than failing the file operation.
                    if (!string.IsNullOrEmpty(procInfo.ProcessName) &&
                        CriticalSystemProcesses.Contains(procInfo.ProcessName))
                    {
                        Logger.Warn($"Skipping kill request for critical system process: {procInfo.ProcessName} (PID {procInfo.ProcessId})");
                        success = false;
                        continue;
                    }

                    // 3. Surgical Kill by PID
                    // Ensure KillProcessTreeAndParents has an overload that accepts 'int pid'
                    if (!KillProcessTreeAndParents(procInfo.ProcessId))
                    {
                        Logger.Error($"Failed to kill process {procInfo.ProcessName} (PID {procInfo.ProcessId}).");
                        success = false;
                    }
                    else
                    {
                        Logger.Info($"Successfully terminated process tree for PID {procInfo.ProcessId} ({procInfo.ProcessName}) to release lock on {filePath}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enumerate processes using {filePath}.", ex);
                return false;
            }

            return success;
        }

        /// <summary>
        /// Retrieves the parent process ID of a given <see cref="Process"/>.
        /// </summary>
        /// <param name="process">The process to query.</param>
        /// <returns>The parent process ID.</returns>
        private int GetParentProcessId(Process process)
        {
            try
            {
                ProcessBasicInformation pbi = new ProcessBasicInformation();
                uint retLen;
                int status = NtQueryInformationProcess(process.Handle, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out retLen);

                if (status != 0) // STATUS_SUCCESS == 0
                    return -1;   // could not query parent

                return pbi.InheritedFromUniqueProcessId.ToInt32();
            }
            catch
            {
                return -1; // safer fallback
            }
        }

        /// <summary>
        /// Recursively kills the specified process and all its child processes 
        /// using live Toolhelp32 snapshots to prevent mid-walk evasion.
        /// </summary>
        private void KillProcessTree(Process process, int selfPid, HashSet<int> protectedPids)
        {
            try
            {
                if (protectedPids.Contains(process.Id)) return;

                // 1. Live-Snapshot Recursion: Walk downwards and kill all descendants
                KillChildrenInternal(process.Id, process.StartTime, selfPid);

                // 2. Finally, kill the target itself
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(AppConfig.KillTreeWaitMs);
                }
            }
            catch (InvalidOperationException) { /* Process already exited */ }
            catch (Win32Exception) { /* Access denied */ }
            catch (Exception ex)
            {
                Logger.Warn($"Unexpected error killing process tree: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively kills the parent processes of the specified process.
        /// </summary>
        private void KillParentProcesses(Process process, HashSet<int> protectedPids)
        {
            try
            {
                int parentId = GetParentProcessId(process);

                // Stop at System/Idle or if the parent is part of the current execution chain
                if (parentId <= 4 || protectedPids.Contains(parentId)) return;

                // Fetch parent dynamically instead of relying on a stale snapshot
                Process parent;
                try
                {
                    parent = Process.GetProcessById(parentId);
                }
                catch (ArgumentException)
                {
                    return; // Parent no longer exists
                }

                try
                {
                    // Check parent name against safelist
                    if (IsProtected(parent.Id, parent.ProcessName, protectedPids))
                    {
                        Logger.Debug($"Aborting parent kill walk: {parent.ProcessName} (PID {parent.Id}) is protected.");
                        return;
                    }

                    // Verify identity to prevent recycled PID accidents
                    try
                    {
                        if (parent.StartTime > process.StartTime.AddSeconds(2)) return;
                    }
                    catch (Win32Exception) { return; }

                    // Move up first
                    KillParentProcesses(parent, protectedPids);

                    if (!parent.HasExited)
                    {
                        parent.Kill();
                        parent.WaitForExit(AppConfig.KillParentWaitMs);
                    }
                }
                finally
                {
                    parent.Dispose();
                }
            }
            catch
            {
                // Catch all to ensure one failing parent doesn't stop the chain
            }
        }

        /// <summary>
        /// Gets the PID of the current process and all its ancestors.
        /// </summary>
        private HashSet<int> GetAncestorPids()
        {
            var ancestors = new HashSet<int>();
            try
            {
                int currentPid;
                int parentId;

                // 1. Get current PID and dispose immediately
                using (var current = Process.GetCurrentProcess())
                {
                    currentPid = current.Id;
                    ancestors.Add(currentPid);
                    parentId = GetParentProcessId(current);
                }

                // 2. Walk up the tree
                // Stop at System (PID 4) or if we hit a loop/error
                while (parentId > 4)
                {
                    if (!ancestors.Add(parentId)) break; // Prevent infinite loops

                    try
                    {
                        using (var parent = Process.GetProcessById(parentId))
                        {
                            parentId = GetParentProcessId(parent);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Parent process no longer exists
                        break;
                    }
                    catch (Win32Exception)
                    {
                        // Access denied to the parent process
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not fully resolve ancestor tree: {ex.Message}");
            }
            return ancestors;
        }

    }
}
