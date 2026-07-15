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
                // Instantiating inside the STA block binds the dispatcher wrapper to the active thread context
                var uiDispatcher = new WpfUiDispatcher();
                var executionSequence = new List<string>();
                var frame = new DispatcherFrame();

                // 1. Queue a high-priority operation
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    executionSequence.Add("HighPriority");
                }, DispatcherPriority.Send);

                // 2. Queue the yield task validation operation
                _ = uiDispatcher.InvokeAsync(async () =>
                {
                    await uiDispatcher.YieldAsync();
                    executionSequence.Add("YieldContinuation");
                }, DispatcherPriority.Normal);

                // 3. Queue a final frame exit callback at ContextIdle to flush everything prior cleanly
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    frame.Continue = false;
                }, DispatcherPriority.ContextIdle);

                // Act: Run the localized message loop to process operations in priority order without hanging
                Dispatcher.PushFrame(frame);

                // Assert
                Assert.Equal(2, executionSequence.Count);
                Assert.Equal("HighPriority", executionSequence[0]);
                Assert.Equal("YieldContinuation", executionSequence[1]);

                await Task.CompletedTask;
            });
        }

        #endregion
    }
}