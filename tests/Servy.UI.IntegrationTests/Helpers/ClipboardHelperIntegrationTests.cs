using Servy.Testing;
using Servy.UI.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UI.IntegrationTests.Helpers
{
    /// <summary>
    /// Covers the clipboard write itself, which <c>ClipboardHelperTests</c> deliberately never reaches:
    /// its stand-in dispatcher reports a canned outcome without running the callback, so the real
    /// <see cref="System.Windows.Clipboard"/> call inside <see cref="UI.Helpers.ClipboardHelper.TrySetTextAsync"/>
    /// and the direct, non-marshalled branch taken when no dispatcher is supplied are unpinned there.
    /// Both need a real STA thread, which <see cref="Helper.RunOnSTA(System.Func{System.Threading.Tasks.Task}, bool)"/> provides.
    /// </summary>
    [Collection(UiStaCollection.Name)]
    public class ClipboardHelperIntegrationTests
    {
        /// <summary>
        /// Verifies that the branch taken when no dispatcher is supplied - the one
        /// <c>ConsoleView.CopySelectedLinesAsync</c> uses in production - writes straight to the
        /// system clipboard from the calling STA thread.
        /// </summary>
        [Fact]
        public async Task TrySetTextAsync_NoDispatcher_WritesDirectlyToTheSystemClipboard()
        {
            // Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange: a payload unique to this run, so a stale clipboard value cannot pass the assertion
                var payload = "servy-clipboard-direct-" + Guid.NewGuid().ToString("N");

                // Act
                var result = await UI.Helpers.ClipboardHelper.TrySetTextAsync(payload, cancellationToken: CancellationToken.None);

                // Assert: the round trip proves the write reached the clipboard, not merely that the helper reported success
                Assert.True(result);
                Assert.Equal(payload, System.Windows.Clipboard.GetText());
            });
        }

        /// <summary>
        /// Verifies that the marshalled branch reaches the same system clipboard when a real
        /// <see cref="WpfUiDispatcher"/> is supplied, rather than only counting the attempts.
        /// </summary>
        [Fact]
        public async Task TrySetTextAsync_WithWpfUiDispatcher_MarshalsTheWriteOntoTheStaThread()
        {
            // Execute inside the active STA message loop thread context
            await Helper.RunOnSTA(async () =>
            {
                // Arrange: a payload unique to this run, so a stale clipboard value cannot pass the assertion
                var payload = "servy-clipboard-dispatched-" + Guid.NewGuid().ToString("N");
                var dispatcher = new WpfUiDispatcher();

                // Act
                var result = await UI.Helpers.ClipboardHelper.TrySetTextAsync(payload, dispatcher, CancellationToken.None);

                // Assert: the round trip proves the marshalled write reached the clipboard
                Assert.True(result);
                Assert.Equal(payload, System.Windows.Clipboard.GetText());
            });
        }
    }
}
