using Servy.Testing;
using Servy.UI.Services;
using System.Windows.Threading;

namespace Servy.UI.IntegrationTests.Services
{
    public class WpfUiDispatcherIntegrationTests
    {
        private readonly WpfUiDispatcher _uiDispatcher;

        public WpfUiDispatcherIntegrationTests()
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
                // Arrange
                var executionSequence = new List<string>();
                var lockObject = new object();

                // Queue a higher priority operation on the STA Dispatcher thread.
                // This operation should run and insert its tracking marker first because 
                // the YieldAsync background continuation yields execution down to a lower background priority.
                var highPriorityTask = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    lock (lockObject)
                    {
                        executionSequence.Add("HighPriority");
                    }
                }, DispatcherPriority.Send);

                // Start the yield task and append its verification sequence marker upon completion
                var yieldTask = _uiDispatcher.YieldAsync().ContinueWith(t =>
                {
                    lock (lockObject)
                    {
                        executionSequence.Add("YieldContinuation");
                    }
                });

                // Act
                await Task.WhenAll(highPriorityTask.Task, yieldTask);

                // Assert
                lock (lockObject)
                {
                    Assert.Equal(2, executionSequence.Count);
                    Assert.Equal("HighPriority", executionSequence[0]);
                    Assert.Equal("YieldContinuation", executionSequence[1]);
                }
            });
        }

        #endregion
    }
}