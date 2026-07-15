using Servy.Testing;
using Servy.UI.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servy.UI.IntegrationTests.Services
{
    public class CursorServiceIntegrationTests
    {
        private readonly CursorService _service;

        public CursorServiceIntegrationTests()
        {
            _service = new CursorService();
        }

        #region Branch: Headless / Null Dispatcher

        [Fact]
        public void SetWaitCursorAndResetCursor_WhenApplicationIsNull_DoNotThrow()
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
            // Act and assert are wrapped in RunOnSTA to ensure we have a valid STA context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                Helper.EnsureApplication();
                Mouse.OverrideCursor = Cursors.Hand;

                // Act
                // The service detects we are on a background thread and marshals the call
                // to the dispatcher thread via Dispatcher.InvokeAsync
                await Task.Run(() =>
                {
                    _service.ResetCursor();
                });

                // Deterministically flush the Dispatcher message queue without blocking indefinitely on CI.
                // We push a nested frame and post a callback at Background priority to close it.
                // This forces the queue to pump everything prior to (and including) Background tasks, 
                // then exit safely without waiting for ApplicationIdle states.
                var frame = new DispatcherFrame();
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new SendOrPostCallback(state =>
                    {
                        ((DispatcherFrame)state!).Continue = false;
                    }),
                    frame);

                // Run the local pump on this thread until the background frame exit is processed.
                Dispatcher.PushFrame(frame);

                // Assert
                // Since the dispatcher queue was completely flushed, the cursor state check is now deterministic.
                Assert.Null(Mouse.OverrideCursor);

                await Task.CompletedTask;
            });
        }

        #endregion
    }
}