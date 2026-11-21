using Servy.Manager.Helpers;
using Servy.Testing;
using System;
using System.Threading;
using Xunit;
using Helper = Servy.Manager.Helpers.Helper;

namespace Servy.Manager.UnitTests.Helpers
{
    [Collection(AmbientTestCollection.Name)]
    public class HelperTests
    {
        [Fact]
        public void CancelAndDisposeSafely_NullCts_ReturnsImmediately()
        {
            // Arrange & Act & Assert
            // Should pass without throwing a NullReferenceException
            var exception = Record.Exception(() => Helper.CancelAndDisposeSafely(null));
            Assert.Null(exception);
        }

        [Fact]
        public void CancelAndDisposeSafely_ValidActiveCts_CancelsAndDisposesSuccessfully()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Act
            Helper.CancelAndDisposeSafely(cts);

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
            var exception = Record.Exception(() => Helper.CancelAndDisposeSafely(cts));
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
            Exception exception = null;

            // Act
            // The method should catch the AggregateException, log the warning, and force execution through the finally block
            var log = LogCapture.Run(() => { exception = Record.Exception(() => Helper.CancelAndDisposeSafely(cts)); });

            // Assert
            Assert.Null(exception); // Proves the exception was caught cleanly

            // Proves the finally block executed and disposed the token despite the broadcast error cascade
            Assert.Throws<ObjectDisposedException>(() => _ = cts.Token);

            // Proves the warning, the one signal an operator gets, was written with the callback's own failure
            Assert.Contains("CancellationTokenSource.Cancel callback threw; continuing with Dispose.", log);
            Assert.Contains("Malicious callback failure.", log);
        }
    }
}
