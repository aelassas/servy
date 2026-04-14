using Servy.Core.Native;
using System;
using System.Collections.Generic;
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
                return $"({process.Id})";
            }
        }

        /// <summary>
        /// Retrieves all child processes for the specified parent.
        /// </summary>
        /// <param name="parentPid">The PID of the parent.</param>
        /// <param name="parentStartTime">The start time of the parent for identity validation.</param>
        /// <returns>
        /// A list of tuples containing the <see cref="Process"/> and its <see cref="IntPtr"/> handle. 
        /// <br/><strong>CRITICAL:</strong> The caller assumes full ownership of the returned Process objects and must dispose of them to prevent native handle leaks.
        /// </returns>
        /// <remarks>
        /// This method filters the global process list using a parent PID and a 2-second start-time buffer 
        /// to mitigate PID reuse risks. 
        /// <para>
        /// <b>Ownership Transfer:</b> Processes that do not match the criteria are disposed of immediately 
        /// within this method. However, processes that are included in the return list are <b>NOT</b> disposed. 
        /// The caller is responsible for iterating the results and calling <c>Dispose()</c> on each 
        /// <see cref="Process"/> object.
        /// </para>
        /// </remarks>
        public static unsafe List<(Process Process, Handle Handle)> GetChildren(int parentPid, DateTime parentStartTime)
        {
            var children = new List<(Process Process, Handle Handle)>();

            if (parentPid <= 0 || parentStartTime == DateTime.MinValue)
                return children;

            foreach (var other in Process.GetProcesses())
            {
                Handle handle = null;
                bool addedToChildren = false;

                try
                {
                    // Capture the ID early. If the process has already exited, 
                    // this might throw, which is safely caught and handled.
                    int otherId = other.Id;

                    handle = NativeMethods.OpenProcess(
                        NativeMethods.ProcessAccess.QueryInformation,
                        false,
                        otherId // Use the cached ID safely
                    );

                    if (handle == null || handle.IsInvalid)
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
                        handle.DangerousGetHandle(), // Explicitly pass the internal IntPtr
                        NativeMethods.ProcessInfoClass.ProcessBasicInformation,
                        out var info,
                        sizeof(NativeMethods.ProcessBasicInformation)) != 0)
                    {
                        continue;
                    }

                    if ((int)info.InheritedFromUniqueProcessId == parentPid)
                    {
                        children.Add((other, handle));
                        addedToChildren = true;
                        continue;
                    }
                }
                catch
                {
                    // Ignore inaccessible processes or processes that threw during ID access.
                }
                finally
                {
                    // Dispose only if ownership was NOT transferred to the children list.
                    if (!addedToChildren)
                    {
                        other.Dispose();
                        handle?.Dispose();
                    }
                }
            }

            return children;
        }
    }
}