using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servy.UI.Services
{
    /// <summary>
    /// WPF implementation of <see cref="ICursorService"/>.
    /// </summary>
    public class CursorService : ICursorService
    {
        /// <inheritdoc />
        public void SetWaitCursor() => SetCursorSafe(Cursors.Wait);

        /// <inheritdoc />
        public void ResetCursor() => SetCursorSafe(null);

        /// <summary>
        /// Safely sets the cursor, marshaling to the UI thread if necessary,
        /// and doing nothing when no <see cref="Application"/> is running.
        /// </summary>
        [ExcludeFromCodeCoverage] // Flaky on CI
        private static void SetCursorSafe(Cursor? cursor)
        {
            // Cache dispatcher once to prevent NRE if Application.Current becomes null during shutdown
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
            {
                Mouse.OverrideCursor = cursor;
            }
            else
            {
                dispatcher.InvokeAsync(() =>
                {
                    Mouse.OverrideCursor = cursor;
                }, DispatcherPriority.Normal);
            }
        }
    }
}
