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

            // Shared retry policy for transient Windows file locks (AV scans, indexer, async streams)
            RetryDelete.Attempt(() => Directory.Delete(TempDirectory, recursive: true));
        }
    }
}
