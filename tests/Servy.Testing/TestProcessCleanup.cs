using Servy.Service.Helpers;
using System.Diagnostics;

namespace Servy.Testing
{
    /// <summary>
    /// Provides unified helper functions for killing and disposing processes safely during test teardowns.
    /// </summary>
    public static class TestProcessCleanup
    {
        /// <summary>
        /// Forcefully terminates a process tree and disposes the process handle safely.
        /// </summary>
        /// <param name="process">The target process to kill and dispose.</param>
        /// <param name="waitMs">Timeout in milliseconds to wait for process exit.</param>
        public static void KillAndDispose(Process process, int waitMs = TestTimeouts.CleanupWaitMs)
        {
            if (process == null) return;

            try
            {
                if (!process.HasExited)
                {
                    ProcessHelper.KillProcessTree(process);
                    process.WaitForExit(waitMs);
                }
            }
            catch
            {
                // Swallowed: Safe teardown boundary for exited or inaccessible processes
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }
    }
}
