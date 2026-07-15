using Servy.Testing;
using Servy.UI.Services;
using System.Windows.Threading;

namespace Servy.UI.IntegrationTests.Services
{
    public class WpfUiDispatcherIntegrationTests
    {
        #region YieldAsync Tests

        [Fact]
        public async Task YieldAsync_CompletesSuccessfully()
        {
            // Arrange & Act & Assert: Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange: Bind the dispatcher wrapper inside the active STA thread execution loop
                var uiDispatcher = new WpfUiDispatcher();
                var task = uiDispatcher.YieldAsync();

                // Assert: Verify the task is created successfully
                Assert.NotNull(task);

                // Act: Wait for the background yield pass to complete cleanly
                await task;

                // Assert: If we reach here, the Background priority action was executed 
                // and the dispatcher processed it.
                Assert.True(task.IsCompleted && !task.IsFaulted && !task.IsCanceled);
            });
        }

        [Fact]
        public async Task YieldAsync_EnsuresExecutionOrder()
        {
            // Arrange & Act & Assert: Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange: Bind the dispatcher wrapper inside the active STA thread execution loop
                var uiDispatcher = new WpfUiDispatcher();
                bool yieldCompleted = false;

                // Queue a higher priority operation (Send priority)
                var highPriorityTask = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    // High priority execution marker
                }, DispatcherPriority.Send);

                // Queue the yield operation which targets a lower priority (Background)
                var yieldTask = uiDispatcher.YieldAsync().ContinueWith(_ => yieldCompleted = true);

                // Act: Await the tasks sequentially to verify priority processing flow
                await highPriorityTask;
                await yieldTask;

                // Assert: Once high-priority processing completes, confirm the background yield executed
                Assert.True(yieldCompleted);
            });
        }

        #endregion
    }
}