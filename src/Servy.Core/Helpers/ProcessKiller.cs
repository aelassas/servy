using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides high-performance, recursive process termination logic. Capable of walking both up the parent chain and down the descendant process tree.
    /// </summary>
    /// <remarks>
    /// This implementation uses pre-computed native snapshot maps to achieve linear complexity. This avoids the severe system call amplification and access denial issues found in naive recursive implementations.
    /// </remarks>
    public class ProcessKiller : IProcessKiller
    {
        #region Safety Guardrails

        /// <summary>
        /// A safelist of critical Windows processes that should never be terminated, even if they are holding file locks or are part of a target process tree.
        /// </summary>
        private static readonly HashSet<string> CriticalSystemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "idle", "csrss", "lsass", "wininit", "services",
            "winlogon", "smss", "svchost", "explorer", "runtimebroker",
            "dwm", "fontdrvhost", "audiodg", "MsMpEng", "MsSense", "LsaIso",
            "WUDFHost", "wmiprvse", "conhost", "taskhostw", "sihost",
            "ctfmon", "dllhost", "searchindexer", "searchhost"
        };

        /// <summary>
        /// A lightweight container for process metadata captured from a native snapshot to prevent redundant operating system queries.
        /// </summary>
        private struct ProcessInfoNode
        {
            /// <summary>
            /// The numerical identifier of the parent process.
            /// </summary>
            public int ParentId;

            /// <summary>
            /// The name of the executable file.
            /// </summary>
            public string Name;
        }

        /// <summary>
        /// Evaluates whether a process is protected based on its PID, its name, or its status as an ancestor of the current execution thread.
        /// </summary>
        /// <param name="pid">The process ID to evaluate.</param>
        /// <param name="processName">The name of the process.</param>
        /// <param name="protectedPids">A set of PIDs belonging to the current process or its parents.</param>
        /// <returns>A boolean indicating true if the process is protected, otherwise false.</returns>
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
        public void KillChildren(int parentPid)
        {
            int selfPid;
            using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

            // Step 1: Create a handle-less view of the entire system process table in a single pass
            var (_, byParent) = BuildSnapshotAndChildMapNative();

            // Step 2: Recursively terminate descendants using the map
            var visited = new HashSet<int>();
            WalkAndKillChildren(parentPid, DateTime.MinValue, selfPid, byParent, visited);
        }

        /// <summary>
        /// Performs a single-pass iteration of the OS process table using Toolhelp32 to build both an in-memory 
        /// parent-to-children relationship map and a complete snapshot map for upward traversal.
        /// </summary>
        /// <returns>A tuple containing the metadata snapshot map and the parent-to-child relationship map.</returns>
        private (Dictionary<int, ProcessInfoNode> Snapshot, Dictionary<int, List<int>> ByParent) BuildSnapshotAndChildMapNative()
        {
            var snapshotMap = new Dictionary<int, ProcessInfoNode>();
            var byParent = new Dictionary<int, List<int>>();
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return (snapshotMap, byParent);

            try
            {
                PROCESSENTRY32 pe32 = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                if (Process32First(snapshot, ref pe32))
                {
                    do
                    {
                        int ppid = (int)pe32.th32ParentProcessID;
                        int pid = (int)pe32.th32ProcessID;

                        // 1. Populate the upward-traversal metadata map
                        snapshotMap[pid] = new ProcessInfoNode
                        {
                            ParentId = ppid,
                            Name = pe32.szExeFile
                        };

                        // 2. Populate the downward-traversal relationship map
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

            return (snapshotMap, byParent);
        }

        /// <summary>
        /// The recursive engine that walks down the pre-computed PID map.
        /// </summary>
        /// <param name="parentPid">The PID of the process whose children are being targeted.</param>
        /// <param name="parentStartTime">The creation time of the parent for identity verification.</param>
        /// <param name="selfPid">The PID of the current process to prevent suicide.</param>
        /// <param name="byParent">The pre-computed relationship dictionary.</param>
        /// <param name="visited">A set of PIDs already processed in this walk to prevent infinite recursion on cycles.</param>
        private void WalkAndKillChildren(
            int parentPid,
            DateTime parentStartTime,
            int selfPid,
            Dictionary<int, List<int>> byParent,
            HashSet<int> visited)
        {
            if (!byParent.TryGetValue(parentPid, out var childrenPids)) return;

            foreach (int childPid in childrenPids)
            {
                // Skip if it's the current process or the System process (PID 0-4)
                if (childPid == selfPid || childPid <= 4) continue;

                // CYCLE GUARD: If we have already visited this child in the current walk, 
                // skip it to prevent a StackOverflowException from infinite recursion.
                if (!visited.Add(childPid))
                {
                    Logger.Debug($"WalkAndKillChildren: Cycle detected or redundant PID encountered ({childPid}). Skipping.");
                    continue;
                }

                try
                {
                    using (var child = Process.GetProcessById(childPid))
                    {
                        if (CriticalSystemProcesses.Contains(child.ProcessName)) continue;

                        // Identity check: Ensures we don't kill a process that recycled a PID 
                        // from a target that died before this operation started.
                        if (parentStartTime != DateTime.MinValue && child.StartTime < parentStartTime.AddSeconds(-2))
                            continue;

                        // Depth-First Recursion: Kill the leaves of the tree first.
                        // We pass the same visited set down the stack.
                        WalkAndKillChildren(childPid, child.StartTime, selfPid, byParent, visited);

                        if (!child.HasExited)
                        {
                            child.Kill();
                            if (!child.WaitForExit(SafeWait(AppConfig.KillChildWaitMs)))
                            {
                                Logger.Warn($"Child process {child.Id} did not exit within the safety window.");
                            }
                        }
                    }
                }
                catch (ArgumentException) { /* Process already dead */ }
                catch (Exception ex) { Logger.Warn($"Failed to kill descendant {childPid} ({ex.GetType().Name}): {ex.Message}"); }
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

                // Normalize name
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                // SECURITY: Reject critical system process names up-front
                if (CriticalSystemProcesses.Contains(processName))
                {
                    Logger.Warn($"Refused to kill critical system process by name: '{processName}'.");
                    return false;
                }

                int selfPid;
                using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

                // Single Toolhelp32 walk populates both maps, eliminating redundant syscalls and race conditions
                var (completeSnapshot, byParent) = BuildSnapshotAndChildMapNative();
                var protectedPids = GetAncestorPids(completeSnapshot);

                var allProcesses = Process.GetProcesses();
                try
                {
                    var targetProcesses = allProcesses
                        .Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                        .Where(p => !protectedPids.Contains(p.Id))
                        .ToList();

                    if (targetProcesses.Count == 0) return true;

                    // ROBUSTNESS: Perform the parent kill walk BEFORE the tree kill walk.
                    // This ensures that we query information while the target process handles are still valid.
                    if (killParents)
                    {
                        foreach (var proc in targetProcesses)
                        {
                            KillParentProcesses(proc.Id, proc.StartTime, protectedPids, completeSnapshot);
                        }
                    }

                    foreach (var proc in targetProcesses)
                    {
                        KillProcessTree(proc, selfPid, protectedPids, byParent);
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
                Logger.Error($"Win32 error in KillProcessTreeAndParents('{processName}').", ex);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error in KillProcessTreeAndParents('{processName}').", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public bool KillProcessTreeAndParents(int pid, bool killParents = true)
        {
            if (pid <= 0) return false;

            try
            {
                // Single Toolhelp32 walk populates both maps
                var (completeSnapshot, byParent) = BuildSnapshotAndChildMapNative();
                var protectedPids = GetAncestorPids(completeSnapshot);

                // SECURITY: Resolve process handle and apply full safety check
                Process target;
                try { target = Process.GetProcessById(pid); }
                catch (ArgumentException) { return true; } // Already exited

                try
                {
                    if (IsProtected(target.Id, target.ProcessName, protectedPids))
                    {
                        Logger.Warn($"Execution blocked: Attempted to kill protected process {target.ProcessName} (PID {pid}).");
                        return false;
                    }

                    int selfPid;
                    using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

                    // ROBUSTNESS: Perform the parent kill walk BEFORE the tree kill walk.
                    if (killParents) KillParentProcesses(target.Id, target.StartTime, protectedPids, completeSnapshot);
                    KillProcessTree(target, selfPid, protectedPids, byParent);

                    return true;
                }
                finally { target.Dispose(); }
            }
            catch (Win32Exception ex)
            {
                Logger.Error($"Win32 error in KillProcessTreeAndParents(pid={pid}).", ex);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error in KillProcessTreeAndParents(pid={pid}).", ex);
                return false;
            }
        }

        /// <summary>
        /// Internal helper to initiate the downward kill walk followed by killing the root process itself.
        /// </summary>
        /// <param name="process">The target process acting as the root of the tree to terminate.</param>
        /// <param name="selfPid">The numerical identifier of the current executing process to avoid suicide operations.</param>
        /// <param name="protectedPids">A set of numerical identifiers corresponding to protected ancestor paths.</param>
        /// <param name="byParent">A pre-computed native map establishing hierarchical relationships across the system.</param>
        private void KillProcessTree(Process process, int selfPid, HashSet<int> protectedPids, Dictionary<int, List<int>> byParent)
        {
            try
            {
                // SECURITY: Use IsProtected instead of a simple PID check to ensure system-critical names are never targeted.
                if (IsProtected(process.Id, process.ProcessName, protectedPids)) return;

                var visited = new HashSet<int>();
                WalkAndKillChildren(process.Id, process.StartTime, selfPid, byParent, visited);

                if (!process.HasExited)
                {
                    process.Kill();
                    if (!process.WaitForExit(SafeWait(AppConfig.KillTreeWaitMs)))
                    {
                        Logger.Warn($"Process tree head {process.Id} did not exit within the safety window.");
                    }
                }
            }
            catch (Exception ex) { Logger.Warn($"Error killing process tree for {process.Id}: {ex.Message}"); }
        }

        #endregion

        #region File-Lock Integration

        /// <inheritdoc/>
        public bool KillProcessesUsingFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Info($"File not found: {filePath}");
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
                    if (procInfo.ProcessId <= 0) continue;

                    // Secondary Safelist Guardrail
                    if (!string.IsNullOrEmpty(procInfo.ProcessName) &&
                        CriticalSystemProcesses.Contains(procInfo.ProcessName))
                    {
                        Logger.Warn($"Skipping kill request for critical system process: {procInfo.ProcessName} (PID {procInfo.ProcessId})");
                        success = false;
                        continue;
                    }

                    // Surgical Kill by PID
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

        #region Helper Methods

        /// <summary>
        /// Recursively walks up the process tree using the pre-computed snapshot. Terminates parents only after confirming they are not protected.
        /// </summary>
        /// <param name="childPid">The numerical identifier of the child process whose parents are being targeted.</param>
        /// <param name="childStartTime">The creation time of the child process for identity verification.</param>
        /// <param name="protectedPids">A set of protected numerical identifiers corresponding to the executing platform infrastructure.</param>
        /// <param name="snapshot">A pre-computed native map establishing hierarchical relationships and names across the system.</param>
        private void KillParentProcesses(int childPid, DateTime childStartTime, HashSet<int> protectedPids, Dictionary<int, ProcessInfoNode> snapshot)
        {
            if (!snapshot.TryGetValue(childPid, out var node)) return;

            int parentId = node.ParentId;

            // Stop at System/Idle or if the parent is part of the current execution chain
            if (parentId <= 4 || protectedPids.Contains(parentId)) return;

            if (!snapshot.TryGetValue(parentId, out var parentNode)) return;

            // SECURITY: Use the name from the snapshot to check protection, avoiding access denied exceptions.
            if (IsProtected(parentId, parentNode.Name, protectedPids))
            {
                Logger.Debug($"Aborting parent kill walk: {parentNode.Name} (PID {parentId}) is protected.");
                return;
            }

            DateTime parentStartTime = DateTime.MinValue;
            try
            {
                using (var p = Process.GetProcessById(parentId))
                {
                    try { parentStartTime = p.StartTime; } catch { /* keep MinValue */ }
                }
            }
            catch (ArgumentException) { /* parent already dead - abort walk */ return; }

            // Move up further first via post-order traversal - pass real anchor
            KillParentProcesses(parentId, parentStartTime, protectedPids, snapshot);

            try
            {
                using (var parentProcess = Process.GetProcessById(parentId))
                {
                    // Identity check to prevent recycled PID accidents
                    try
                    {
                        if (childStartTime != DateTime.MinValue && parentProcess.StartTime > childStartTime.AddSeconds(2))
                            return;
                    }
                    catch (Win32Exception) { /* Handle access denied on StartTime, proceed with caution */ }

                    if (!parentProcess.HasExited)
                    {
                        parentProcess.Kill();
                        parentProcess.WaitForExit(SafeWait(AppConfig.KillParentWaitMs));
                    }
                }
            }
            catch (ArgumentException) { /* Already dead */ }
            catch (Exception ex) { Logger.Warn($"Failed to kill parent {parentId} ({ex.GetType().Name}): {ex.Message}"); }
        }

        /// <summary>
        /// Builds a set containing the current process PID and all its parent PIDs using a lightweight snapshot traversal. Used to prevent the auto-killer from committing suicide or killing its own host.
        /// </summary>
        /// <param name="snapshot">A pre-computed native map establishing hierarchical relationships across the system.</param>
        /// <returns>A hash set representing the structural lineage mapping backward toward the root operating system session.</returns>
        private HashSet<int> GetAncestorPids(Dictionary<int, ProcessInfoNode> snapshot)
        {
            var ancestors = new HashSet<int>();
            try
            {
                int currentPid;
                using (var current = Process.GetCurrentProcess())
                {
                    currentPid = current.Id;
                }

                ancestors.Add(currentPid);
                int currentSearchPid = currentPid;

                // Walk up the tree using the snapshot to avoid handle access restrictions
                while (snapshot.TryGetValue(currentSearchPid, out var node))
                {
                    int parentId = node.ParentId;
                    if (parentId <= 4) break;
                    if (!ancestors.Add(parentId)) break; // Prevent infinite loops
                    currentSearchPid = parentId;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not fully resolve ancestor tree: {ex.Message}");
            }
            return ancestors;
        }

        /// <summary>
        /// Ensures the configured timeout respects the system-wide minimum safety floor.
        /// </summary>
        /// <param name="configuredMs">The timeout value from the user configuration.</param>
        /// <returns>The larger of the configured value or <see cref="AppConfig.MinKillWaitMs"/>.</returns>
        private static int SafeWait(int configuredMs) => Math.Max(configuredMs, AppConfig.MinKillWaitMs);


        #endregion
    }
}