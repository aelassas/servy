using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;
using Servy.UI.Services;

namespace Servy.UI.UnitTests.Services
{
    public class CursorServiceTests
    {
        private readonly CursorService _service;

        public CursorServiceTests()
        {
            _service = new CursorService();
        }

        #region STA Thread Helper

       

        #endregion

        #region Branch: Headless / Null Dispatcher

        [Fact]
        public void SetWaitCursor_WhenApplicationIsNull_DoesNotThrow()
        {
            // Branch: if (Application.Current?.Dispatcher == null) return;
            // This is the default state in standard xUnit runners

            using (var cursor = _service.SetWaitCursor())
            {
                Assert.NotNull(cursor);
            }

            _service.ResetCursor();
            // Coverage: No exception occurred, and code returned early as expected.
        }

        #endregion

        #region Branch: Background Thread (Dispatcher.CheckAccess == false)

        [Fact]
        public async Task ResetCursor_FromBackgroundThread_InvokesOnDispatcher()
        {
            // This test requires an active STA thread pumping messages
            var tcs = new TaskCompletionSource<bool>();

            Helper.RunInSTA(() =>
            {
                EnsureApplicationContext();
                Mouse.OverrideCursor = Cursors.Hand;

                // Branch: else { Application.Current.Dispatcher.InvokeAsync(...) }
                // Call the service from a background thread while the STA thread pumps
                Task.Run(() =>
                {
                    _service.ResetCursor();
                    tcs.SetResult(true);
                });

                // Pump the dispatcher to allow the InvokeAsync call to execute
                DoEvents();
            });

            await tcs.Task;

            // Verification depends on the cursor state being reset after the dispatcher work
            Helper.RunInSTA(() =>
            {
                Assert.Null(Mouse.OverrideCursor);
            });
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Safely initializes Application.Current if it doesn't exist for the test context.
        /// </summary>
        private static void EnsureApplicationContext()
        {
            if (Application.Current == null)
            {
                new Application();
            }
        }

        /// <summary>
        /// Forces the dispatcher to process all pending messages, 
        /// including the InvokeAsync call from the service.
        /// </summary>
        private static void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(f =>
                {
                    ((DispatcherFrame)f).Continue = false;
                    return null;
                }), frame);
            Dispatcher.PushFrame(frame);
        }

        #endregion
    }
}