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
        /// <summary>
        /// Validates the rotated filename segment. 
        /// Updated to allow chained collision suffixes (e.g., .20260429_213213.(1).(2)) 
        /// produced during high-frequency rotations within a single second.
        /// </summary>
        private static readonly Regex _rotatedTimestampRegex = new Regex(@"^\d{8}_\d{6}(?:\.\(\d+\))*$", RegexOptions.Compiled, AppConfig.InputRegexTimeout);

        private const string RotationTimestampFormat = "yyyyMMdd_HHmmss";
        // length includes the leading dot
        private static readonly int RotationTimestampExtensionLength = RotationTimestampFormat.Length + 1;

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
        private DateTime _pendingRotationDate; // Tracks the uncommitted date of an in-flight rotation
        private readonly int _maxRotations; // 0 = unlimited
        private int _consecutiveDeletionFailures;
        private readonly object _lock = new object();
        private readonly Func<DateTime> _timeProvider;
        private DateTime _oversizeWarningNextEligibleAt = DateTime.MinValue;

        /// <summary>
        /// A circuit-breaker flag that disables all rotation logic if a permanent failure occurs.
        /// This prevents infinite loops of failed moves that spike CPU usage.
        /// </summary>
        private bool _rotationDisabled;

        /// <summary>
        /// Indicates that a thread is currently moving the log file on disk. 
        /// Other threads must wait to avoid opening a handle to a file that is about to be renamed.
        /// </summary>
        private bool _rotationInProgress;

        /// <summary>
        /// The timestamp when the circuit breaker will auto-reset to a half-open state.
        /// </summary>
        private DateTime _disabledCooldownUntil = DateTime.MinValue;

        // --- Cooldown and fast-fail constraints ---
        private DateTime _rotationCooldownUntil = DateTime.MinValue;

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
                FileShare.ReadWrite | FileShare.Delete,
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
            WriteInternal(w => w.WriteLine(line));
        }

        /// <summary>
        /// Writes text to the log file without adding a newline, checking for rotation.
        /// </summary>
        /// <param name="text">The text to write.</param>
        public void Write(string text)
        {
            WriteInternal(w => w.Write(text));
        }

        /// <summary>
        /// Shared internal logic for writing to the underlying stream and triggering rotation checks.
        /// </summary>
        /// <param name="writeAction">The specific write operation to perform on the <see cref="StreamWriter"/>.</param>
        private void WriteInternal(Action<StreamWriter> writeAction)
        {
            string pathToRotate = null;
            string targetRotatedPath = null;

            lock (_lock)
            {
                // Block other threads from writing or re-opening the file 
                // while the physical File.Move is taking place.
                while (_rotationInProgress)
                {
                    // Releases the lock and sleeps until Monitor.PulseAll is called
                    Monitor.Wait(_lock);
                }

                // Check disposal *after* waking up, in case Dispose was called while we waited
                if (_disposed) return;

                // 1. Lazy Initialize if null (either first run or just rotated)
                if (_writer == null)
                {
                    InitializeWriter();
                }

                // Execute the provided write action (Write or WriteLine)
                writeAction(_writer);

                // AutoFlush is true in InitializeWriter, but explicit flush ensures 
                // the FileInfo.Length is accurate for the next CheckRotation call.
                _writer.Flush();

                // 2. Check if we need to rotate
                (pathToRotate, targetRotatedPath) = CheckRotation();
                if (pathToRotate != null && targetRotatedPath != null)
                {
                    _rotationInProgress = true;
                }
            }

            // Perform the physical file move and retry logic completely outside the lock
            // to prevent blocking other threads during I/O-intensive rotation operations.
            if (pathToRotate != null && targetRotatedPath != null)
            {
                try
                {
                    PerformPhysicalRotation(pathToRotate, targetRotatedPath);
                }
                finally
                {
                    lock (_lock)
                    {
                        _rotationInProgress = false;
                        Monitor.PulseAll(_lock); // Wake up waiting writers
                    }
                }
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

                    // ROBUSTNESS: Ensure year-over-year transitions where both dates fall in ISO week 1 
                    // do not bypass rotation when a full calendar year has actually passed.
                    return (now.Date - _lastRotationDate.Date).TotalDays >= 7 || thisWeek != lastWeek;

                case DateRotationType.Monthly:
                    return now.Month != _lastRotationDate.Month || now.Year != _lastRotationDate.Year;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether the current log file should be rotated,
        /// based on enabled rotation modes (size and/or date).
        /// If rotation is required, detaches the writer and returns the target paths.
        /// </summary>
        private (string oldPath, string newPath) CheckRotation()
        {
            var now = _timeProvider();

            // Self-Healing Circuit Breaker: Try to clear a disabled state after a cool-off period.
            if (_rotationDisabled)
            {
                if (now > _disabledCooldownUntil)
                {
                    Logger.Info($"Log rotation circuit breaker resetting for '{_file.Name}'. Attempting rotation again.");
                    _rotationDisabled = false;
                }
                else
                {
                    // Optionally log a warning if file size gets egregiously large while disabled
                    _file.Refresh();
                    if (_enableSizeRotation && _file.Exists && _file.Length > _rotationSizeInBytes * 2 && now >= _oversizeWarningNextEligibleAt)
                    {
                        Logger.Warn($"Log rotation is currently disabled due to previous errors. File '{_file.Name}' has exceeded twice its max size ({_file.Length} bytes).");
                        _oversizeWarningNextEligibleAt = _disabledCooldownUntil;
                    }
                    return (null, null);
                }
            }

            if (_writer == null) return (null, null);

            // If we recently failed a rotation due to an IO lock, bypass rotation checks 
            // until the short IO cooldown expires to prevent pipe stalling.
            if (now < _rotationCooldownUntil) return (null, null);

            _file.Refresh();
            if (!_file.Exists) return (null, null);

            long currentLength = _file.Length;

            bool rotateBySize = false;
            bool rotateByDate = false;

            // --- SIZE ROTATION ---
            if (_enableSizeRotation && _rotationSizeInBytes > 0 && currentLength >= _rotationSizeInBytes)
            {
                rotateBySize = true;
            }

            if (rotateBySize)
            {
                return PrepareRotation(now);
            }

            // --- DATE ROTATION ---
            if (_enableDateRotation)
            {
                rotateByDate = ShouldRotateByDate(now);
            }

            if (rotateByDate)
            {
                return PrepareRotation(now);
            }

            return (null, null);
        }

        /// <summary>
        /// Atomically detaches the writer from the current log file and constructs the rotation paths.
        /// </summary>
        private (string oldPath, string newPath) PrepareRotation(DateTime now)
        {
            CloseWriter();

            var timestamp = now.ToString(RotationTimestampFormat, CultureInfo.InvariantCulture);

            var directory = Path.GetDirectoryName(_file.FullName) ?? AppFoldersHelper.GetAppDirectory();
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_file.FullName);
            var extension = Path.GetExtension(_file.FullName);

            var newFileName = $"{fileNameWithoutExt}.{timestamp}{extension}";
            var rotatedPath = Path.Combine(directory, newFileName);

            // Set the short IO cooldown immediately inside the lock so subsequent concurrent writes 
            // don't try to rotate the detached file while the physical move is pending.
            _rotationCooldownUntil = now.AddMilliseconds(AppConfig.LogRotationCooldownMs);

            // Store the rotation date as pending. It will only be committed upon physical success.
            _pendingRotationDate = now;

            return (_file.FullName, rotatedPath);
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

            bool isTimestamp = extension.Length == RotationTimestampExtensionLength &&
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

            string[] allPotentialFiles;
            try
            {
                allPotentialFiles = Directory.GetFiles(directory, searchPattern);
            }
            catch (Exception ex)
            {
                int currentFailures = Interlocked.Increment(ref _consecutiveDeletionFailures);
                Logger.Warn($"Failed to enumerate rotated log files in '{directory}': {ex.Message}. Consecutive failures: {currentFailures}");
                if (currentFailures >= AppConfig.LogRotationDeletionFailureEscalationThreshold)
                    Logger.Error($"Persistent failure to enforce log rotation limit for '{_file.FullName}' (consecutive failures: {currentFailures}, max retained: {_maxRotations}). Disk space growth is no longer bounded.");
                return;
            }

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
                Interlocked.Exchange(ref _consecutiveDeletionFailures, 0);
                return;
            }

            foreach (var file in rotatedFiles.Skip(_maxRotations))
            {
                try
                {
                    File.Delete(file);
                    Interlocked.Exchange(ref _consecutiveDeletionFailures, 0); // Reset on any successful deletion
                }
                catch (Exception ex)
                {
                    int currentFailures = Interlocked.Increment(ref _consecutiveDeletionFailures);
                    Logger.Warn($"Failed to delete old log file '{file}': {ex.Message}. Consecutive failures: {currentFailures}");

                    // If we hit a threshold (e.g., 10), we log a more severe error to alert operators.
                    if (currentFailures >= AppConfig.LogRotationDeletionFailureEscalationThreshold)
                    {
                        Logger.Error($"Persistent failure to enforce log rotation limit for '{_file.FullName}' (consecutive failures: {currentFailures}, max retained: {_maxRotations}). Disk space growth is no longer bounded.");
                    }
                }
            }
        }

        /// <summary>
        /// Executes the physical file rename operation outside the public write lock.
        /// Includes a fast-fail retry mechanism to gracefully handle external file contention.
        /// </summary>
        private void PerformPhysicalRotation(string oldPath, string rotatedPath)
        {
            // Execute uniqueness generation outside the lock to minimize the critical section window
            try
            {
                rotatedPath = GenerateUniqueFileName(rotatedPath);
            }
            catch (Exception ex)
            {
                TripCircuitBreaker($"Failed to generate unique filename for rotation: {ex.Message}", ex);
                return;
            }

            bool success = false;

            for (int attempt = 0; attempt < AppConfig.LogRotationMaxSyncRetries; attempt++)
            {
                try
                {
                    File.Move(oldPath, rotatedPath);
                    success = true;
                    break;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (attempt < AppConfig.LogRotationMaxSyncRetries - 1)
                    {
                        // Unlocked sleep! Writers can proceed to generate a new file.
                        Thread.Sleep(AppConfig.LogRotationSyncRetryDelayMs);
                    }
                    else
                    {
                        Logger.Warn($"Log rotation deferred due to file lock on '{_file.Name}': {ex.Message}. Will retry in {AppConfig.LogRotationCooldownMs}ms.");
                        return; // IO Cooldown is already active, we just abort this attempt
                    }
                }
                catch (Exception ex)
                {
                    TripCircuitBreaker($"Log rotation critical failure: {ex.Message}", ex);
                    break;
                }
            }

            if (success)
            {
                lock (_lock)
                {
                    // HEALING: Reset the breaker state on success
                    _rotationCooldownUntil = DateTime.MinValue;
                    _rotationDisabled = false;
                    _disabledCooldownUntil = DateTime.MinValue;

                    // COMMIT the pending rotation date now that the file move actually succeeded
                    _lastRotationDate = _pendingRotationDate;
                }

                // File management processes are safe to run unlocked
                EnforceMaxRotations();
            }
        }

        /// <summary>
        /// Trips the circuit breaker, disabling rotation until the critical cooldown period expires.
        /// </summary>
        private void TripCircuitBreaker(string message, Exception ex)
        {
            Logger.Error($"{message}. Rotation will be disabled for {AppConfig.LogRotationCriticalFailureCooldownMs / AppConfig.MillisecondsPerMinute} minutes.", ex);
            lock (_lock)
            {
                _rotationDisabled = true;
                _disabledCooldownUntil = _timeProvider().AddMilliseconds(AppConfig.LogRotationCriticalFailureCooldownMs);
            }
        }

        /// <summary>
        /// Gracefully closes and disposes of the <see cref="StreamWriter"/> and the underlying <see cref="FileStream"/>.
        /// </summary>
        private void CloseWriter()
        {
            if (_writer != null)
            {
                try { _writer.Flush(); }
                catch (Exception ex) { Logger.Warn($"CloseWriter: Flush failed for '{_file.Name}': {ex.Message}"); }
                finally
                {
                    try { _writer.Dispose(); } catch { /* best effort */ }
                    _writer = null;
                }
            }
            if (_baseStream != null)
            {
                try { _baseStream.Dispose(); } catch { /* best effort */ }
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