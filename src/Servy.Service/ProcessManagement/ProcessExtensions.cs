using Servy.Core.Native;
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
        /// Gets the child processes of the specified process.
        /// </summary>
        /// <param name="parentPid">Parent process PID.</param>
        /// <param name="parentStartTime">Parent process start time</param>
        /// <returns>Children.</returns>
        public static unsafe List<(Process Process, Handle Handle)> GetChildren(int parentPid, DateTime parentStartTime)
        {
            var children = new List<(Process Process, Handle Handle)>();

            if (parentPid <= 0 || parentStartTime == DateTime.MinValue)
                return children;

            foreach (var other in Process.GetProcesses())
            {
                Handle? handle = null;
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