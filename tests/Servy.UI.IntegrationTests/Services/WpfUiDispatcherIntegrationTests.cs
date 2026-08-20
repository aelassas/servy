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

                // Queue a higher priority operation (Send priority)
                var highPriorityTask = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    executionOrder.Enqueue("HighPriority");
                }, DispatcherPriority.Send);

                // Queue the yield operation which targets a lower priority (Background)
                async Task RunYieldAsync()
                {
                    await uiDispatcher.YieldAsync();
                    executionOrder.Enqueue("YieldBackground");
                }

                var yieldTask = RunYieldAsync();

                // Act: Await the tasks concurrently so the dispatcher pump handles them by priority
                await Task.WhenAll(highPriorityTask.Task, yieldTask);

                // Assert: Verify that the queue tracks the correct prioritized processing flow
                var results = executionOrder.ToArray();

                Assert.Equal(2, results.Length);
                Assert.Equal("HighPriority", results[0]);
                Assert.Equal("YieldBackground", results[1]);
            });
        }

        #endregion
    }
}
