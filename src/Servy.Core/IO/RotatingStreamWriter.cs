using Servy.Core.Enums;
using System.Globalization;
using System.Security.AccessControl;
using System.Text;

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
        private StreamWriter? _writer;
        private FileStream? _baseStream;
        private readonly bool _enableSizeRotation;
        private readonly long _rotationSizeInBytes;
        private readonly bool _enableDateRotation;
        private readonly DateRotationType _dateRotationType;
        private readonly bool _useLocalTimeForRotation;
        private DateTime _lastRotationDate;
        private readonly int _maxRotations; // 0 = unlimited
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RotatingStreamWriter"/> class.
        /// </summary>
        /// <param name="path">The path to the log file.</param>
        /// <param name="enableSizeRotation">
        /// Enables rotation when the log file exceeds the size specified
        /// in <paramref name="rotationSizeInBytes"/>.
        /// </param>
        /// <param name="rotationSizeInBytes">The maximum file size in bytes before rotating.</param>
        /// <param name="enableDateRotation">
        /// Enables rotation based on the date interval specified by <paramref name="dateRotationType"/>.
        /// </param>
        /// <param name="dateRotationType">
        /// Defines the date-based rotation schedule (daily, weekly, or monthly).
        /// Ignored when <paramref name="enableDateRotation"/> is <c>false</c>.
        /// </param>
        /// <param name="maxRotations">The maximum number of rotated log files to keep. Set to 0 for unlimited.</param>
        /// <param name="useLocalTimeForRotation">Indicates whether to use local system time for log rotation (Default: false (UTC)).</param>
        /// <remarks>
        /// When both size-based and date-based rotation are enabled,
        /// size rotation takes precedence. If a size-based rotation occurs,
        /// date-based rotation is skipped for that write.
        /// </remarks>
        public RotatingStreamWriter(
            string path,
            bool enableSizeRotation,
            long rotationSizeInBytes,
            bool enableDateRotation,
            DateRotationType dateRotationType,
            int maxRotations,
            bool useLocalTimeForRotation)
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
            _enableSizeRotation = enableSizeRotation;
            _rotationSizeInBytes = rotationSizeInBytes;
            _enableDateRotation = enableDateRotation;
            _dateRotationType = dateRotationType;
            var now = useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
            var lastWriteTime = useLocalTimeForRotation ? File.GetLastWriteTime(path) : File.GetLastWriteTimeUtc(path);
            _lastRotationDate = File.Exists(path) ? lastWriteTime : now; // baseline for date rotation
            _maxRotations = maxRotations;
            _useLocalTimeForRotation = useLocalTimeForRotation;
        }

        /// <summary>
        /// Initializes the underlying <see cref="FileStream"/> and <see cref="StreamWriter"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Uses <see cref="FileMode.Append"/> to ensure that the file pointer is always positioned 
        /// at the true end-of-file, preventing "NULL holes" during concurrent access or process restarts.
        /// </para>
        /// <para>
        /// The <see cref="StreamWriter"/> is configured with <see cref="StreamWriter.AutoFlush"/> enabled 
        /// and uses UTF-8 encoding without a Byte Order Mark (BOM) for compatibility with Unix-style log viewers.
        /// </para>
        /// </remarks>
        private void InitializeWriter()
        {
            // Native Win32 Atomic Append:
            // Explicitly using FileSystemRights.AppendData forces the Win32 kernel to drop FILE_WRITE_DATA.
            // The OS will now physically reject any write that doesn't go to the true EOF,
            // completely eliminating NULL holes even if an external process clears the file.
            _baseStream = FileSystemAclExtensions.Create(
                fileInfo: _file,
                mode: FileMode.Append,
                rights: FileSystemRights.AppendData, // The strict Win32 flag FileSystemRights.AppendData
                share: FileShare.ReadWrite,
                bufferSize: 4096,
                options: FileOptions.None,
                fileSecurity: null);

            _writer = new StreamWriter(_baseStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) // UTF-8 without BOM
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
                if (_disposed) return;

                // 1. Lazy Initialize if null (either first run or just rotated)
                if (_writer == null)
                {
                    InitializeWriter();
                }

                _writer!.WriteLine(line);
                // AutoFlush is true in InitializeWriter, but explicit flush ensures 
                // the FileInfo.Length is accurate for the next CheckRotation call.
                _writer!.Flush();

                // 2. Check if we need to rotate
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
                if (_disposed) return;

                // 1. Lazy Initialize if null (either first run or just rotated)
                if (_writer == null)
                {
                    InitializeWriter();
                }

                _writer!.Write(text);
                // AutoFlush is true in InitializeWriter, but explicit flush ensures 
                // the FileInfo.Length is accurate for the next CheckRotation call.
                _writer!.Flush();

                // 2. Check if we need to rotate
                CheckRotation();
            }
        }

        /// <summary>
        /// Determines whether a date-based rotation should occur
        /// based on the configured <see cref="DateRotationType"/>.
        /// </summary>
        private bool ShouldRotateByDate()
        {
            var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;

            switch (_dateRotationType)
            {
                case DateRotationType.Daily:
                    if (now.Date > _lastRotationDate.Date)
                    {
                        // If using local time, ensure at least 23 hours have passed to avoid DST duplicate rotations
                        if (_useLocalTimeForRotation)
                        {
                            return (now - _lastRotationDate).TotalHours >= 23;
                        }
                        return true;
                    }
                    return false;

                case DateRotationType.Weekly:
                    var lastWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        _lastRotationDate, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    var thisWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    return thisWeek != lastWeek || now.Year != _lastRotationDate.Year;

                case DateRotationType.Monthly:
                    return now.Month != _lastRotationDate.Month || now.Year != _lastRotationDate.Year;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether the current log file should be rotated,
        /// based on enabled rotation modes (size and/or date).
        /// </summary>
        private void CheckRotation()
        {
            // If writer is null, the file hasn't been created yet (Lazy Init).
            // There is nothing to rotate.
            if (_writer == null) return;

            _file.Refresh();
            if (!_file.Exists) return;

            long currentLength = _file.Length;

            bool rotateBySize = false;
            bool rotateByDate = false;

            // --- SIZE ROTATION ---
            if (_enableSizeRotation && _rotationSizeInBytes > 0 && currentLength >= _rotationSizeInBytes)
            {
                rotateBySize = true;
            }

            // If size rotation matches, rotate immediately and return
            if (rotateBySize)
            {
                Rotate();
                var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
                _lastRotationDate = now;
                return;
            }

            // --- DATE ROTATION ---
            if (_enableDateRotation)
            {
                rotateByDate = ShouldRotateByDate();
            }

            if (rotateByDate)
            {
                Rotate();
                _lastRotationDate = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Generates a unique file path by inserting a numeric suffix before the file extension if the file already exists.
        /// </summary>
        private static string GenerateUniqueFileName(string basePath)
        {
            if (!File.Exists(basePath))
                return basePath;

            string? directory = Path.GetDirectoryName(basePath);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException($"Cannot determine directory from path: {basePath}", nameof(basePath));
            string fileName = Path.GetFileName(basePath);

            string extension = Path.GetExtension(fileName);
            string namePart;

            bool isTimestamp = extension.Length == 16 &&
                               extension.StartsWith(".") &&
                               extension.Substring(1).All(c => char.IsDigit(c) || c == '_');

            if (string.IsNullOrEmpty(extension) || isTimestamp)
            {
                namePart = fileName;
                extension = "";
            }
            else
            {
                namePart = Path.GetFileNameWithoutExtension(fileName);
            }

            const int MaxRetryLimit = 10000;
            int count = 1;
            string newPath;

            do
            {
                // Safety bound: Prevent the process from hanging or causing high I/O latency 
                // if the directory is pathologically full or locked.
                if (count > MaxRetryLimit)
                {
                    throw new IOException(
                        $"Failed to generate a unique filename for '{basePath}' after {MaxRetryLimit} attempts. " +
                        "Please verify directory permissions or clean up orphaned files.");
                }

                newPath = Path.Combine(directory, $"{namePart}.({count}){extension}");
                count++;
            }
            while (File.Exists(newPath));

            return newPath;
        }

        /// <summary>
        /// Deletes older rotated log files to enforce the maximum rotation limit.
        /// </summary>
        private void EnforceMaxRotations()
        {
            if (_maxRotations <= 0)
                return;

            string directory = _file.Directory!.FullName;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(_file.FullName);
            string extension = Path.GetExtension(_file.FullName);

            var allPotentialFiles = Directory.GetFiles(directory, $"{fileNameWithoutExt}*");

            var rotatedFiles = allPotentialFiles
                .Where(f =>
                {
                    if (f.Equals(_file.FullName, StringComparison.OrdinalIgnoreCase))
                        return false;

                    string name = Path.GetFileName(f);

                    if (!name.StartsWith($"{fileNameWithoutExt}.", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return string.IsNullOrEmpty(extension) || name.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(File.GetLastWriteTime)
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
                    // Silently ignore to ensure logging resilience.
                }
            }
        }

        /// <summary>
        /// Rotates the current log file by inserting a local timestamp before the file extension.
        /// The caller must hold the lock.
        /// </summary>
        private void Rotate()
        {
            try
            {
                _file.Refresh();
                if (!_file.Exists || _file.Length == 0) return;

                CloseWriter();

                var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
                var timestamp = now.ToString("yyyyMMdd_HHmmss");

                var directory = Path.GetDirectoryName(_file.FullName)!;
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_file.FullName);
                var extension = Path.GetExtension(_file.FullName);

                var newFileName = $"{fileNameWithoutExt}.{timestamp}{extension}";
                var rotatedPath = Path.Combine(directory, newFileName);

                rotatedPath = GenerateUniqueFileName(rotatedPath);

                File.Move(_file.FullName, rotatedPath);

                EnforceMaxRotations();
            }
            catch
            {
                // Silently catch I/O errors (e.g., file locked). 
                // We will just overwrite/append to the existing file below on the next write.
            }

            // LAZY INIT FIX: We no longer eagerly call InitializeWriter() in a finally block here.
            // If the rotation succeeds, _writer remains null. 
            // The next time Write or WriteLine is called, the file will be cleanly recreated.
        }

        /// <summary>
        /// Gracefully closes and disposes of the <see cref="StreamWriter"/> and the underlying <see cref="FileStream"/>.
        /// </summary>
        private void CloseWriter()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
            if (_baseStream != null)
            {
                _baseStream.Dispose();
                _baseStream = null;
            }
        }

        /// <summary>
        /// Flushes the underlying <see cref="StreamWriter"/>, ensuring that all buffered
        /// data is written to the log file.
        /// </summary>
        public void Flush()
        {
            lock (_lock)
            {
                _writer?.Flush();

                if (_baseStream != null && _baseStream.Length > 0)
                {
                    _baseStream.Flush(true);
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
        protected virtual void Dispose(bool disposing)
        {
            lock (_lock) // Ensure Dispose doesn't race with Write/Rotate
            {
                if (_disposed) return;

                if (disposing)
                {
                    CloseWriter();
                }

                _disposed = true;
            }
        }
    }
}