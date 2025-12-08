using System;
using System.IO;
using System.Linq;

namespace Servy.Core.IO
{
    /// <summary>
    /// Writes text to a file with automatic log rotation based on file size.
    /// When the file exceeds a specified size, it is renamed with a timestamp suffix,
    /// and a new log file is started.
    /// </summary>
    public class RotatingStreamWriter : IDisposable
    {
        private bool _disposed;
        private readonly FileInfo _file;
        private StreamWriter _writer;
        private readonly long _rotationSize;
        private readonly int _maxRotations; // 0 = unlimited
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RotatingStreamWriter"/> class.
        /// </summary>
        /// <param name="path">The path to the log file.</param>
        /// <param name="rotationSizeInBytes">The maximum file size in bytes before rotating.</param>
        /// <param name="maxRotations">The maximum number of rotated log files to keep. Set to 0 for unlimited.</param>
        public RotatingStreamWriter(string path, long rotationSizeInBytes, int maxRotations = 0)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            _file = new FileInfo(path);
            _rotationSize = rotationSizeInBytes;
            _maxRotations = maxRotations;
            _writer = CreateWriter();
        }

        /// <summary>
        /// Creates a new <see cref="StreamWriter"/> in append mode with read/write sharing.
        /// </summary>
        /// <returns>A new <see cref="StreamWriter"/> instance.</returns>
        private StreamWriter CreateWriter()
        {
            return new StreamWriter(
                _file.Open(FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false) // UTF-8 without BOM
                )
            {
                AutoFlush = true
            };
        }

        /// <summary>
        /// Writes a line to the log file and checks for rotation.
        /// </summary>
        /// <param name="line">The line of text to write.</param>
        public void WriteLine(string line)
        {
            lock (_lock)
            {
                _writer.WriteLine(line);
                _writer.Flush();
                CheckRotation();

            }
        }

        /// <summary>
        /// Writes text to the log file without adding a newline, checking for rotation.
        /// </summary>
        public void Write(string text)
        {
            lock (_lock)
            {
                _writer.Write(text);
                _writer.Flush();
                CheckRotation();
            }
        }

        /// <summary>
        /// Checks if the file exceeds rotation size and rotates if necessary.
        /// </summary>
        private void CheckRotation()
        {
            _file.Refresh();
            if (_rotationSize > 0 && _file.Length >= _rotationSize)
            {
                Rotate();
            }
        }

        /// <summary>
        /// Generates a unique file path by appending a numeric suffix if the file already exists.
        /// For example, if "log.txt" exists, it will try "log(1).txt", "log(2).txt", etc., until a free name is found.
        /// </summary>
        /// <param name="basePath">The initial file path to check.</param>
        /// <returns>A unique file path that does not exist yet.</returns>
        private static string GenerateUniqueFileName(string basePath)
        {
            if (!File.Exists(basePath))
                return basePath;

            string directory = Path.GetDirectoryName(basePath);

            string filenameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);

            int count = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{filenameWithoutExt}.({count}){extension}");
                count++;
            }
            while (File.Exists(newPath));

            return newPath;
        }

        /// <summary>
        /// Deletes older rotated log files to enforce the maximum rotation limit.
        /// If <see cref="_maxRotations"/> is set to <c>0</c>, rotation cleanup is disabled.
        /// Only files matching the rotated filename pattern of the current log file
        /// are considered. 
        /// </summary>
        /// <remarks>
        /// This method never throws exceptions. Any deletion failure is silently ignored
        /// to ensure logging remains fully resilient.
        /// </remarks>
        private void EnforceMaxRotations()
        {
            if (_maxRotations <= 0)
                return; // unlimited

            string directory = _file.Directory.FullName;
            string baseName = Path.GetFileName(_file.FullName);

            // Rotated files follow: logfile.log.20240101_120355 or logfile.log.(1).20240101_120355 or
            // logfile.20240101_120355 or logfile.(1).20240101_120355
            var rotatedFiles = Directory.GetFiles(directory, $"{baseName}.*")
                .Where(f => !f.Equals(_file.FullName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            if (rotatedFiles.Count <= _maxRotations)
                return;

            foreach (var file in rotatedFiles.Skip(_maxRotations))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Do NOT crash logging if deletion fails.
                    // Silently ignore. Logging must be resilient.
                }
            }
        }

        /// <summary>
        /// Rotates the current log file by renaming it with a timestamp suffix.
        /// If a file with the target name exists, a numeric suffix is appended to generate a unique filename.
        /// After rotation, a new log file is created.
        /// </summary>
        private void Rotate()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string rotatedPath = $"{_file.FullName}.{timestamp}";

            // Generate unique rotated filename if it already exists
            rotatedPath = GenerateUniqueFileName(rotatedPath);

            File.Move(_file.FullName, rotatedPath);

            // Enforce retention
            EnforceMaxRotations();

            // Recreate writer for new log file
            _writer = new StreamWriter(new FileStream(_file.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
        }

        /// <summary>
        /// Flushes the underlying <see cref="StreamWriter"/>, ensuring that all buffered
        /// data is written to the log file.
        /// </summary>
        /// <remarks>
        /// This method is thread-safe and can be called while the <see cref="RotatingStreamWriter"/>
        /// is in use. If the writer has been disposed, this method does nothing.
        /// </remarks>
        public void Flush()
        {
            lock (_lock)
            {
                if (_writer != null)
                {
                    _writer.Flush();
                }
            }
        }

        /// <summary>
        /// Public dispose method that clients call.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected virtual dispose method following the standard pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_lock)
                {
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Close();
                        _writer.Dispose();
                        _writer = null;
                    }
                }
            }

            _disposed = true;
        }

    }
}
