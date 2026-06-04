using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;
using Servy.UI.Services;
using Servy.Testing;

namespace Servy.UI.IntegrationTests.Services
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
            // This is the default state in standard xUnit runners because Application.Current is null.

            // Act
            var exception = Record.Exception(() =>
            {
                _service.SetWaitCursor();
                _service.ResetCursor();
            });

            // Assert
            Assert.Null(exception);
            // Coverage: This confirms the guard clause effectively prevented an 
            // ObjectDisposedException or NullReferenceException when the WPF context is missing.
        }

        #endregion

        #region Branch: Background Thread (Dispatcher.CheckAccess == false)

        [Fact]
        public async Task ResetCursor_FromBackgroundThread_InvokesOnDispatcher()
        {
            await Helper.RunInSTAContext(async () =>
            {
                EnsureApplicationContext();

                // 1. Force the initial state
                Mouse.OverrideCursor = Cursors.Hand;
                Assert.Equal(Cursors.Hand, Mouse.OverrideCursor);

                // 2. Execute on background thread
                await Task.Run(() =>
                {
                    _service.ResetCursor();
                });

                // 3. Robust Polling with Higher Priority
                int retries = 0;
                bool resetSuccessfully = false;

                while (retries < 60) // Increased retries for CI stability
                {
                    // Use 'Send' or 'Normal' priority instead of 'Background'.
                    // 'Background' priority is often starved in busy CI runners.
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Normal);

                    if (Mouse.OverrideCursor == null)
                    {
                        resetSuccessfully = true;
                        break;
                    }

                    await Task.Delay(20);
                    retries++;
                }

                // 4. Force explicit null if still held, but report the failure
                if (!resetSuccessfully)
                {
                    Mouse.OverrideCursor = null; // Clean up for other tests
                    Assert.Fail($"Expected cursor to be null, but it remained: {Mouse.OverrideCursor}");
                }
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