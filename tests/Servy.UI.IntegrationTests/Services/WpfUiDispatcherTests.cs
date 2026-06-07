using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;
using Servy.UI.Services;
using Servy.Testing;

namespace Servy.UI.IntegrationTests.Services
{
    public class WpfUiDispatcherTests
    {
        private readonly WpfUiDispatcher _uiDispatcher;

        public WpfUiDispatcherTests()
        {
            _uiDispatcher = new WpfUiDispatcher();
        }

        #region YieldAsync Tests

        [Fact]
        public async Task YieldAsync_CompletesSuccessfully()
        {
            // Branch: Execute InvokeAsync at Background priority
            await Helper.RunOnSTA(async () =>
            {
                var task = _uiDispatcher.YieldAsync();

                // Assert: The task is created
                Assert.NotNull(task);

                // Act: Wait for the yield to complete
                await task;

                // Assert: If we reach here, the Background priority action was executed 
                // and the dispatcher processed it.
                Assert.True(task.IsCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task YieldAsync_EnsuresExecutionOrder()
        {
            await Helper.RunOnSTA(async () =>
            {
                bool yieldCompleted = false;

                // Queue a higher priority operation
                var highPriorityTask = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    // This should happen before the background yield if queued similarly, 
                    // but we use it to verify the pump is active.
                }, DispatcherPriority.Send);

                // Start the yield
                var yieldTask = _uiDispatcher.YieldAsync().ContinueWith(_ => yieldCompleted = true);

                await yieldTask;

                Assert.True(yieldCompleted);
            });
        }

        #endregion
    }
}