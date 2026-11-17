using Servy.Testing;
using Servy.UI.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace Servy.UI.IntegrationTests.Services
{
    [Collection(UiStaCollection.Name)]
    public class WpfUiDispatcherIntegrationTests
    {
        #region YieldAsync Tests

        [Fact]
        public async Task YieldAsync_CompletesSuccessfully()
        {
            // Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var uiDispatcher = new WpfUiDispatcher();

                // Act: Record any exception thrown while yielding to the dispatcher message pump
                var exception = await Record.ExceptionAsync(async () => await uiDispatcher.YieldAsync());

                // Assert: The absence of an exception proves that YieldAsync completed cleanly on the active STA thread
                Assert.Null(exception);
            });
        }

        [Fact]
        public async Task YieldAsync_EnsuresExecutionOrder()
        {
            // Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange: Bind the dispatcher wrapper inside the active STA thread execution loop
                var uiDispatcher = new WpfUiDispatcher();
                var executionOrder = new System.Collections.Concurrent.ConcurrentQueue<string>();

                // Queue the yield operation FIRST, at its documented Background priority, so the
                // asserted sequence is reachable only through genuine priority ordering and not
                // through the dispatcher's FIFO handling of equal priorities.
                async Task RunYieldAsync()
                {
                    await uiDispatcher.YieldAsync();
                    executionOrder.Enqueue("YieldBackground");
                }

                var yieldTask = RunYieldAsync();

                // Then queue Input, the priority directly above Background, and Send, the highest
                // of all. Both must still run first, which pins the yield strictly below Input.
                var inputTask = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    executionOrder.Enqueue("Input");
                }, DispatcherPriority.Input);

                var highPriorityTask = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    executionOrder.Enqueue("HighPriority");
                }, DispatcherPriority.Send);

                // Act: Await the tasks concurrently so the dispatcher pump handles them by priority
                await Task.WhenAll(highPriorityTask.Task, inputTask.Task, yieldTask);

                // Assert: Verify that the queue tracks the correct prioritized processing flow
                var results = executionOrder.ToArray();

                Assert.Equal(3, results.Length);
                Assert.Equal("HighPriority", results[0]);
                Assert.Equal("Input", results[1]);
                Assert.Equal("YieldBackground", results[2]);
            });
        }

        #endregion

        #region InvokeAsync Tests

        [Fact]
        public async Task InvokeAsync_Action_RunsDelegateOnTheApplicationDispatcherThreadBeforeCompleting()
        {
            // Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                // createApp makes the precondition explicit instead of leaving it to test order: with an
                // Application present the wrapper captures Application.Current.Dispatcher, as it does in
                // the running app, and that dispatcher lives on its own thread, not on this STA thread.
                var uiDispatcher = new WpfUiDispatcher();
                int appThreadId = Application.Current.Dispatcher.Thread.ManagedThreadId;
                int callerThreadId = Thread.CurrentThread.ManagedThreadId;
                int observedThreadId = -1;
                bool ranBeforeCompletion = false;

                // Act
                var invokeTask = uiDispatcher.InvokeAsync(() =>
                {
                    observedThreadId = Thread.CurrentThread.ManagedThreadId;
                });

                await invokeTask;
                ranBeforeCompletion = observedThreadId != -1;

                // Assert: the delegate ran before the task completed, on the application's dispatcher
                // thread, which is a real cross-thread marshal from the caller
                Assert.True(ranBeforeCompletion);
                Assert.Equal(appThreadId, observedThreadId);
                Assert.NotEqual(callerThreadId, observedThreadId);
            }, createApp: true);
        }

        [Fact]
        public async Task InvokeAsync_ActionWithPriority_HonoursTheRequestedPriority()
        {
            // Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var uiDispatcher = new WpfUiDispatcher();
                var executionOrder = new System.Collections.Concurrent.ConcurrentQueue<string>();

                Task backgroundTask = Task.CompletedTask;
                Task sendTask = Task.CompletedTask;

                // Act
                // Queue both from inside one callback on the captured dispatcher. It cannot start either
                // of them until that callback returns, so both are pending together whichever thread owns
                // the dispatcher. The low-priority work is still queued FIRST, so the asserted sequence is
                // reachable only through genuine priority ordering and not through FIFO handling.
                await uiDispatcher.InvokeAsync(() =>
                {
                    backgroundTask = uiDispatcher.InvokeAsync(
                        () => executionOrder.Enqueue("Background"),
                        DispatcherPriority.Background);

                    sendTask = uiDispatcher.InvokeAsync(
                        () => executionOrder.Enqueue("Send"),
                        DispatcherPriority.Send);
                });

                await Task.WhenAll(backgroundTask, sendTask);

                // Assert
                var results = executionOrder.ToArray();

                Assert.Equal(2, results.Length);
                Assert.Equal("Send", results[0]);
                Assert.Equal("Background", results[1]);
            }, createApp: true);
        }

        [Fact]
        public async Task InvokeAsync_Generic_ReturnsTheCallbackResult()
        {
            // Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange: this is the overload MessageBoxService's non-headless path carries the
                // user's Yes/No answer back through
                var uiDispatcher = new WpfUiDispatcher();
                const string sentinel = "marshalled-result";

                // Act
                var result = await uiDispatcher.InvokeAsync(() => sentinel);

                // Assert
                Assert.Equal(sentinel, result);
            });
        }

        #endregion
    }
}
