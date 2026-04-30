using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.IO;
using Servy.Core.Security;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Provides a thread-safe, fail-silent static logging utility for the Servy ecosystem.
    /// Uses <see cref="RotatingStreamWriter"/> to ensure logs are rotated based on size 
    /// while preventing unbounded file growth.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class Logger
    {
        /// <summary>
        /// Default maximum number of backup log files to keep. When the number of rotated files exceeds this limit, the oldest files will be deleted.
        /// </summary>
        public const int DefaultMaxBackupLogFiles = 10;

        /// <summary>
        /// Logs folder path.
        /// </summary>
        public static readonly string LogsPath = Path.Combine(AppConfig.ProgramDataPath, "logs");

        private static readonly object _lock = new object();
        private static volatile RotatingStreamWriter? _writer;
        private static LogLevel _currentLogLevel = LogLevel.Info;
        private static string? _fileName;
        private static long _logRotationSizeMB = AppConfig.DefaultRotationSizeMB;
        private static DateRotationType _dateRotationType;
        private static bool _useLocalTimeForRotation;

        /// <summary>
        /// The maximum number of backup log files to keep. 
        /// Set to 0 to allow an unlimited number of backup files.
        /// </summary>
        private static int _maxBackupLogFiles = DefaultMaxBackupLogFiles;

        /// <summary>
        /// Pre-computed, uppercase string representations of LogLevels.
        /// </summary>
        private static readonly string[] LevelStrings = new[]
        {
            "DEBUG", // 0
            "INFO",  // 1
            "WARN",  // 2
            "ERROR"  // 3
        };

        /// <summary>
        /// Initializes the logger with a specific file name and sets up the rotating stream. 
        /// This should be called once at the beginning of the application lifecycle.
        /// </summary>
        /// <param name="fileName">The name of the log file (e.g., "Servy.Manager.log").</param>
        /// <param name="initialLevel">The starting log level. Defaults to <see cref="LogLevel.Info"/>.</param>
        /// <param name="logRotationSizeMB">The maximum size of the log file in MB before rotation. Defaults to 10MB.</param>
        /// <param name="dateRotationType">
        /// Specifies the interval (Daily, Weekly, Monthly) for time-based log rotation. 
        /// Defaults to <see cref="DateRotationType.None"/>.
        /// </param>
        /// <param name="useLocalTimeForRotation">Indicates whether to use local system time for log rotation (Default: false (UTC)).</param>
        /// <param name="maxBackupLogFiles">The maximum number of backup files to retain. Defaults to 10. Set to 0 for unlimited backups.</param>
        public static void Initialize(
            string? fileName,
            LogLevel initialLevel = LogLevel.Info,
            int logRotationSizeMB = AppConfig.DefaultRotationSizeMB,
            DateRotationType dateRotationType = DateRotationType.None,
            bool useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation,
            int maxBackupLogFiles = DefaultMaxBackupLogFiles
            )
        {
            lock (_lock)
            {
                _fileName = fileName;
                _currentLogLevel = initialLevel;
                _logRotationSizeMB = logRotationSizeMB;
                _dateRotationType = dateRotationType;
                _useLocalTimeForRotation = useLocalTimeForRotation;
                _maxBackupLogFiles = maxBackupLogFiles;

                InternalInitialize();
            }
        }

        /// <summary>
        /// Core initialization logic. Assumes lock is already acquired.
        /// </summary>
        private static void InternalInitialize()
        {
            if (string.IsNullOrEmpty(_fileName)) return;

            try
            {
                EnsureLogsDir();

                string logPath = Path.Combine(LogsPath, _fileName);

                // ATOMIC TEARDOWN:
                // Capture the reference, nullify the static field, then dispose.
                // This ensures waiting threads or 'fast-path' checks see null immediately.
                var oldWriter = _writer;
                _writer = null;
                oldWriter?.Dispose();

                // Convert MB to Bytes for the underlying writer
                long rotationSizeInBytes = _logRotationSizeMB * 1024L * 1024;

                _writer = new RotatingStreamWriter(
                    path: logPath,
                    enableSizeRotation: true,
                    rotationSizeInBytes: rotationSizeInBytes,
                    enableDateRotation: _dateRotationType != DateRotationType.None,
                    dateRotationType: _dateRotationType,
                    maxRotations: _maxBackupLogFiles,
                    useLocalTimeForRotation: _useLocalTimeForRotation
                );
            }
            catch (Exception ex)
            {
                try
                {
                    EnsureLogsDir();

                    var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;

                    File.AppendAllText(Path.Combine(LogsPath, "LoggerInitializationErrors.log"),
                        $"[{now:yyyy-MM-dd HH:mm:ss}] Failed to initialize logger with file '{_fileName}'. Exception: {ex}{Environment.NewLine}");
                }
                catch
                {
                    // Fail-silent
                }
            }
        }

        /// <summary>
        /// Sets the maximum size for log rotation in Megabytes.
        /// If the logger is already initialized, the underlying writer is recreated to apply the new limit.
        /// </summary>
        /// <param name="sizeMB">The size in MB. Must be greater than 0.</param>
        public static void SetLogRotationSize(int sizeMB)
        {
            lock (_lock)
            {
                if (sizeMB > 0 && _logRotationSizeMB != sizeMB)
                {
                    _logRotationSizeMB = sizeMB;

                    // If we have an active writer, recreate it to apply the new size constraint
                    if (_writer != null)
                    {
                        InternalInitialize();
                    }
                }
            }
        }

        /// <summary>
        /// Sets the maximum number of backup log files to retain.
        /// </summary>
        /// <param name="maxBackupLogFiles">Maximum number of backup files. Set to 0 for unlimited backups.</param>
        public static void SetMaxBackupLogFiles(int maxBackupLogFiles)
        {
            lock (_lock)
            {
                if (maxBackupLogFiles >= 0 && _maxBackupLogFiles != maxBackupLogFiles)
                {
                    _maxBackupLogFiles = maxBackupLogFiles;

                    // If we have an active writer, recreate it to apply the new max backup files constraint
                    if (_writer != null)
                    {
                        InternalInitialize();
                    }
                }
            }
        }

        /// <summary>
        /// Updates the date-based rotation strategy at runtime.
        /// </summary>
        /// <param name="dateRotationType">
        /// The new <see cref="DateRotationType"/> to apply (e.g., Daily, Weekly, Monthly, or None).
        /// </param>
        /// <remarks>
        /// If the rotation type is changed and a log file is currently open, the internal writer 
        /// will be re-initialized to ensure the new rotation logic is applied immediately 
        /// to the next write operation.
        /// </remarks>
        public static void SetDateRotationType(DateRotationType dateRotationType)
        {
            lock (_lock)
            {
                if (_dateRotationType != dateRotationType)
                {
                    _dateRotationType = dateRotationType;

                    // If we have an active writer, recreate it to apply the new size constraint
                    if (_writer != null)
                    {
                        InternalInitialize();
                    }
                }
            }
        }

        /// <summary>
        /// Updates the time context used for log rotation calculations at runtime.
        /// </summary>
        /// <param name="useLocalTimeForRotation">
        /// <c>true</c> to rotate logs based on the server's local system time; 
        /// <c>false</c> to use Coordinated Universal Time (UTC).
        /// </param>
        /// <remarks>
        /// <para>
        /// Changing this value at runtime triggers a thread-safe update. If a log writer is currently 
        /// active, it will be re-initialized to ensure subsequent rotation checks immediately 
        /// respect the new time context.
        /// </para>
        /// <para>
        /// Note: Transitioning from UTC to Local (or vice versa) while a log file is open may result 
        /// in a one-time rotation delay or premature rotation if the offset between the two time 
        /// standards crosses a rotation boundary (e.g., midnight).
        /// </para>
        /// </remarks>
        public static void SetUseLocalTimeForRotation(bool useLocalTimeForRotation)
        {
            lock (_lock)
            {
                if (_useLocalTimeForRotation != useLocalTimeForRotation)
                {
                    _useLocalTimeForRotation = useLocalTimeForRotation;

                    // If we have an active writer, recreate it to apply the new time context
                    if (_writer != null)
                    {
                        InternalInitialize();
                    }
                }
            }
        }

        /// <summary>
        /// Sets the minimum log level to be recorded. 
        /// Messages below this level will be ignored.
        /// </summary>
        /// <param name="level">The new <see cref="LogLevel"/>.</param>
        public static void SetLogLevel(LogLevel level)
        {
            lock (_lock)
            {
                _currentLogLevel = level;
            }
        }

        /// <summary>
        /// Logs a debug message and optional exception details at the DEBUG level.
        /// </summary>
        /// <param name="message">The operational message to log.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Debug(string? message, Exception? ex = null)
        {
            if (_currentLogLevel <= LogLevel.Debug)
            {
                Log(LogLevel.Debug, ex != null ? $"{message}{Environment.NewLine}Exception: {ex}" : message);
            }
        }

        /// <summary>
        /// Logs a message at the INFO level. 
        /// Use this for general operational milestones and state changes.
        /// </summary>
        /// <param name="message">The operational message to log.</param>
        public static void Info(string? message)
        {
            if (_currentLogLevel <= LogLevel.Info)
            {
                Log(LogLevel.Info, message);
            }
        }

        /// <summary>
        /// Logs a message at the WARN level. 
        /// Use this for non-critical issues or unexpected states that do not halt execution.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Warn(string? message, Exception? ex = null)
        {
            if (_currentLogLevel <= LogLevel.Warn)
            {
                Log(LogLevel.Warn, ex != null ? $"{message}{Environment.NewLine}Exception: {ex}" : message);
            }
        }

        /// <summary>
        /// Logs an error message and optional exception details at the ERROR level.
        /// </summary>
        /// <param name="message">The description of the error.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Error(string? message, Exception? ex = null)
        {
            if (_currentLogLevel <= LogLevel.Error)
            {
                Log(LogLevel.Error, ex != null ? $"{message}{Environment.NewLine}Exception: {ex}" : message);
            }
        }

        /// <summary>
        /// Core logging logic that handles thread synchronization and delegated I/O via the rotating writer.
        /// </summary>
        /// <param name="level">The severity level enum.</param>
        /// <param name="message">The content of the log entry.</param>
        private static void Log(LogLevel level, string? message)
        {
            // 1. Volatile read: Thread sees 'null' as soon as InternalInitialize starts
            if (_writer == null) return;

            if (string.IsNullOrEmpty(message)) return;

            try
            {
                lock (_lock)
                {
                    // 2. Double-check: If a thread was waiting for the lock while 
                    // InternalInitialize was running, it will now correctly see 
                    // either null or the NEW writer.
                    if (_writer == null) return;

                    string levelName = (int)level >= 0 && (int)level < LevelStrings.Length
                        ? LevelStrings[(int)level]
                        : level.ToString().ToUpper(); // Fallback for safety

                    var sanitizedMessage = message?
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n");

                    // Format: [2026-03-12 22:00:00+01:00] [INFO] Message text OR [2026-03-12 22:00:00Z] [INFO] Message text
                    var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
                    string tzMarker = _useLocalTimeForRotation ? now.ToString("zzz") : "Z";
                    string logEntry = $"[{now:yyyy-MM-dd HH:mm:ss}{tzMarker}] [{levelName}] {sanitizedMessage}";

                    _writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    EnsureLogsDir();

                    var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
                    File.AppendAllText(Path.Combine(LogsPath, "LoggerWriteErrors.log"),
                        $"[{now:yyyy-MM-dd HH:mm:ss}] Failed to write log entry: {ex.Message}{Environment.NewLine}");
                }
                catch { /* truly fail-silent only as last resort */ }
            }
        }

        /// <summary>
        /// Gracefully shuts down the logger by disposing the underlying stream writer.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_writer != null)
                {
                    try
                    {
                        _writer.Dispose();
                    }
                    catch
                    {
                        // Fail-silent
                    }
                    finally
                    {
                        _writer = null;
                    }
                }
            }
        }

        #region Private Helpers

        /// <summary>
        /// Validates that the logging directory exists and applies the required security descriptors to protect log integrity.
        /// </summary>
        private static void EnsureLogsDir()
        {
            // LOGIC: Uses SecurityHelper to create the directory with specific permissions
            SecurityHelper.CreateSecureDirectory(LogsPath, breakInheritance: false);
        }

        #endregion

    }
}