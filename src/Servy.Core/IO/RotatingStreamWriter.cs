using Servy.Core.Enums;
using System;
using System.Globalization;
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
        private readonly bool _enableSizeRotation;
        private readonly long _rotationSize;
        private readonly bool _enableDateRotation;
        private readonly DateRotationType _dateRotationType;
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
            int maxRotations)
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
            _rotationSize = rotationSizeInBytes;
            _enableDateRotation = enableDateRotation;
            _dateRotationType = dateRotationType;
            _lastRotationDate = File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.Now; // baseline for date rotation
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
        /// Determines whether a date-based rotation should occur
        /// based on the configured <see cref="DateRotationType"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the current local date has crossed the rotation boundary
        /// (day, week, or month) since the last rotation; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Daily rotation triggers when the calendar date changes (local).
        /// </para>
        /// <para>
        /// Weekly rotation uses ISO week numbering (Monday as first day of week).
        /// </para>
        /// <para>
        /// Monthly rotation triggers when either the month or year differs.
        /// </para>
        /// </remarks>
        private bool ShouldRotateByDate()
        {
            var now = DateTime.Now;

            switch (_dateRotationType)
            {
                case DateRotationType.Daily:
                    return now.Date > _lastRotationDate.Date;

                case DateRotationType.Weekly:
                    var lastWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        _lastRotationDate, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    var thisWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    return thisWeek != lastWeek;

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
        /// <remarks>
        /// Rotation rules:
        /// <list type="number">
        /// <item>
        /// <description>
        /// If size rotation is enabled and the file exceeds the configured size,
        /// the file is rotated immediately and date-based rotation is skipped.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// If size rotation does not apply and date rotation is enabled,
        /// rotation occurs when the configured date interval has elapsed.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        private void CheckRotation()
        {
            _file.Refresh();

            bool rotateBySize = false;
            bool rotateByDate = false;

            // --- SIZE ROTATION ---
            if (_enableSizeRotation && _rotationSize > 0 && _file.Length >= _rotationSize)
            {
                rotateBySize = true;
            }

            // If size rotation matches, rotate immediately and return
            if (rotateBySize)
            {
                Rotate();
                _lastRotationDate = DateTime.Now;
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
                _lastRotationDate = DateTime.Now;
            }
        }

        /// <summary>
        /// Generates a unique file path by inserting a numeric suffix before the file extension if the file already exists.
        /// <para>
        /// Example (with extension): If "app.20260325.log" exists, it returns "app.20260325.(1).log".
        /// </para>
        /// <para>
        /// Example (no extension): If "app.20260325" exists, it returns "app.20260325.(1)".
        /// </para>
        /// </summary>
        /// <param name="basePath">The initial file path (potentially containing a timestamp) to check for existence.</param>
        /// <returns>A unique file path that does not currently exist on disk.</returns>
        private static string GenerateUniqueFileName(string basePath)
        {
            if (!File.Exists(basePath))
                return basePath;

            string directory = Path.GetDirectoryName(basePath);
            string fileName = Path.GetFileName(basePath);

            // Path.GetExtension gets everything from the LAST dot to the end
            string extension = Path.GetExtension(fileName); // e.g., ".log" or ".20260325_001611"
            string namePart;

            // A "Solid" check: 
            // If the extension is exactly the length of your timestamp (+1 for the dot)
            // and consists of digits/underscores, it's NOT a real file extension.
            // Timestamp: .yyyyMMdd_HHmmss (16 characters)
            bool isTimestamp = extension.Length == 16 &&
                               extension.StartsWith(".") &&
                               extension.Substring(1).All(c => char.IsDigit(c) || c == '_');

            if (string.IsNullOrEmpty(extension) || isTimestamp)
            {
                // Case: MyApplication_Output.20260325_001611
                // Treat the whole thing as the name, so we append .(1) at the very end.
                namePart = fileName;
                extension = "";
            }
            else
            {
                // Case: MyApplication_Output.20260325_001611.log
                // Treat .log as the extension, so .(1) goes before it.
                namePart = Path.GetFileNameWithoutExtension(fileName);
            }

            int count = 1;
            string newPath;
            do
            {
                // Result: namePart.(count).extension
                newPath = Path.Combine(directory, $"{namePart}.({count}){extension}");
                count++;
            }
            while (File.Exists(newPath));

            return newPath;
        }

        /// <summary>
        /// Deletes older rotated log files to enforce the maximum rotation limit.
        /// If <see cref="_maxRotations"/> is set to <c>0</c>, rotation cleanup is disabled.
        /// </summary>
        /// <remarks>
        /// Rotated files follow the pattern: {name}.{timestamp}.{ext} or {name}.{timestamp}.(n).{ext}
        /// This method never throws exceptions; deletion failures are silently ignored.
        /// </remarks>
        private void EnforceMaxRotations()
        {
            if (_maxRotations <= 0)
                return;

            string directory = _file.Directory.FullName;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(_file.FullName);
            string extension = Path.GetExtension(_file.FullName);

            // 1. Search for all files starting with the base name.
            // We use a broader glob because the timestamp is now injected before the extension.
            var allPotentialFiles = Directory.GetFiles(directory, $"{fileNameWithoutExt}*");

            var rotatedFiles = allPotentialFiles
                .Where(f =>
                {
                    // 2. Exclude the currently active log file
                    if (f.Equals(_file.FullName, StringComparison.OrdinalIgnoreCase))
                        return false;

                    // 3. Ensure it matches our specific rotated patterns:
                    // - MyApplication.20260325_120000.log
                    // - MyApplication.20260325_120000.(1).log
                    // - MyApplication.20260325_120000 (if no extension)
                    // - MyApplication.20260325_120000.(1)
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

            // Delete the oldest files (those beyond the max count)
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
        /// <para>
        /// Example: 'app.log' becomes 'app.20260325_001611.log'. 
        /// If no extension is present, the timestamp is appended to the end.
        /// </para>
        /// If a file with the generated name already exists, a numeric suffix is added (e.g., '.(1)') to ensure uniqueness.
        /// This method closes the current writer, renames the file, enforces retention policies, and initializes a new log file.
        /// </summary>
        private void Rotate()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // 1. Deconstruct the path
            var directory = Path.GetDirectoryName(_file.FullName);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_file.FullName);
            var extension = Path.GetExtension(_file.FullName); // Includes the dot, e.g., ".log"

            // 2. Reconstruct with the timestamp inside
            // If extension is empty, Path.Combine still works perfectly.
            var newFileName = $"{fileNameWithoutExt}.{timestamp}{extension}";
            var rotatedPath = Path.Combine(directory, newFileName);

            // 3. Generate unique rotated filename if it already exists
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
