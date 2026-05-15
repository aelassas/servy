using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

            // 1. Take a snapshot of all processes currently in the system.
            IntPtr hSnapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
            if (hSnapshot == NativeMethods.INVALID_HANDLE_VALUE || hSnapshot == IntPtr.Zero)
            {
                Logger.Error($"Failed to create Toolhelp32 snapshot. Win32 Error: {Marshal.GetLastWin32Error()}");
                return children;
            }

            try
            {
                var pe32 = new NativeMethods.PROCESSENTRY32();
                // We MUST set dwSize before calling Process32First, or the API will fail.
                pe32.dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.PROCESSENTRY32));

                if (!NativeMethods.Process32First(hSnapshot, ref pe32))
                    return children;

                // 2. Walk the process tree in memory (extremely fast)
                do
                {
                    if (pe32.th32ParentProcessID == parentPid)
                    {
                        Process? child = null;
                        try
                        {
                            // 3. Obtain the managed wrapper only for verified children
                            child = Process.GetProcessById((int)pe32.th32ProcessID);

                            // Strict StartTime check to prevent PID reuse collisions
                            // We use >= and subtract 1 second to account for OS tick precision
                            if (child.StartTime >= parentStartTime.AddSeconds(-AppConfig.PidReuseToleranceSeconds))
                            {
                                children.Add(child);
                                child = null; // ownership transferred to the list
                            }
                        }
                        catch (ArgumentException) { /* PID gone, expected */ }
                        catch (Win32Exception) { /* Access denied, expected */ }
                        catch (Exception ex)
                        {
                            Logger.Debug($"Unexpected error while resolving child PID {pe32.th32ProcessID}: {ex.Message}");
                        }
                        finally
                        {
                            child?.Dispose();
                        }
                    }
                } while (NativeMethods.Process32Next(hSnapshot, ref pe32));
            }
            finally
            {
                // Always close the native handle to prevent memory leaks
                NativeMethods.CloseHandle(hSnapshot);
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
                    Process? child = null;
                    try
                    {
                        child = Process.GetProcessById(childPid);

                        // Strict StartTime check to prevent PID reuse collisions
                        if (child.StartTime >= current.StartTime.AddSeconds(-AppConfig.PidReuseToleranceSeconds))
                        {
                            allDescendants.Add(child);

                            // Queue the validated child for the next level of BFS
                            queue.Enqueue((childPid, child.StartTime));
                            child = null; // ownership transferred to the list
                        }
                    }
                    catch (ArgumentException) { /* PID gone, expected */ }
                    catch (Win32Exception) { /* Access denied, expected */ }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Unexpected error while resolving descendant PID {childPid}: {ex.Message}");
                    }
                    finally
                    {
                        child?.Dispose();
                    }
                }
            }

            return allDescendants;
        }
    }
}