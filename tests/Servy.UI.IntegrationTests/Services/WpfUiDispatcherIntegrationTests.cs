using Servy.Testing;
using Servy.UI.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

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
                // Arrange
                // Instantiating WpfUiDispatcher inside the STA block captures this thread's Dispatcher.
                var uiDispatcher = new WpfUiDispatcher();
                bool backgroundJobRan = false;
                var frame = new DispatcherFrame();

                // 1. Queue our background validation job.
                _ = uiDispatcher.InvokeAsync(() =>
                {
                    backgroundJobRan = true;
                }, DispatcherPriority.Background);

                // 2. Queue an explicit exit callback at a lower priority (ContextIdle).
                // This guarantees conhost drains the Background task queue entirely before exiting, 
                // completely bypassing headless rendering environment deadlocks.
                _ = uiDispatcher.InvokeAsync(() =>
                {
                    frame.Continue = false;
                }, DispatcherPriority.ContextIdle);

                // Act: Start a local, deterministic message pump loop on this thread.
                // It unblocks immediately once the ContextIdle operation clears.
                Dispatcher.PushFrame(frame);

                // Assert: Verify the Background item executed successfully
                Assert.True(backgroundJobRan, "The dispatcher did not pump queued Background messages before the frame exited.");

                await Task.CompletedTask;
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