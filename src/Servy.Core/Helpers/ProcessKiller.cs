using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using System.ComponentModel;
using System.Diagnostics;

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
        /// Centralized helper to evaluate if a process name belongs to the critical system safelist.
        /// Handles normalization of '.exe' suffixes to ensure consistent lookup regardless of the data source.
        /// </summary>
        private bool IsCriticalProcess(string? processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;

            string cleanName = processName;
            if (cleanName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                cleanName = cleanName.Substring(0, cleanName.Length - 4);

            return CriticalSystemProcesses.Contains(cleanName);
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

            return IsCriticalProcess(processName);
        }

        #endregion

        #region Public Interface: Child Termination

        /// <inheritdoc/>
        public void KillChildren(int parentPid)
        {
            int selfPid;
            using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

            // Step 1: Create a handle-less view of the entire system process table in a single pass
            var (snapshot, byParent) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();

            // SECURITY: Get ancestor PIDs to prevent killing the Servy process chain if it happens 
            // to be a descendant (e.g. through PID reuse or complex supervisor patterns)
            var protectedPids = GetAncestorPids(snapshot);

            // Step 2: Recursively terminate descendants using the map
            var visited = new HashSet<int>();
            WalkAndKillChildren(parentPid, DateTime.MinValue, selfPid, protectedPids, byParent, visited);
        }

        /// <summary>
        /// The recursive engine that walks down the pre-computed PID map.
        /// </summary>
        /// <param name="parentPid">The PID of the process whose children are being targeted.</param>
        /// <param name="parentStartTime">The creation time of the parent for identity verification.</param>
        /// <param name="selfPid">The PID of the current process to prevent suicide.</param>
        /// <param name="protectedPids">A set of numerical identifiers corresponding to protected ancestor paths.</param>
        /// <param name="byParent">The pre-computed relationship dictionary.</param>
        /// <param name="visited">A set of PIDs already processed in this walk to prevent infinite recursion on cycles.</param>
        private void WalkAndKillChildren(
            int parentPid,
            DateTime parentStartTime,
            int selfPid,
            HashSet<int> protectedPids,
            Dictionary<int, List<int>> byParent,
            HashSet<int> visited)
        {
            if (!byParent.TryGetValue(parentPid, out var childrenPids)) return;

            foreach (int childPid in childrenPids)
            {
                if (childPid == selfPid || childPid <= 4) continue;

                if (!visited.Add(childPid))
                {
                    Logger.Debug($"WalkAndKillChildren: Cycle detected or redundant PID encountered ({childPid}). Skipping.");
                    continue;
                }

                try
                {
                    using (var child = Process.GetProcessById(childPid))
                    {
                        // SECURITY: Use centralized IsProtected as the single source of truth 
                        // to guard against killing ancestors or system critical processes.
                        if (IsProtected(childPid, child.ProcessName, protectedPids)) continue;

                        // Identity check: Ensures we don't kill a process that recycled a PID 
                        // from a target that died before this operation started.
                        if (parentStartTime != DateTime.MinValue && child.StartTime < parentStartTime.AddSeconds(-AppConfig.PidReuseToleranceSeconds))
                            continue;

                        // Depth-First Recursion: Kill the leaves of the tree first.
                        // We pass the same visited set and protected PIDs down the stack.
                        WalkAndKillChildren(childPid, child.StartTime, selfPid, protectedPids, byParent, visited);

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
                catch (Exception ex) { Logger.Warn($"Failed to kill descendant {childPid}.", ex); }
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

                // SECURITY: Reject critical system process names up-front using centralized normalization
                if (IsCriticalProcess(processName))
                {
                    Logger.Warn($"Refused to kill critical system process by name: '{processName}'.");
                    return false;
                }

                // Internal normalization for consistent LINQ comparison (Process.ProcessName is extension-less)
                string normalizedName = processName;
                if (normalizedName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    normalizedName = normalizedName.Substring(0, normalizedName.Length - 4);

                int selfPid;
                using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

                var (completeSnapshot, byParent) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();
                var protectedPids = GetAncestorPids(completeSnapshot);

                var allProcesses = Process.GetProcesses();
                try
                {
                    var targetProcesses = allProcesses
                        .Where(p => string.Equals(p.ProcessName, normalizedName, StringComparison.OrdinalIgnoreCase))
                        .Where(p => !protectedPids.Contains(p.Id))
                        .ToList();

                    if (targetProcesses.Count == 0) return true;

                    // ROBUSTNESS: Perform the parent kill walk BEFORE the tree kill walk.
                    // This ensures that we query information while the target process handles are still valid.
                    if (killParents)
                    {
                        foreach (var proc in targetProcesses)
                        {
                            KillParentProcesses(proc.Id, proc.StartTime, protectedPids, completeSnapshot, new HashSet<int>());
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
                var (completeSnapshot, byParent) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();
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
                    if (killParents) KillParentProcesses(target.Id, target.StartTime, protectedPids, completeSnapshot, new HashSet<int>());
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

                // Thread protectedPids into the walk
                WalkAndKillChildren(process.Id, process.StartTime, selfPid, protectedPids, byParent, visited);

                if (!process.HasExited)
                {
                    process.Kill();
                    if (!process.WaitForExit(SafeWait(AppConfig.KillTreeWaitMs)))
                    {
                        Logger.Warn($"Process tree head {process.Id} did not exit within the safety window.");
                    }
                }
            }
            catch (Exception ex) { Logger.Warn($"Error killing process tree for {process.Id}.", ex); }
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

                    // FIX: Using IsCriticalProcess ensures handle.exe output (with .exe) is correctly validated against the safelist
                    if (IsCriticalProcess(procInfo.ProcessName))
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
        /// <param name="visited">A tracking set of PIDs already evaluated in this walk to prevent StackOverflow exceptions from PID-reuse cycles.</param>
        private void KillParentProcesses(int childPid, DateTime childStartTime, HashSet<int> protectedPids, Dictionary<int, ProcessInfoNode> snapshot, HashSet<int> visited)
        {
            // CYCLE GUARD: Prevent infinite recursion if Windows PID reuse creates a cycle in the snapshot.
            if (!visited.Add(childPid))
            {
                Logger.Debug($"KillParentProcesses: cycle detected at PID {childPid}. Aborting upward walk.");
                return;
            }

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

            // Move up further first via post-order traversal - pass real anchor and the visited set
            KillParentProcesses(parentId, parentStartTime, protectedPids, snapshot, visited);

            try
            {
                using (var parentProcess = Process.GetProcessById(parentId))
                {
                    try
                    {
                        if (childStartTime != DateTime.MinValue && parentProcess.StartTime > childStartTime.AddSeconds(AppConfig.PidReuseToleranceSeconds))
                            return;
                    }
                    catch (Win32Exception) { /* Handle access denied on StartTime, proceed with caution */ }

                    if (!parentProcess.HasExited)
                    {
                        parentProcess.Kill();
                        if (!parentProcess.WaitForExit(SafeWait(AppConfig.KillParentWaitMs)))
                        {
                            Logger.Warn($"Parent process {parentProcess.Id} did not exit within the safety window.");
                        }
                    }
                }
            }
            catch (ArgumentException) { /* Already dead */ }
            catch (Exception ex) { Logger.Warn($"Failed to kill parent {parentId}.", ex); }
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
                using (var current = Process.GetCurrentProcess()) { currentPid = current.Id; }

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
            catch (Exception ex) { Logger.Warn($"Could not fully resolve ancestor tree: {ex.Message}"); }
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