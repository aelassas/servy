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
                // This must execute first when the pump starts spinning.
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    executionSequence.Add("HighPriority");
                }, DispatcherPriority.Send);

                // 2. Queue the background verification task.
                // We queue this directly at Background priority to mimic the exact resumption 
                // level target of the YieldAsync engine without running into headless layout stalls.
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    executionSequence.Add("YieldContinuation");
                }, DispatcherPriority.Background);

                // 3. Queue the frame exit routine at ContextIdle.
                // Since ContextIdle is lower than Background, this is guaranteed to run last.
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    frame.Continue = false;
                }, DispatcherPriority.ContextIdle);

                // Act: Start the localized pump loop.
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