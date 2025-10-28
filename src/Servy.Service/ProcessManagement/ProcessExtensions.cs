using Servy.Service.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

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
                return $"({process.Id})";
            }
        }

        /// <summary>
        /// Gets the child processes of the specified process.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <returns>Children.</returns>
        public static unsafe List<(Process Process, Handle Handle)> GetChildren(this Process process)
        {
            var children = new List<(Process Process, Handle Handle)>();
            int parentPid = process.Id;
            DateTime parentStartTime;

            try
            {
                parentStartTime = process.StartTime;
            }
            catch (Exception ex)
            {
                // Could not get start time, return empty
                Debug.WriteLine($"Failed to get StartTime of parent PID {parentPid}: {ex.Message}");
                return children;
            }

            foreach (var other in Process.GetProcesses())
            {
                Handle handle = new Handle(IntPtr.Zero);
                try
                {
                    handle = NativeMethods.OpenProcess(
                        NativeMethods.ProcessAccess.QueryInformation,
                        false,
                        other.Id
                    );

                    if (handle == IntPtr.Zero)
                        continue;

                    // Skip processes that started before parent
                    try
                    {
                        if (other.StartTime <= parentStartTime)
                            continue;
                    }
                    catch
                    {
                        // Process may have exited, ignore
                        continue;
                    }

                    if (NativeMethods.NtQueryInformationProcess(
                        handle,
                        NativeMethods.ProcessInfoClass.ProcessBasicInformation,
                        out var info,
                        sizeof(NativeMethods.ProcessBasicInformation)) != 0)
                    {
                        continue;
                    }

                    if ((int)info.InheritedFromUniqueProcessId == parentPid)
                    {
                        children.Add((other, handle));
                        continue;
                    }
                }
                catch
                {
                    // Ignore inaccessible processes
                    other.Dispose();
                }
                finally
                {
                    // Dispose only if not already added to children
                    if (!children.Exists(c => c.Process.Id == other.Id))
                    {
                        other.Dispose();
                        handle.Dispose();
                    }
                }
            }

            return children;
        }

        /// <summary>
        /// Retrieves the child processes of the specified <see cref="Process"/> using WMI (Windows Management Instrumentation).
        /// </summary>
        /// <param name="process">The parent <see cref="Process"/> whose child processes are to be retrieved.</param>
        /// <returns>
        /// A <see cref="List{Process}"/> containing all currently running child processes of the specified parent process. 
        /// Processes that have exited during the query are ignored.
        /// </returns>
        /// <remarks>
        /// This method uses WMI to query the <c>Win32_Process</c> class and filter by <c>ParentProcessId</c>. 
        /// It is more robust than querying native process handles because it can detect detached or GUI processes.
        /// </remarks>
        public static List<Process> GetChildrenWmi(this Process process)
        {
            var children = new List<Process>();
            int parentPid = process.Id;

            try
            {
                string query = $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentPid}";
                using (var searcher = new ManagementObjectSearcher(query))
                using (var results = searcher.Get())
                {
                    foreach (var mo in results)
                    {
                        try
                        {
                            var pidObj = mo["ProcessId"];
                            if (pidObj == null)
                                continue;

                            int pid = Convert.ToInt32(pidObj);
                            var child = Process.GetProcessById(pid);
                            children.Add(child);
                        }
                        catch
                        {
                            // Process may have exited, ignore
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI GetChildren failed for PID {parentPid}: {ex.Message}");
            }

            return children;
        }
    }
}
