using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides high-performance, recursive process termination logic.
    /// Capable of walking both up (parents) and down (descendants) the process tree.
    /// </summary>
    /// <remarks>
    /// This implementation uses a pre-computed native snapshot map to achieve O(N) 
    /// complexity, avoiding the O(N*D) syscall amplification found in naive recursive implementations.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public class ProcessKiller : IProcessKiller
    {
        #region Safety Guardrails

        /// <summary>
        /// A safelist of critical Windows processes that should never be terminated,
        /// even if they are holding file locks or are part of a target process tree.
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
        /// Evaluates whether a process is protected based on its PID, its name, or its
        /// status as an ancestor of the current execution thread.
        /// </summary>
        /// <param name="pid">The process ID to evaluate.</param>
        /// <param name="processName">The name of the process (e.g., "cmd").</param>
        /// <param name="protectedPids">A set of PIDs belonging to the current process or its parents.</param>
        /// <returns><c>true</c> if the process is protected; otherwise, <c>false</c>.</returns>
        private bool IsProtected(int pid, string processName, HashSet<int> protectedPids)
        {
            // Protection for system-level PIDs and the Servy process chain
            if (pid <= 4 || protectedPids.Contains(pid)) return true;

            if (!string.IsNullOrEmpty(processName))
            {
                string cleanName = processName;
                if (cleanName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    cleanName = cleanName.Substring(0, cleanName.Length - 4);

                if (CriticalSystemProcesses.Contains(cleanName)) return true;
            }
            return false;
        }

        #endregion

        #region Public Interface: Child Termination

        /// <inheritdoc/>
        /// <remarks>
        /// This entry point builds a global native snapshot to ensure that even if intermediate
        /// "bridge" processes have exited, their orphaned descendants are still reachable.
        /// </remarks>
        public void KillChildren(int parentPid)
        {
            int selfPid;
            using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

            // Step 1: Create a handle-less view of the entire system process table
            var byParent = BuildParentChildMapNative();

            // Step 2: Recursively terminate descendants using the map
            WalkAndKillChildren(parentPid, DateTime.MinValue, selfPid, byParent);
        }

        /// <summary>
        /// Performs a single-pass iteration of the OS process table using Toolhelp32 
        /// to build an in-memory parent-to-children relationship map.
        /// </summary>
        /// <returns>A dictionary where keys are Parent PIDs and values are lists of Child PIDs.</returns>
        private Dictionary<int, List<int>> BuildParentChildMapNative()
        {
            var byParent = new Dictionary<int, List<int>>();
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return byParent;

            try
            {
                PROCESSENTRY32 pe32 = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                if (Process32First(snapshot, ref pe32))
                {
                    do
                    {
                        int ppid = (int)pe32.th32ParentProcessID;
                        int pid = (int)pe32.th32ProcessID;

                        if (!byParent.TryGetValue(ppid, out var children))
                        {
                            children = new List<int>();
                            byParent[ppid] = children;
                        }
                        children.Add(pid);
                    } while (Process32Next(snapshot, ref pe32));
                }
            }
            finally { CloseHandle(snapshot); }
            return byParent;
        }

        /// <summary>
        /// The recursive engine that walks down the pre-computed PID map.
        /// </summary>
        /// <param name="parentPid">The PID of the process whose children are being targeted.</param>
        /// <param name="parentStartTime">The creation time of the parent for identity verification.</param>
        /// <param name="selfPid">The PID of the current process to prevent suicide.</param>
        /// <param name="byParent">The pre-computed relationship dictionary.</param>
        private void WalkAndKillChildren(int parentPid, DateTime parentStartTime, int selfPid, Dictionary<int, List<int>> byParent)
        {
            if (!byParent.TryGetValue(parentPid, out var childrenPids)) return;

            foreach (int childPid in childrenPids)
            {
                // Skip if it's the current process or the System process (PID 0-4)
                if (childPid == selfPid || childPid <= 4) continue;

                try
                {
                    using (var child = Process.GetProcessById(childPid))
                    {
                        if (CriticalSystemProcesses.Contains(child.ProcessName)) continue;

                        // Identity check: Ensures we don't kill a process that recycled a PID 
                        // from a target that died before this operation started.
                        if (parentStartTime != DateTime.MinValue && child.StartTime < parentStartTime.AddSeconds(-2))
                            continue;

                        // Depth-First Recursion: Kill the "leaves" of the tree first
                        WalkAndKillChildren(childPid, child.StartTime, selfPid, byParent);

                        if (!child.HasExited)
                        {
                            child.Kill();
                            child.WaitForExit(AppConfig.KillChildWaitMs);
                        }
                    }
                }
                catch (ArgumentException) { /* Process already dead */ }
                catch (Exception ex) { Logger.Debug($"Failed to kill descendant {childPid}: {ex.Message}"); }
            }
        }

        #endregion

        #region Public Interface: Tree & Parent Termination

        /// <inheritdoc/>
        public bool KillProcessTreeAndParents(string processName, bool killParents = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName)) return false;

                // Normalize name (remove .exe extension if present)
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                int selfPid = Process.GetCurrentProcess().Id;
                var protectedPids = GetAncestorPids();
                var byParent = BuildParentChildMapNative();

                // Snapshot only to identify root processes matching the target name
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
                        KillProcessTree(proc, selfPid, protectedPids, byParent);
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
            catch (Win32Exception ex)
            {
                Logger.Error($"Win32 error in KillProcessTreeAndParents('{processName}'): {ex.Message}", ex);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error in KillProcessTreeAndParents('{processName}'): {ex.Message}", ex);
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
            var byParent = BuildParentChildMapNative();

            Process target;
            try { target = Process.GetProcessById(pid); }
            catch (ArgumentException) { return true; } // Already exited

            try
            {
                KillProcessTree(target, selfPid, protectedPids, byParent);
                if (killParents) KillParentProcesses(target, protectedPids);
                return true;
            }
            finally { target.Dispose(); }
        }

        /// <summary>
        /// Internal helper to initiate the downward kill walk followed by killing the root process itself.
        /// </summary>
        private void KillProcessTree(Process process, int selfPid, HashSet<int> protectedPids, Dictionary<int, List<int>> byParent)
        {
            try
            {
                if (protectedPids.Contains(process.Id)) return;

                // Step 1: Kill descendants first
                WalkAndKillChildren(process.Id, process.StartTime, selfPid, byParent);

                // Step 2: Kill the root of the tree
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(AppConfig.KillTreeWaitMs);
                }
            }
            catch (Exception ex) { Logger.Warn($"Error killing process tree for {process.Id}: {ex.Message}"); }
        }

        #endregion

        #region File-Lock Integration

        /// <inheritdoc/>
        public bool KillProcessesUsingFile(IProcessHelper processHelper, string filePath)
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
                var processes = HandleHelper.GetProcessesUsingFile(processHelper, handleExePath, filePath);

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

        #endregion

        #region Native Helper Methods

        /// <summary>
        /// Retrieves the parent process ID of a given <see cref="Process"/> using 
        /// the native <c>NtQueryInformationProcess</c> API.
        /// </summary>
        private int GetParentProcessId(Process process)
        {
            try
            {
                ProcessBasicInformation pbi = new ProcessBasicInformation();
                uint retLen;
                int status = NtQueryInformationProcess(process.Handle, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out retLen);
                return status == 0 ? pbi.InheritedFromUniqueProcessId.ToInt32() : -1;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Recursively walks UP the process tree, terminating parents until a protected 
        /// process (like a system service or the current application) is encountered.
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
        /// Builds a set containing the current process PID and all its parent PIDs.
        /// Used to prevent the auto-killer from committing suicide or killing its own host.
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

        #endregion
    }
}