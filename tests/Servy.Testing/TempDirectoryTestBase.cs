using System.IO;

namespace Servy.Testing
{
    /// <summary>
    /// Base class for tests requiring an isolated temporary directory.
    /// Creates the directory on construction and attempts recursive deletion on disposal,
    /// retrying on transient file locks and leaving the directory in place if locks persist.
    /// </summary>
    public abstract class TempDirectoryTestBase : IDisposable
    {
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMs = 50;

        /// <summary>
        /// Gets the absolute filesystem path to the isolated temporary directory allocated for the current test.
        /// </summary>
        protected string TempDirectory { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        /// <summary>
        /// Initializes a new instance of the <see cref="TempDirectoryTestBase"/> class
        /// and creates the temporary directory on disk.
        /// </summary>
        protected TempDirectoryTestBase() => Directory.CreateDirectory(TempDirectory);

        /// <summary>
        /// Performs best-effort recursive deletion of the temporary directory,
        /// retrying on transient file locks and swallowing lingering lock exceptions after all attempts expire.
        /// </summary>
        public virtual void Dispose()
        {
            if (!Directory.Exists(TempDirectory))
                return;

            // Retry loop to handle transient Windows file locks (AV scans, indexer, async streams)
            for (int i = 0; i < MaxRetryAttempts; i++)
            {
                try
                {
                    Directory.Delete(TempDirectory, recursive: true);
                    return; // Cleaned up successfully
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (i == MaxRetryAttempts - 1)
                    {
                        // Final attempt failed due to persistent lock; allow orphan in %TEMP% rather than failing a passing test
                        break;
                    }
                    Thread.Sleep(RetryDelayMs);
                }
            }
        }
    }
}
