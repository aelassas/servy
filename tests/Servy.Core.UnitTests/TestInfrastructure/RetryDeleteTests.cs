using Servy.Testing;
using System.IO;

namespace Servy.Core.UnitTests.TestInfrastructure
{
    /// <summary>
    /// Tests for <see cref="RetryDelete"/>, the single home of the retry-delete policy that
    /// <see cref="TempFile"/> and <see cref="TempDirectoryTestBase"/> used to hand-roll separately.
    /// </summary>
    public class RetryDeleteTests
    {
        [Fact]
        public void Attempt_DeleteSucceedsImmediately_InvokesTheDeleteExactlyOnce()
        {
            // Arrange
            int attempts = 0;

            // Act
            RetryDelete.Attempt(() => attempts++);

            // Assert
            Assert.Equal(1, attempts);
        }

        [Fact]
        public void Attempt_TransientFailuresThenSuccess_KeepsRetryingUntilTheDeleteSucceeds()
        {
            // Arrange
            int attempts = 0;

            // Act
            RetryDelete.Attempt(() =>
            {
                attempts++;
                if (attempts < RetryDelete.MaxRetryAttempts)
                {
                    throw new IOException("locked by another process");
                }
            });

            // Assert
            Assert.Equal(RetryDelete.MaxRetryAttempts, attempts);
        }

        [Fact]
        public void Attempt_EveryAttemptFailsTransiently_StopsAfterMaxRetryAttemptsAndSwallowsTheException()
        {
            // Arrange
            int attempts = 0;

            // Act
            var thrown = Record.Exception(() => RetryDelete.Attempt(() =>
            {
                attempts++;
                throw new IOException("locked by another process");
            }));

            // Assert
            Assert.Null(thrown);
            Assert.Equal(RetryDelete.MaxRetryAttempts, attempts);
        }

        [Fact]
        public void Attempt_UnauthorizedAccessIsTransientToo_IsRetriedAndSwallowedLikeAnIOException()
        {
            // Arrange
            int attempts = 0;

            // Act
            var thrown = Record.Exception(() => RetryDelete.Attempt(() =>
            {
                attempts++;
                throw new UnauthorizedAccessException("access denied");
            }));

            // Assert
            Assert.Null(thrown);
            Assert.Equal(RetryDelete.MaxRetryAttempts, attempts);
        }

        [Fact]
        public void Attempt_DeleteThrowsANonTransientException_PropagatesItWithoutRetrying()
        {
            // Arrange
            int attempts = 0;

            // Act
            var thrown = Assert.Throws<InvalidOperationException>(() => RetryDelete.Attempt(() =>
            {
                attempts++;
                throw new InvalidOperationException("not a lock");
            }));

            // Assert
            Assert.Equal("not a lock", thrown.Message);
            Assert.Equal(1, attempts);
        }
    }
}
