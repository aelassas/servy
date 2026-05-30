using Servy.Core.Logging;
using System;
using System.Threading;

namespace Servy.Manager.Helpers
{
    /// <summary>
    /// Provides global utility methods for the Servy Manager application.
    /// </summary>
    internal static class Helper
    {
        /// <summary>
        /// Safely attempts to cancel and dispose of a <see cref="CancellationTokenSource"/>, 
        /// suppressing common lifecycle exceptions to ensure process-wide shutdown integrity.
        /// </summary>
        internal static void CancelAndDisposeSafely(CancellationTokenSource cts)
        {
            if (cts == null) return;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException) { /* already disposed elsewhere */ }
            catch (AggregateException ex)
            {
                Logger.Warn("CancellationTokenSource.Cancel callback threw; continuing with Dispose.", ex);
            }
            finally
            {
                try { cts.Dispose(); } catch (ObjectDisposedException) { /* re-entrant */ }
            }
        }
    }
}
