using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using System.ComponentModel;
using System.Diagnostics;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Format extensions for processes.
    /// </summary>
    public static class ProcessExtensions
    {
        /// <summary>
        /// Formats the process as "ProcessName (Id)".
        /// </summary>
        /// <param name="process">Process.</param>
        /// <returns>Process info.</returns>
        public static string Format(this Process process)
        {
            try
            {
                return $"{process.ProcessName} ({process.Id})";
            }
            catch (InvalidOperationException)
            {
                try { return $"({process.Id})"; } catch { return "(Exited Process)"; }
            }
            catch (Win32Exception)
            {
                try { return $"(PID {process.Id})"; } catch { return "(Inaccessible Process)"; }
            }
        }

        /// <summary>
        /// Retrieves all active child processes for a given parent process using the native Toolhelp32 API.
        /// </summary>
        /// <param name="parentPid">The Process ID of the parent.</param>
        /// <param name="parentStartTime">
        /// The <see cref="DateTime"/> the parent process started. Used to validate 
        /// that a child truly belongs to the current parent instance and not a 
        /// recycled PID from a previous process.
        /// </param>
        /// <returns>
        /// A <see cref="List{Process}"/> containing the child processes. 
        /// <br/><strong>Note:</strong> The caller assumes ownership of these objects and 
        /// must call <c>Dispose()</c> on each to prevent native handle leaks.
        /// </returns>
        public static List<Process> GetChildren(int parentPid, DateTime parentStartTime)
        {
            var children = new List<Process>();

            if (parentPid <= 0 || parentStartTime == DateTime.MinValue)
                return children;

            // 1. ONE snapshot, build parent->children map (centralized)
            var (_, byParent) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();

            // Anchor for PID-reuse detection. Any legitimate child MUST exist before this timestamp.
            var snapshotTime = DateTime.UtcNow;

            // 2. Retrieve verified direct children
            if (byParent.TryGetValue(parentPid, out var childrenPids))
            {
                foreach (int childPid in childrenPids)
                {
                    Process? validChild = TryResolveValidChild(childPid, parentStartTime, snapshotTime);
                    if (validChild != null)
                    {
                        children.Add(validChild);
                    }
                }
            }

            return children;
        }

        /// <summary>
        /// Recursively retrieves all descendants (children, grandchildren, etc.) of a given parent process.
        /// </summary>
        /// <param name="parentPid">The Process ID of the parent.</param>
        /// <param name="parentStartTime">The start time of the parent for PID reuse validation.</param>
        /// <returns>
        /// A flattened <see cref="List{Process}"/> containing the entire descendant tree.
        /// <br/><strong>Note:</strong> The caller assumes full ownership of ALL returned objects and 
        /// must call <c>Dispose()</c> on each to prevent native handle leaks.
        /// </returns>
        public static List<Process> GetAllDescendants(int parentPid, DateTime parentStartTime)
        {
            var allDescendants = new List<Process>();

            if (parentPid <= 0 || parentStartTime == DateTime.MinValue)
                return allDescendants;

            // 1. ONE snapshot, build parent->children map
            var (_, byParent) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();

            // Anchor for PID-reuse detection. Any legitimate child MUST exist before this timestamp.
            var snapshotTime = DateTime.UtcNow;

            // 2. BFS over the map, materialize Process objects only for verified descendants
            var queue = new Queue<(int Pid, DateTime StartTime)>();
            var visited = new HashSet<int>(); // Cycle protection

            queue.Enqueue((parentPid, parentStartTime));
            visited.Add(parentPid);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // If this PID has no children in the snapshot map, continue to the next
                if (!byParent.TryGetValue(current.Pid, out var childrenPids))
                    continue;

                // Only process the child if we haven't seen it before to short-circuit cycles
                foreach (int childPid in childrenPids.Where(visited.Add))
                {
                    Process? validChild = TryResolveValidChild(childPid, current.StartTime, snapshotTime);
                    if (validChild != null)
                    {
                        allDescendants.Add(validChild);

                        // Queue the validated child for the next level of BFS
                        queue.Enqueue((childPid, validChild.StartTime));
                    }
                }
            }

            return allDescendants;
        }

        /// <summary>
        /// Attempts to securely resolve a child process by PID, applying strict lifetime validation 
        /// to ensure the PID hasn't been recycled by an unrelated process.
        /// </summary>
        /// <param name="childPid">The process ID of the candidate child.</param>
        /// <param name="parentStartTime">The start time of the parent process at the current depth.</param>
        /// <param name="snapshotTime">The UTC timestamp of when the system process snapshot was taken.</param>
        /// <returns>A valid <see cref="Process"/> instance if validation passes; otherwise, <c>null</c>.</returns>
        private static Process? TryResolveValidChild(int childPid, DateTime parentStartTime, DateTime snapshotTime)
        {
            Process? child = null;
            try
            {
                child = Process.GetProcessById(childPid);
                var startUtc = child.StartTime.ToUniversalTime();

                // Legitimate child must:
                //   (a) have started no earlier than the current parent level, AND
                //   (b) have started no later than when we observed it in the snapshot.
                bool startedAfterParent = startUtc >= parentStartTime.ToUniversalTime().AddSeconds(-AppConfig.PidReuseToleranceSeconds);
                bool startedBeforeSnapshot = startUtc <= snapshotTime.AddSeconds(AppConfig.PidReuseToleranceSeconds);

                if (startedAfterParent && startedBeforeSnapshot)
                {
                    var validChild = child;
                    child = null; // ownership transferred safely to the caller
                    return validChild;
                }
            }
            catch (ArgumentException) { /* PID gone, expected */ }
            catch (Win32Exception) { /* Access denied, expected */ }
            catch (Exception ex)
            {
                Logger.Debug($"Unexpected error while resolving child PID {childPid}: {ex.Message}");
            }
            finally
            {
                // Disposes the handle if validation fails, the PID was rejected, or an exception occurred.
                child?.Dispose();
            }

            return null;
        }
    }
}