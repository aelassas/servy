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
        /// A safelist of critical Windows processes that should never be terminated, even if they 
        /// are holding file locks or are part of a target process tree.
        /// </summary>
        /// <remarks>
        /// NOTE: This list must be updated when Windows introduces new kernel-pseudo-host processes 
        /// (e.g., future virtualization services or hypervisor hosts).
        /// </remarks>
        private static readonly HashSet<string> CriticalSystemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core Legacy Processes
            "system", "idle", "csrss", "lsass", "wininit", "services",
            "winlogon", "smss", "svchost", "explorer", "runtimebroker",
            "dwm", "fontdrvhost", "audiodg", "MsMpEng", "MsSense", "LsaIso",
            "WUDFHost", "wmiprvse", "conhost", "taskhostw", "sihost",
            "ctfmon", "dllhost", "searchindexer", "searchhost",

            // Modern Win10/11+ Pseudo-System Processes
            "Registry",
            "MemCompression",
            "Secure System",

            // Virtualization & Container Hosts
            "vmcompute",      // Hyper-V Host Compute Service (Orchestrates VMs)
            "vmms"            // Hyper-V Virtual Machine Management Service
        };

        /// <summary>
        /// Centralized helper to evaluate if a process name belongs to the critical system safelist.
        /// Handles normalization of '.exe' suffixes to ensure consistent lookup regardless of the data source.
        /// </summary>
        private bool IsCriticalProcess(string? processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;

            string cleanName = StripExe(processName);

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
            if (pid <= AppConfig.MaxReservedSystemPid || protectedPids.Contains(pid)) return true;

            return IsCriticalProcess(processName);
        }

        #endregion

        #region Public Interface: Child Termination

        /// <inheritdoc/>
        public void KillChildren(int parentPid)
        {
            int selfPid;
            using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

            DateTime parentStartTime = DateTime.MinValue;
            try
            {
                using (var parent = Process.GetProcessById(parentPid))
                {
                    parentStartTime = parent.StartTime;
                }
            }
            catch { /* parent already exited; first-level identity check is best-effort */ }

            // Step 1: Create a handle-less view of the entire system process table in a single pass
            var (snapshot, byParent) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();

            // SECURITY: Get ancestor PIDs to prevent killing the Servy process chain if it happens 
            // to be a descendant (e.g. through PID reuse or complex supervisor patterns)
            var protectedPids = GetAncestorPids(snapshot);

            // Step 2: Recursively terminate descendants using the map
            var visited = new HashSet<int>();
            WalkAndKillChildren(parentPid, parentStartTime, selfPid, protectedPids, byParent, visited);
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
                if (childPid == selfPid || childPid <= AppConfig.MaxReservedSystemPid) continue;

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

                        DateTime childStart = SafeStartTime(child);

                        // If we cannot establish the temporal bounds of either process,
                        // fail-safe by refusing to execute the kill to protect recycled system nodes.
                        if (parentStartTime == DateTime.MinValue || childStart == DateTime.MinValue)
                        {
                            Logger.Warn($"WalkAndKillChildren: Skipping PID {childPid}. Could not establish clear temporal identity for PID-reuse guard.");
                            continue;
                        }

                        // Identity check: Ensures we don't kill a process that recycled a PID 
                        // from a target that died before this operation started.
                        if (childStart < parentStartTime.AddSeconds(-AppConfig.PidReuseToleranceSeconds))
                            continue;

                        // Depth-First Recursion: Kill the leaves of the tree first.
                        // We pass the same visited set and protected PIDs down the stack.
                        WalkAndKillChildren(childPid, childStart, selfPid, protectedPids, byParent, visited);

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
                string normalizedName = StripExe(processName);

                int selfPid;
                using (var current = Process.GetCurrentProcess()) { selfPid = current.Id; }

                var (completeSnapshot, byParent) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();
                var protectedPids = GetAncestorPids(completeSnapshot);

                // Find target PIDs directly from the snapshot - no Process.GetProcesses() needed.
                var targetPids = new List<int>();
                var anyProtected = false;
                foreach (var kvp in completeSnapshot)
                {
                    string snapName = StripExe(kvp.Value.Name ?? string.Empty);

                    if (string.Equals(snapName, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (protectedPids.Contains(kvp.Key))
                        {
                            Logger.Warn($"Execution blocked: Attempted to kill protected process '{normalizedName}' (PID {kvp.Key}).");
                            anyProtected = true;
                            continue;
                        }
                        targetPids.Add(kvp.Key);
                    }
                }

                if (targetPids.Count == 0) return !anyProtected; // true only when genuinely nothing matched

                // Open handles ONLY for the small subset we plan to act on.
                var targets = new List<Process>();
                try
                {
                    foreach (var pid in targetPids)
                    {
                        try
                        {
                            targets.Add(Process.GetProcessById(pid));
                        }
                        catch (ArgumentException)
                        {
                            // Process exited before handle could be opened
                        }
                        catch (InvalidOperationException)
                        {
                            // Process exited before handle could be opened
                        }
                    }

                    // ROBUSTNESS: Perform the parent kill walk BEFORE the tree kill walk.
                    // This ensures that we query information while the target process handles are still valid.
                    if (killParents)
                    {
                        foreach (var proc in targets)
                        {
                            KillParentProcesses(proc.Id, SafeStartTime(proc), protectedPids, completeSnapshot, new HashSet<int>());
                        }
                    }

                    foreach (var proc in targets)
                    {
                        KillProcessTree(proc, selfPid, protectedPids, byParent);
                    }

                    return true;
                }
                finally
                {
                    foreach (var p in targets) p.Dispose();
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
                    // Aligned with the string overload to use SafeStartTime wrapper to prevent Win32Exception 
                    // or InvalidOperationException from skipping the subsequent KillProcessTree execution.
                    if (killParents) KillParentProcesses(target.Id, SafeStartTime(target), protectedPids, completeSnapshot, new HashSet<int>());
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
        /// Safely retrieves the start time of a process without throwing an exception if the process has already exited or access is denied.
        /// </summary>
        /// <param name="p">The <see cref="Process"/> instance to query.</param>
        /// <returns>
        /// The <see cref="DateTime"/> when the process started, or <see cref="DateTime.MinValue"/> if the start time could not be retrieved.
        /// </returns>
        private static DateTime SafeStartTime(Process p)
        {
            try
            {
                return p.StartTime;
            }
            catch
            {
                return DateTime.MinValue;
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

                // HARDENING: Resolve start time safely to prevent Win32Exception/InvalidOperationException from aborting the pipeline.
                // If query access is denied or the process is short-lived, it falls back to MinValue to ensure the tree kill continues.
                DateTime rootStartTime = DateTime.MinValue;
                try
                {
                    rootStartTime = process.StartTime;
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Unable to query StartTime for process {process.Id}. Proceeding with best-effort validation. Details: {ex.Message}");
                }

                // Thread protectedPids into the walk using the securely isolated timestamp
                WalkAndKillChildren(process.Id, rootStartTime, selfPid, protectedPids, byParent, visited);

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

                    // Using IsCriticalProcess ensures handle.exe output (with .exe) is correctly validated against the safelist
                    if (IsCriticalProcess(procInfo.ProcessName))
                    {
                        Logger.Warn($"Skipping kill request for critical system process: {procInfo.ProcessName} (PID {procInfo.ProcessId})");
                        success = false;
                        continue;
                    }

                    // Surgical Kill by PID
                    if (!KillProcessTreeAndParents(procInfo.ProcessId, killParents: false))
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
        /// Removes the ".exe" extension from the specified file name, if present.
        /// </summary>
        /// <param name="name">The file name or path string to process.</param>
        /// <returns>
        /// The file name without the ".exe" extension if it was present; 
        /// otherwise, the original string. Returns the input as-is if it is null or empty.
        /// </returns>
        private static string StripExe(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;
        }

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
            if (parentId <= AppConfig.MaxReservedSystemPid || protectedPids.Contains(parentId)) return;

            if (!snapshot.TryGetValue(parentId, out var parentNode)) return;

            // SECURITY: Use the name from the snapshot to check protection, avoiding access denied exceptions.
            if (IsProtected(parentId, parentNode.Name, protectedPids))
            {
                Logger.Debug($"Aborting parent kill walk: {parentNode.Name} (PID {parentId}) is protected.");
                return;
            }

            DateTime parentStartTime = DateTime.MinValue;
            Process? parentProcess = null;

            try
            {
                // Open the process handle exactly once to establish an unchangeable identity context
                parentProcess = Process.GetProcessById(parentId);
            }
            catch (ArgumentException)
            {
                // parent already dead - abort walk
                return;
            }

            try
            {
                try
                {
                    parentStartTime = parentProcess.StartTime;
                }
                catch (Exception)
                {
                    // keep MinValue if reading StartTime fails due to transient access limits
                }

                // ROBUSTNESS: Perform temporal identity validation BEFORE walking up the ancestor tree.
                // This isolates PID recycling exploits at the boundary and blocks the walk from leaking into unrelated trees.
                try
                {
                    // Verify the handle context directly using the cached or live process start time 
                    // to completely eliminate the PID-recycling exploit window.
                    DateTime exactStartTime = parentStartTime == DateTime.MinValue ? parentProcess.StartTime : parentStartTime;

                    // If temporal attributes are inaccessible (exactStartTime or childStartTime is MinValue),
                    // abort the upward walk immediately. This eliminates the "kill blindly" fallback loop.
                    if (childStartTime == DateTime.MinValue || exactStartTime == DateTime.MinValue)
                    {
                        Logger.Warn($"KillParentProcesses: Aborting upward tree walk at parent PID {parentId}. Incomplete temporal identity metrics.");
                        return;
                    }

                    if (exactStartTime > childStartTime.AddSeconds(AppConfig.PidReuseToleranceSeconds))
                    {
                        Logger.Debug($"Aborting parent tree walk: PID {parentId} has been recycled (started after child).");
                        return;
                    }

                    // Promote the verified, re-read live value to the outer tracking scope variable.
                    // This prevents passing DateTime.MinValue down into subsequent post-order traversals,
                    // ensuring grandparent and ancestor process contexts validate cleanly.
                    parentStartTime = exactStartTime;
                }
                catch (Win32Exception)
                {
                    // If a Win32 Access Denied exception is thrown here during direct live verification, 
                    // we must fail closed and abort immediately rather than risking blind tree termination.
                    Logger.Warn($"KillParentProcesses: Access Denied establishing live temporal identity for parent PID {parentId}. Aborting walk.");
                    return;
                }

                // Move up further first via post-order traversal - pass real anchor and the visited set
                // Safe to recurse now that the immediate parent identity context has been definitively verified.
                KillParentProcesses(parentId, parentStartTime, protectedPids, snapshot, visited);

                if (!parentProcess.HasExited)
                {
                    parentProcess.Kill();
                    if (!parentProcess.WaitForExit(SafeWait(AppConfig.KillParentWaitMs)))
                    {
                        Logger.Warn($"Parent process {parentProcess.Id} did not exit within the safety window.");
                    }
                }
            }
            catch (ArgumentException) { /* Already dead */ }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to kill parent {parentId}.", ex);
            }
            finally
            {
                // Ensure resource cleanup happens on all execution paths, regardless of sub-tree failures
                parentProcess?.Dispose();
            }
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
                    if (parentId <= AppConfig.MaxReservedSystemPid) break;
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