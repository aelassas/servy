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
                Mouse.OverrideCursor = Cursors.Hand;

                await Task.Run(() =>
                {
                    _service.ResetCursor();
                });

                // Force the Dispatcher to process the Reset operation
                // This flushes the queue up to 'Background' priority
                await Dispatcher.Yield(DispatcherPriority.Background);

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