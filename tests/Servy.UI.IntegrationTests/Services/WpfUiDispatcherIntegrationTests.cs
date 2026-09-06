using Servy.Testing;
using Servy.UI.Services;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Servy.UI.IntegrationTests.Services
{
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
    }
}
