using System;
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
        /// <inheritdoc/>
        public IDisposable SetWaitCursor()
        {
            return new OverrideCursorDisposable(Cursors.Wait);
        }

        /// <inheritdoc/>
        public void ResetCursor()
        {
            SetCursorSafe(null);
        }

        /// <summary>
        /// Safely sets the cursor, marshaling to the UI thread if necessary, 
        /// and ignoring calls during headless unit tests.
        /// </summary>
        private static void SetCursorSafe(Cursor? cursor)
        {
            // Skip in unit test environments where Application.Current or its Dispatcher might be null
            if (Application.Current?.Dispatcher == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                Mouse.OverrideCursor = cursor;
            }
            else
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Mouse.OverrideCursor = cursor;
                }, DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// A nested helper class that implements IDisposable to automatically 
        /// reset the cursor when the using block scope ends.
        /// </summary>
        private class OverrideCursorDisposable : IDisposable
        {
            public OverrideCursorDisposable(Cursor cursor)
            {
                SetCursorSafe(cursor);
            }

            public void Dispose()
            {
                SetCursorSafe(null);
            }
        }
    }
}