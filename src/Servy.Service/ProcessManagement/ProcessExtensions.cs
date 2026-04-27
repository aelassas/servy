using Servy.Core.Logging;
using Servy.Core.Native;
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
                // If the process has completely flushed from RAM, accessing .Id 
                // can sometimes throw a second InvalidOperationException. 
                // It's safer to catch everything here.
                try
                {
                    return $"({process.Id})";
                }
                catch
                {
                    return "(Exited Process)";
                }
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
        /// <remarks>
        /// <para>
        /// This method uses <c>CreateToolhelp32Snapshot</c> to bypass WMI dependencies, 
        /// ensuring reliable process enumeration even on hardened servers where WMI is disabled.
        /// </para>
        /// <para>
        /// <b>PID Reuse Protection:</b> Since Windows recycles PIDs, we verify that 
        /// <c>child.StartTime >= parentStartTime</c>. A 1-second buffer is subtracted from 
        /// the parent start time to account for OS clock tick precision.
        /// </para>
        /// </remarks>
        public static List<Process> GetChildren(int parentPid, DateTime parentStartTime)
        {
            var children = new List<Process>();

            if (parentPid <= 0 || parentStartTime == DateTime.MinValue)
                return children;

            // 1. Take a snapshot of all processes currently in the system.
            IntPtr hSnapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
            if (hSnapshot == NativeMethods.INVALID_HANDLE_VALUE)
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
                        try
                        {
                            // 3. Obtain the managed wrapper only for verified children
                            var child = Process.GetProcessById((int)pe32.th32ProcessID);

                            // Strict StartTime check to prevent PID reuse collisions
                            // We use >= and subtract 1 second to account for OS tick precision
                            if (child.StartTime >= parentStartTime.AddSeconds(-1))
                            {
                                children.Add(child);
                            }
                            else
                            {
                                child.Dispose(); // Not our child, dispose it immediately
                            }
                        }
                        catch (ArgumentException) { /* PID gone, expected */ }
                        catch (System.ComponentModel.Win32Exception) { /* Access denied, expected */ }
                        catch (Exception ex)
                        {
                            Logger.Debug($"Unexpected error while resolving child PID {pe32.th32ProcessID}: {ex.Message}");
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

            // Fetch Level 1 children using your existing Toolhelp32/WMI method
            var directChildren = GetChildren(parentPid, parentStartTime);

            foreach (var child in directChildren)
            {
                // 1. Add the current child to the flat list
                allDescendants.Add(child);

                try
                {
                    // Capture state for the next level of recursion
                    int childPid = child.Id;
                    DateTime childStartTime = child.StartTime;

                    // 2. Recursively fetch grandchildren
                    var deeperDescendants = GetAllDescendants(childPid, childStartTime);

                    // 3. Flatten the result into our main list
                    allDescendants.AddRange(deeperDescendants);
                }
                catch
                {
                    // If the child died between being captured and us reading its .Id/.StartTime,
                    // or if it threw Access Denied, we safely ignore the recursion. 
                    // The dead process remains in 'allDescendants' so the caller can properly Dispose() it.
                }
            }

            return allDescendants;
        }

    }
}