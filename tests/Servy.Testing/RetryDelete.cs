using System.IO;

namespace Servy.Testing
{
    /// <summary>
    /// Single home for the best-effort retry-delete policy shared by <see cref="TempFile"/> and
    /// <see cref="TempDirectoryTestBase"/>: transient Windows file locks (AV scans, indexer,
    /// async streams) are retried a fixed number of times and then given up on, so a lingering
    /// lock leaves an orphan under the temporary directory rather than failing a passing test.
    /// </summary>
    public static class RetryDelete
    {
        /// <summary>
        /// Number of delete attempts made before a persistent lock is given up on.
        /// </summary>
        public const int MaxRetryAttempts = 3;

        /// <summary>
        /// Delay between two consecutive delete attempts, in milliseconds.
        /// </summary>
        public const int RetryDelayMs = 50;

        /// <summary>
        /// Invokes <paramref name="delete"/> until it succeeds or <see cref="MaxRetryAttempts"/>
        /// attempts have been made, treating <see cref="IOException"/> and
        /// <see cref="UnauthorizedAccessException"/> as transient and waiting
        /// <see cref="RetryDelayMs"/> milliseconds between attempts. Any other exception is
        /// propagated to the caller.
        /// </summary>
        /// <param name="delete">The delete action to attempt.</param>
        public static void Attempt(Action delete)
        {
            for (int i = 0; i < MaxRetryAttempts; i++)
            {
                try
                {
                    delete();
                    return; // Cleaned up successfully
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (i == MaxRetryAttempts - 1)
                    {
                        // Final attempt failed due to a persistent lock; allow the orphan rather than failing a passing test
                        return;
                    }

                    Thread.Sleep(RetryDelayMs);
                }
            }
        }
    }
}
