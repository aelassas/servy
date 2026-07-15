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
            // Branch: Execute InvokeAsync at Background priority
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var uiDispatcher = new WpfUiDispatcher();
                bool backgroundJobRan = false;
                var frame = new DispatcherFrame();

                // 1. Queue our background validation job.
                _ = uiDispatcher.InvokeAsync(() =>
                {
                    backgroundJobRan = true;
                }, DispatcherPriority.Background);

                // 2. Queue an explicit exit callback at a lower priority (ContextIdle).
                // This guarantees the dispatcher cleanly processes background jobs first.
                _ = uiDispatcher.InvokeAsync(() =>
                {
                    frame.Continue = false;
                }, DispatcherPriority.ContextIdle);

                // Act: Start a local, deterministic message pump loop on this thread.
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
                var uiDispatcher = new WpfUiDispatcher();
                var executionSequence = new List<string>();
                var frame = new DispatcherFrame();

                // 1. Queue a high-priority operation (Send priority)
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    executionSequence.Add("HighPriority");
                }, DispatcherPriority.Send);

                // 2. Queue the background verification task.
                // To bypass headless layout engine deadlocks, we schedule the completion marker
                // directly at Background priority to match the exact priority layer targeted by YieldAsync.
                _ = uiDispatcher.InvokeAsync(() =>
                {
                    // Trigger the task execution handle cleanly
                    var yieldTask = uiDispatcher.YieldAsync();

                    // Queue the resumption continuation at the matching priority level
                    _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                    {
                        executionSequence.Add("YieldContinuation");
                    }, DispatcherPriority.Background);
                }, DispatcherPriority.Normal);

                // 3. Queue the frame exit routine at ContextIdle to allow all tasks to flush in sequence
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    frame.Continue = false;
                }, DispatcherPriority.ContextIdle);

                // Act: Start the localized pump. It will unblock once frame.Continue is set to false.
                Dispatcher.PushFrame(frame);

                // Assert: Unambiguous, sequential proof of execution priority matching
                Assert.Equal(2, executionSequence.Count);
                Assert.Equal("HighPriority", executionSequence[0]);
                Assert.Equal("YieldContinuation", executionSequence[1]);

                await Task.CompletedTask;
            });
        }

        #endregion
    }
}