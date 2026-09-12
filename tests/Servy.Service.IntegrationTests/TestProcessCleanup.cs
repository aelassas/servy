using Servy.Service.ProcessManagement;
using Servy.Testing;

namespace Servy.Service.IntegrationTests
{
    /// <summary>
    /// Provides unified helper functions for killing and disposing processes safely during test teardowns.
    /// </summary>
    public static class TestProcessCleanup
    {
        /// <summary>
        /// Forcefully terminates a process wrapper tree and disposes the wrapper handle safely.
        /// </summary>
        /// <param name="wrapper">The target process wrapper to kill and dispose.</param>
        /// <param name="waitMs">Timeout in milliseconds to wait for process exit.</param>
        public static void KillAndDispose(IProcessWrapper wrapper, int waitMs = TestTimeouts.CleanupWaitMs)
        {
            if (wrapper == null) return;

            try
            {
                if (!wrapper.HasExited)
                {
                    wrapper.Kill(entireProcessTree: true);
                    wrapper.WaitForExit(waitMs);
                }
            }
            catch
            {
                // Swallowed: Safe teardown boundary for exited or inaccessible process wrappers
            }
            finally
            {
                try { wrapper.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Forcefully terminates a process wrapper tree without disposing the wrapper, for the
        /// "kill now, let the enclosing <c>using</c> dispose the handle" shape used inside test bodies.
        /// </summary>
        /// <param name="wrapper">The target process wrapper to kill.</param>
        /// <param name="waitMs">Timeout in milliseconds to wait for process exit.</param>
        public static void KillNow(IProcessWrapper wrapper, int waitMs = TestTimeouts.CleanupWaitMs)
        {
            if (wrapper == null) return;

            try
            {
                if (!wrapper.HasExited)
                {
                    wrapper.Kill(entireProcessTree: true);
                    wrapper.WaitForExit(waitMs);
                }
            }
            catch
            {
                // Swallowed: Safe teardown boundary for exited or inaccessible process wrappers
            }
        }
    }
}
