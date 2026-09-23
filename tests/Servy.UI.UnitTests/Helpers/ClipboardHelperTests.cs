using Servy.Core.Config;
using Servy.UI.Helpers;
using Servy.UI.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Servy.UI.UnitTests.Helpers
{
    /// <summary>
    /// Covers <see cref="ClipboardHelper.TrySetTextAsync"/>, the single home of the clipboard-write
    /// retry policy that <c>ServiceCommands.CopyPidAsync</c> and <c>ConsoleView.CopySelectedLinesAsync</c>
    /// used to hand-roll one copy each.
    /// </summary>
    public class ClipboardHelperTests
    {
        /// <summary>
        /// An <see cref="IUiDispatcher"/> that stands in for the STA marshalling without ever running
        /// the clipboard write, reporting a canned outcome per attempt and counting the attempts.
        /// </summary>
        private sealed class CountingDispatcher : IUiDispatcher
        {
            private readonly Func<int, bool> _outcome;

            /// <summary>
            /// Initializes a new instance of the <see cref="CountingDispatcher"/> class.
            /// </summary>
            /// <param name="outcome">
            /// Maps the zero-based attempt index to the result the clipboard write is to report.
            /// </param>
            public CountingDispatcher(Func<int, bool> outcome) => _outcome = outcome;

            /// <summary>
            /// Gets the number of clipboard writes the helper has marshalled so far.
            /// </summary>
            public int Invocations { get; private set; }

            /// <inheritdoc />
            public Task<T> InvokeAsync<T>(Func<T> callback)
            {
                // The callback is deliberately NOT invoked: it would reach the real system clipboard.
                var result = _outcome(Invocations);
                Invocations++;
                return Task.FromResult((T)(object)result);
            }

            /// <inheritdoc />
            public Task InvokeAsync(Action action) => throw new NotSupportedException();

            /// <inheritdoc />
            public Task InvokeAsync(Action action, DispatcherPriority priority) => throw new NotSupportedException();

            /// <inheritdoc />
            public Task YieldAsync() => throw new NotSupportedException();
        }

        [Fact]
        public async Task TrySetTextAsync_WriteSucceedsOnFirstAttempt_ReturnsTrueWithoutRetrying()
        {
            // Arrange
            var dispatcher = new CountingDispatcher(_ => true);

            // Act
            var result = await ClipboardHelper.TrySetTextAsync("payload", dispatcher, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(1, dispatcher.Invocations);
        }

        [Fact]
        public async Task TrySetTextAsync_WriteFailsEveryAttempt_ExhaustsConfiguredRetriesAndReturnsFalse()
        {
            // Arrange
            var dispatcher = new CountingDispatcher(_ => false);

            // Act
            var result = await ClipboardHelper.TrySetTextAsync("payload", dispatcher, CancellationToken.None);

            // Assert
            Assert.False(result);
            Assert.Equal(AppConfig.ClipboardComMaxRetries, dispatcher.Invocations);
        }

        [Fact]
        public async Task TrySetTextAsync_WriteSucceedsOnLastAttempt_ReturnsTrueAfterTheConfiguredRetries()
        {
            // Arrange
            var lastAttemptIndex = AppConfig.ClipboardComMaxRetries - 1;
            var dispatcher = new CountingDispatcher(attempt => attempt == lastAttemptIndex);

            // Act
            var result = await ClipboardHelper.TrySetTextAsync("payload", dispatcher, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(AppConfig.ClipboardComMaxRetries, dispatcher.Invocations);
        }

        [Fact]
        public async Task TrySetTextAsync_TokenCancelledBeforeTheRetryWait_PropagatesCancellation()
        {
            // Arrange
            var dispatcher = new CountingDispatcher(_ => false);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => ClipboardHelper.TrySetTextAsync("payload", dispatcher, cts.Token));

                // The first write is attempted before the wait that observes the token.
                Assert.Equal(1, dispatcher.Invocations);
            }
        }
    }
}
