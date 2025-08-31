using System;
using System.Diagnostics;

namespace Servy.Service.Helpers
{
    /// <summary>
    /// Helper methods for processes.
    /// </summary>
    public static class ProcessHelper
    {

        /// <summary>
        /// Recursively kills the specified process and all of its child processes.
        /// This method is intended for .NET Framework 4.8 where <c>Process.Kill(true)</c>
        /// is not available.
        /// </summary>
        /// <param name="process">The root process to terminate.</param>
        /// <remarks>
        /// Uses WMI (<c>Win32_Process</c>) to enumerate child processes by <c>ParentProcessId</c>.
        /// Children are killed first, followed by the parent process, to avoid leaving orphaned processes.
        /// Any exceptions (e.g., process already exited) are caught and ignored.
        /// </remarks>
        public static void KillProcessTree(Process process)
        {
            try
            {
                if (process == null || process.HasExited)
                    return;

                using (var searcher = new System.Management.ManagementObjectSearcher(
                           $"Select * From Win32_Process Where ParentProcessId={process.Id}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var childPid = Convert.ToInt32(obj["ProcessId"]);
                        try
                        {
                            using (var childProc = Process.GetProcessById(childPid))
                            {
                                KillProcessTree(childProc); // recursively kill children
                            }
                        }
                        catch
                        {
                            // child may have already exited
                        }
                    }
                }

                // Kill the main process last
                process.Kill();
                process.WaitForExit(30_000); // safety timeout
            }
            catch
            {
                // ignore errors (access denied, already exited, etc.)
            }
        }


    }
}
