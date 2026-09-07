using System;
using System.IO;
using System.Threading;

namespace Servy.Testing
{
    /// <summary>
    /// Owns a single temporary file for the duration of a test and deletes it on disposal,
    /// retrying on transient file locks exactly as <see cref="TempDirectoryTestBase"/> does.
    /// Unlike <see cref="Path.GetTempFileName"/> nothing is created on disk until the content
    /// is written, so there is no base <c>.tmp</c> artifact left behind.
    /// </summary>
    public sealed class TempFile : IDisposable
    {
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMs = 50;

        /// <summary>
        /// Gets the absolute path of the temporary file.
        /// The file itself exists only once something has written to it.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TempFile"/> class for a uniquely named file
        /// carrying the requested suffix, under the system temporary directory.
        /// </summary>
        /// <param name="suffix">Suffix to append to the generated name, leading dot included (for example <c>".json"</c>).</param>
        public TempFile(string suffix)
            : this(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + suffix)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TempFile"/> class for a file that has to live
        /// in a specific directory rather than the system temporary directory.
        /// </summary>
        /// <param name="directory">Directory to place the file in.</param>
        /// <param name="fileName">File name to use, extension included.</param>
        public TempFile(string directory, string fileName) => Path = System.IO.Path.Combine(directory, fileName);

        /// <summary>
        /// Writes <paramref name="content"/> to the file, creating it.
        /// </summary>
        /// <param name="content">Content to write.</param>
        /// <returns>This instance, so the call can be chained into a <c>using</c> statement.</returns>
        public TempFile Write(string content)
        {
            File.WriteAllText(Path, content);
            return this;
        }

        /// <summary>
        /// Performs best-effort deletion of the file, retrying on transient locks
        /// and leaving the file in place if the locks persist.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < MaxRetryAttempts; i++)
            {
                try
                {
                    if (File.Exists(Path))
                    {
                        File.Delete(Path);
                    }

                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (i == MaxRetryAttempts - 1)
                    {
                        return;
                    }

                    Thread.Sleep(RetryDelayMs);
                }
            }
        }
    }
}
