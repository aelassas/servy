using System.IO;

namespace Servy.Testing
{
    /// <summary>
    /// Serves as a base class for tests requiring a uniquely isolated temporary directory fixture.
    /// Automatically provisions the filesystem context on instantiation and ensures a recursive, 
    /// cascading teardown cleanup pass occurs upon disposal.
    /// </summary>
    public abstract class TempDirectoryTestBase : IDisposable
    {
        private const int MaxRetryAttempts = 3;

        /// <summary>
        /// Gets the absolute filesystem path to the isolated temporary directory allocated for the current test context.
        /// </summary>
        protected string TempDirectory { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        /// <summary>
        /// Initializes a new instance of the <see cref="TempDirectoryTestBase"/> class 
        /// and physically constructs the isolated backing directory on disk.
        /// </summary>
        protected TempDirectoryTestBase() => Directory.CreateDirectory(TempDirectory);

        /// <summary>
        /// Disposes of the fixture context by recursively purging the managed temporary directory 
        /// and all nested filesystem items from disk.
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
                    Thread.Sleep(50);
                }
            }
        }
    }
}