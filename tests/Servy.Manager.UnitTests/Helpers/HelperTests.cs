using System;
using System.Threading;
using Xunit;

namespace Servy.Manager.UnitTests.Helpers
{
    public class HelperTests
    {
        [Fact]
        public void CancelAndDisposeSafely_NullCts_ReturnsImmediately()
        {
            // Arrange & Act & Assert
            // Should pass without throwing a NullReferenceException
            Manager.Helpers.Helper.CancelAndDisposeSafely(null);
        }

        [Fact]
        public void CancelAndDisposeSafely_ValidActiveCts_CancelsAndDisposesSuccessfully()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Act
            Manager.Helpers.Helper.CancelAndDisposeSafely(cts);

            // Assert
            Assert.True(cts.IsCancellationRequested);

            // Verifying disposal by ensuring subsequent access throws ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => _ = cts.Token);
        }

        [Fact]
        public void CancelAndDisposeSafely_OnCancel_CatchesObjectDisposedException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Dispose(); // Manually pre-dispose to force ObjectDisposedException when Cancel() executes

            // Act & Assert
            // The method should catch the ObjectDisposedException internally and return cleanly
            var exception = Record.Exception(() => Manager.Helpers.Helper.CancelAndDisposeSafely(cts));
            Assert.Null(exception);
        }

        [Fact]
        public void CancelAndDisposeSafely_OnCancel_CatchesAggregateExceptionAndProceedsToDispose()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Register a broken, malicious callback to the token.
            // When cts.Cancel() is triggered, this callback fires synchronously on the calling thread
            // causing Cancel() to wrap the internal error inside an AggregateException.
            cts.Token.Register(() => throw new InvalidOperationException("Malicious callback failure."));

            // Act
            // The method should catch the AggregateException, log the warning, and force execution through the finally block
            var exception = Record.Exception(() => Manager.Helpers.Helper.CancelAndDisposeSafely(cts));

            // Assert
            Assert.Null(exception); // Proves the exception was caught cleanly

            // Proves the finally block executed and disposed the token despite the broadcast error cascade
            Assert.Throws<ObjectDisposedException>(() => _ = cts.Token);
        }

        [Fact]
        public void CancelAndDisposeSafely_OnDispose_HandlesReentrantObjectDisposedException()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Force a race condition inside the cancellation broadcast chain:
            // When Cancel() begins executing, it runs our token callback, which disposes the CTS 
            // completely out from underneath the running code block before the finally block can even execute.
            cts.Token.Register(() => cts.Dispose());

            // Act & Assert
            // When the finally block runs try { cts.Dispose(); }, it will throw a re-entrant ObjectDisposedException.
            // Helper should catch it silently without leaking the error up the test execution framework pointer.
            var exception = Record.Exception(() => Manager.Helpers.Helper.CancelAndDisposeSafely(cts));
            Assert.Null(exception);
        }
    }
}