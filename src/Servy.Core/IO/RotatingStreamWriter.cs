using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Servy.Core.IO
{
    /// <summary>
    /// Writes text to a file with automatic log rotation based on file size.
    /// When the file exceeds a specified size, it is renamed with a timestamp suffix,
    /// and a new log file is started.
    /// </summary>
    public class RotatingStreamWriter : IDisposable
    {
        private static readonly Regex _rotatedTimestampRegex = new Regex(@"^\d{8}_\d{6}(?:\.\(\d+\))?$", RegexOptions.Compiled, AppConfig.InputRegexTimeout);

        private bool _disposed;
        private readonly FileInfo _file;
        private StreamWriter _writer;
        private FileStream _baseStream;
        private readonly bool _enableSizeRotation;
        private readonly long _rotationSizeInBytes;
        private readonly bool _enableDateRotation;
        private readonly DateRotationType _dateRotationType;
        private readonly bool _useLocalTimeForRotation;
        private DateTime _lastRotationDate;
        private readonly int _maxRotations; // 0 = unlimited
        private int _consecutiveDeletionFailures;
        private readonly object _lock = new object();
        private readonly Func<DateTime> _timeProvider;

        /// <summary>
        /// A circuit-breaker flag that disables all rotation logic if a permanent failure occurs.
        /// This prevents infinite loops of failed moves that spike CPU usage.
        /// </summary>
        private bool _rotationDisabled;

        // --- Cooldown and fast-fail constraints ---
        private DateTime _rotationCooldownUntil = DateTime.MinValue;
        private const int RotationCooldownMs = 1000;
        private const int MaxSyncRotationRetries = 3;
        private const int SyncRotationRetryDelayMs = 50;

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
        /// <param name="timeProvider">The function to provide the current time. Defaults to system clock based on <paramref name="useLocalTimeForRotation"/>.</param>
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
            bool useLocalTimeForRotation,
            Func<DateTime> timeProvider = null)
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
            _useLocalTimeForRotation = useLocalTimeForRotation;
            // Initialize the time provider. Defaults to the system clock based on configuration.
            _timeProvider = timeProvider ?? (() => _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow);

            var now = _timeProvider();
            var lastWriteTime = useLocalTimeForRotation ? File.GetLastWriteTime(path) : File.GetLastWriteTimeUtc(path);
            _lastRotationDate = File.Exists(path) ? lastWriteTime : now; // baseline for date rotation
            _maxRotations = maxRotations;
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
            _baseStream = new FileStream(
                _file.FullName,
                FileMode.Append,
                FileSystemRights.AppendData, // The strict Win32 flag
                FileShare.ReadWrite,
                4096,
                FileOptions.None);

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

                _writer.WriteLine(line);
                // AutoFlush is true in InitializeWriter, but explicit flush ensures 
                // the FileInfo.Length is accurate for the next CheckRotation call.
                _writer.Flush();

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

                _writer.Write(text);
                // AutoFlush is true in InitializeWriter, but explicit flush ensures 
                // the FileInfo.Length is accurate for the next CheckRotation call.
                _writer.Flush();

                // 2. Check if we need to rotate
                CheckRotation();
            }
        }

        /// <summary>
        /// Determines whether a date-based rotation should occur
        /// based on the configured <see cref="DateRotationType"/>.
        /// </summary>
        private bool ShouldRotateByDate(DateTime now)
        {
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
            // If writer is null, the file hasn't been created yet.
            // If rotation is disabled (Circuit Breaker tripped), do nothing to save CPU.
            if (_rotationDisabled || _writer == null) return;

            // If we recently failed a rotation due to a lock, bypass rotation checks 
            // until the cooldown expires to prevent pipe stalling.
            if (_timeProvider() < _rotationCooldownUntil) return;

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
                _lastRotationDate = _timeProvider(); // Uses the seam
                return;
            }

            // --- DATE ROTATION ---
            if (_enableDateRotation)
            {
                rotateByDate = ShouldRotateByDate(_timeProvider()); // Uses the seam
            }

            if (rotateByDate)
            {
                Rotate();
                _lastRotationDate = _timeProvider(); // Uses the seam
            }
        }

        /// <summary>
        /// Generates a unique file path by inserting a numeric suffix before the file extension if the file already exists.
        /// </summary>
        private static string GenerateUniqueFileName(string basePath)
        {
            if (!File.Exists(basePath))
                return basePath;

            string directory = Path.GetDirectoryName(basePath);
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

            int count = 1;
            string newPath;

            do
            {
                // Safety bound: Prevent the process from hanging or causing high I/O latency 
                // if the directory is pathologically full or locked.
                if (count > AppConfig.RotatingStreamWriterMaxUniqueFilenameRetries)
                {
                    throw new IOException(
                        $"Failed to generate a unique filename for '{basePath}' after {AppConfig.RotatingStreamWriterMaxUniqueFilenameRetries} attempts. " +
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

            // Safely resolve directory to prevent NRE if _file context is bare
            var parentDir = _file.Directory;
            if (parentDir == null)
            {
                Logger.Warn($"Log file '{_file.Name}' has no directory context. Skipping rotation enforcement.");
                return;
            }

            string directory = parentDir.FullName;
            string currentFullName = _file.FullName;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(currentFullName) ?? string.Empty;
            string extension = Path.GetExtension(currentFullName) ?? string.Empty;

            // Pre-calculate values outside the lambda so the analyzer doesn't 
            // have to track string nullability across scopes.
            int extensionLength = extension.Length;
            string prefix = fileNameWithoutExt + ".";

            // Tighten the glob pattern to require the dot separator
            string searchPattern = $"{fileNameWithoutExt}.*{extension}";
            var allPotentialFiles = Directory.GetFiles(directory, searchPattern);

            var rotatedFiles = allPotentialFiles
                .Where(f =>
                {
                    // Explicit guard against Path.GetFileName returning null
                    string name = Path.GetFileName(f);
                    if (string.IsNullOrEmpty(name))
                        return false;

                    if (f.Equals(currentFullName, StringComparison.OrdinalIgnoreCase))
                        return false;

                    // 1. Validate Prefix (using the pre-calculated local variable)
                    if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return false;

                    // 2. Validate Suffix
                    // Using extensionLength > 0 proves to Sonar that we aren't accessing a null string's properties
                    if (extensionLength > 0 && !name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    // 3. Extract the middle portion (the injected timestamp + potential collision suffix)
                    int startIndex = prefix.Length;
                    int expectedMiddleLength = name.Length - startIndex - extensionLength;

                    if (expectedMiddleLength <= 0)
                        return false;

                    string middle = name.Substring(startIndex, expectedMiddleLength);

                    // 4. Strictly validate the middle portion against the expected rotation format
                    return _rotatedTimestampRegex.IsMatch(middle);
                })
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();

            if (rotatedFiles.Count <= _maxRotations)
            {
                // Reset counter if we are successfully within limits
                _consecutiveDeletionFailures = 0;
                return;
            }

            foreach (var file in rotatedFiles.Skip(_maxRotations))
            {
                try
                {
                    File.Delete(file);
                    _consecutiveDeletionFailures = 0; // Reset on any successful deletion
                }
                catch (Exception ex)
                {
                    _consecutiveDeletionFailures++;
                    Logger.Warn($"Failed to delete old log file '{file}': {ex.Message}. Consecutive failures: {_consecutiveDeletionFailures}");

                    // If we hit a threshold (e.g., 10), we log a more severe error to alert operators.
                    if (_consecutiveDeletionFailures >= 10)
                    {
                        Logger.Error($"Persistent failure to enforce log rotation limit for '{_file.Name}'. Disk space growth is no longer bounded.");
                    }
                }
            }
        }

        /// <summary>
        /// Rotates the current log file by inserting a timestamp before the file extension.
        /// Includes a fast-fail retry mechanism and cooldown to prevent child process stalling.
        /// </summary>
        private void Rotate()
        {
            // GUARD: Don't even touch the disk if the circuit is open or cooling down
            if (_rotationDisabled || _timeProvider() < _rotationCooldownUntil) return;

            _file.Refresh();
            if (!_file.Exists || _file.Length == 0) return;

            CloseWriter();

            var now = _timeProvider();
            var timestamp = now.ToString("yyyyMMdd_HHmmss");

            var directory = Path.GetDirectoryName(_file.FullName) ?? AppFoldersHelper.GetAppDirectory();
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_file.FullName);
            var extension = Path.GetExtension(_file.FullName);

            var newFileName = $"{fileNameWithoutExt}.{timestamp}{extension}";
            var rotatedPath = Path.Combine(directory, newFileName);
            rotatedPath = GenerateUniqueFileName(rotatedPath);

            bool success = false;

            for (int attempt = 0; attempt < MaxSyncRotationRetries; attempt++)
            {
                try
                {
                    File.Move(_file.FullName, rotatedPath);
                    success = true;
                    break;
                }
                catch (IOException ex)
                {
                    if (attempt < MaxSyncRotationRetries - 1)
                    {
                        Thread.Sleep(SyncRotationRetryDelayMs);
                    }
                    else
                    {
                        Logger.Warn($"Log rotation deferred due to file lock on '{_file.Name}': {ex.Message}. Will retry in {RotationCooldownMs}ms.");
                        _rotationCooldownUntil = _timeProvider().AddMilliseconds(RotationCooldownMs);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Log rotation critical failure: {ex.Message}. Rotation will be disabled until service restart.", ex);
                    _rotationDisabled = true;
                    break;
                }
            }

            if (success)
            {
                // HEALING: Reset the breaker state on success
                _rotationCooldownUntil = DateTime.MinValue;
                _rotationDisabled = false;

                EnforceMaxRotations();
            }
            else
            {
                Logger.Warn($"Log rotation disabled for: {_file.FullName} due to persistent error.");
            }
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