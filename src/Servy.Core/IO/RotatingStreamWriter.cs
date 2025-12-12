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
        private DateTime _lastRotationDateUtc;
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
            _lastRotationDateUtc = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.UtcNow; // baseline for date rotation
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
        /// <c>true</c> if the current UTC date has crossed the rotation boundary
        /// (day, week, or month) since the last rotation; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Daily rotation triggers when the calendar date changes (UTC).
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
            var now = DateTime.UtcNow;

            switch (_dateRotationType)
            {
                case DateRotationType.Daily:
                    return now.Date > _lastRotationDateUtc.Date;

                case DateRotationType.Weekly:
                    var lastWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        _lastRotationDateUtc, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    var thisWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    return thisWeek != lastWeek;

                case DateRotationType.Monthly:
                    return now.Month != _lastRotationDateUtc.Month || now.Year != _lastRotationDateUtc.Year;

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
                _lastRotationDateUtc = DateTime.UtcNow;
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
                _lastRotationDateUtc = DateTime.UtcNow;
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

            // Rotated files follow: logfile.log.20251208_104539 or logfile.log.(1).20251208_104539 or
            // logfile.20251208_104539 or logfile.(1).20251208_104539
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
