using Servy.Core.Config;
using Servy.UI.Services;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Servy.UI.Helpers
{
    /// <summary>
    /// Owns the single clipboard-write retry policy shared by every caller that copies text
    /// to the system clipboard, so the retry count, the delay between attempts and the set of
    /// clipboard failures treated as transient live in exactly one place.
    /// </summary>
    public static class ClipboardHelper
    {
        /// <summary>
        /// Attempts to place <paramref name="text"/> on the system clipboard, retrying transient
        /// Win32 clipboard failures up to <see cref="AppConfig.ClipboardComMaxRetries"/> times and
        /// waiting <see cref="AppConfig.ClipboardComRetryDelayMs"/> milliseconds between attempts.
        /// </summary>
        /// <param name="text">The text to place on the clipboard.</param>
        /// <param name="dispatcher">
        /// The UI dispatcher used to marshal each write onto the STA thread, for callers that may
        /// run off it; <see langword="null"/> for a caller that already runs on the UI thread and
        /// therefore writes directly.
        /// </param>
        /// <param name="cancellationToken">A token that cancels the wait between attempts.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that completes with <see langword="true"/> once the text
        /// reached the clipboard, or <see langword="false"/> when every attempt failed.
        /// </returns>
        /// <remarks>
        /// Reporting exhaustion is left to the caller: each one logs and notifies in its own terms.
        /// </remarks>
        public static async Task<bool> TrySetTextAsync(
            string text,
            IUiDispatcher dispatcher = null,
            CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < AppConfig.ClipboardComMaxRetries; i++)
            {
                // Accessing the Clipboard requires the STA thread (UI thread). Only the granular
                // write is invoked on the dispatcher, so the wait below never blocks it.
                bool success = dispatcher == null
                    ? TrySetTextCore(text)
                    : await dispatcher.InvokeAsync(() => TrySetTextCore(text));

                if (success)
                {
                    return true;
                }

                // If we failed, wait asynchronously before trying again.
                // This allows the UI thread to remain responsive during the wait.
                if (i < AppConfig.ClipboardComMaxRetries - 1)
                {
                    await Task.Delay(AppConfig.ClipboardComRetryDelayMs, cancellationToken);
                }
            }

            return false;
        }

        /// <summary>
        /// Performs one clipboard write attempt, reporting a transient Win32 clipboard failure
        /// as <see langword="false"/> rather than letting it escape.
        /// </summary>
        /// <param name="text">The text to place on the clipboard.</param>
        /// <returns><see langword="true"/> when the write succeeded; otherwise <see langword="false"/>.</returns>
        private static bool TrySetTextCore(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (ExternalException)
            {
                // COMException (clipboard locked by another process) or any other Win32 clipboard
                // failure: non-fatal, retry after the configured delay.
                return false;
            }
        }
    }
}
