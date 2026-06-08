using Servy.Testing;
using Servy.UI.Services;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;

namespace Servy.UI.IntegrationTests.Services
{
    #region xUnit Non-Parallel Collection Setup

    [CollectionDefinition("WpfCursorTests", DisableParallelization = true)]
    public class WpfCursorTestCollection : ICollectionFixture<object>
    {
        // Used exclusively to enforce global non-parallelization constraints across the suite
    }

    #endregion

    [Collection("WpfCursorTests")]
    public class CursorServiceTests
    {
        private readonly CursorService _service;

        public CursorServiceTests()
        {
            _service = new CursorService();
            // REMOVED: Thread-unsafe initialization mutations completely removed from MTA constructor
        }

        #region Branch: Headless / Null Dispatcher

        [Fact]
        public void SetWaitCursor_WhenApplicationIsNull_DoesNotThrow()
        {
            // Act
            var exception = Record.Exception(() =>
            {
                _service.SetWaitCursor();
                _service.ResetCursor();
            });

            // Assert
            Assert.Null(exception);
        }

        #endregion

        #region Branch: Background Thread (Dispatcher.CheckAccess == false)

        [Fact]
        public async Task SetCursorSafe_FromBackgroundThread_MarshalsToUiDispatcherAsynchronously()
        {
            await Helper.RunOnSTA(async () =>
            {
                // CRITICAL isolation sequence executing safely on the active STA thread
                ResetApplicationContext();
                EnsureApplicationContext();
                var currentDispatcher = Dispatcher.CurrentDispatcher;

                // Arrange - Establish an initial distinct cursor state
                Mouse.OverrideCursor = Cursors.Arrow;

                var waitCursorTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var resetCursorTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Act - 1. Invoke worker thread to set the wait cursor
                _ = Task.Run(async () =>
                {
                    _service.SetWaitCursor();
                    await currentDispatcher.InvokeAsync(() => waitCursorTcs.TrySetResult(true), DispatcherPriority.Background);
                });

                var frame = new DispatcherFrame();
                _ = waitCursorTcs.Task.ContinueWith(_ => currentDispatcher.InvokeAsync(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                // Assert Wait Cursor state
                Assert.Equal(Cursors.Wait, Mouse.OverrideCursor);

                // Act - 2. Invoke worker thread to reset the cursor
                _ = Task.Run(async () =>
                {
                    _service.ResetCursor();
                    await currentDispatcher.InvokeAsync(() => resetCursorTcs.TrySetResult(true), DispatcherPriority.Background);
                });

                var resetFrame = new DispatcherFrame();
                _ = resetCursorTcs.Task.ContinueWith(_ => currentDispatcher.InvokeAsync(() => resetFrame.Continue = false));
                Dispatcher.PushFrame(resetFrame);

                // Assert Reset state
                Assert.Null(Mouse.OverrideCursor);

                ResetApplicationContext();
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
        /// Forcefully tears down Application.Current using reflection to guarantee environment reset.
        /// Must be called strictly within an active STA thread context loop.
        /// </summary>
        private static void ResetApplicationContext()
        {
            try
            {
                Mouse.OverrideCursor = null;
            }
            catch (InvalidOperationException)
            {
                // Safe fallback guard rail if state drops out during asynchronous unwinding
            }

            if (Application.Current != null)
            {
                try
                {
                    typeof(Application)
                        .GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)
                        ?.SetValue(null, null);
                }
                catch
                {
                    // Fallback block if reflection mappings are locked under strict runtime security rules
                }
            }
        }

        #endregion
    }
}