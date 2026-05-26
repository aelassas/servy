using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.IO;
using Servy.Core.Security;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Provides a thread-safe, fail-silent static logging utility for the Servy ecosystem.
    /// Uses <see cref="RotatingStreamWriter"/> to ensure logs are rotated based on size 
    /// while preventing unbounded file growth.
    /// </summary>
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

        /// <summary>
        /// The maximum number of fallback log writes allowed per process lifetime.
        /// Prevents unbounded growth of fallback log files if the primary logger continuously fails.
        /// </summary>
        private const int MaxFallbackWrites = 10;

        private static readonly object _lock = new object();
        private static volatile RotatingStreamWriter _writer;

        // Volatile int backing field for LogLevel to ensure visibility across threads on hot paths
        private static volatile int _currentLogLevel = (int)LogLevel.Info;

        private static string _fileName;
        private static int _logRotationSizeMB = AppConfig.DefaultRotationSizeMB;
        private static DateRotationType _dateRotationType;
        private static bool _useLocalTimeForRotation;

        /// <summary>
        /// The maximum number of backup log files to keep. 
        /// Set to 0 to allow an unlimited number of backup files.
        /// </summary>
        private static int _maxBackupLogFiles = DefaultMaxBackupLogFiles;

        // Counters to limit fallback file growth
        private static int _initFallbackWriteCount = 0;
        private static int _logFallbackWriteCount = 0;

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
        /// <param name="logLevel">The starting log level. Defaults to <see cref="LogLevel.Info"/>.</param>
        /// <param name="logRotationSizeMB">The maximum size of the log file in MB before rotation. Defaults to 10MB.</param>
        /// <param name="dateRotationType">
        /// Specifies the interval (Daily, Weekly, Monthly) for time-based log rotation. 
        /// Defaults to <see cref="DateRotationType.None"/>.
        /// </param>
        /// <param name="useLocalTimeForRotation">Indicates whether to use local system time for log rotation (Default: false (UTC)).</param>
        /// <param name="maxBackupLogFiles">The maximum number of backup files to retain. Defaults to 10. Set to 0 for unlimited backups.</param>
        public static void Initialize(
            string fileName,
            LogLevel logLevel = LogLevel.Info,
            int logRotationSizeMB = AppConfig.DefaultRotationSizeMB,
            DateRotationType dateRotationType = DateRotationType.None,
            bool useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation,
            int maxBackupLogFiles = DefaultMaxBackupLogFiles
            )
        {
            lock (_lock)
            {
                _fileName = fileName;
                _currentLogLevel = (int)logLevel;
                _logRotationSizeMB = logRotationSizeMB;
                _dateRotationType = dateRotationType;
                _useLocalTimeForRotation = useLocalTimeForRotation;
                _maxBackupLogFiles = maxBackupLogFiles;

                InternalInitialize();
            }
        }

        /// <summary>
        /// Configures and initializes the global logging state, including severity filters and rotation policies.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is thread-safe and can be called at runtime to reconfigure logging parameters. 
        /// If an active log writer already exists, or if a filename was previously established, it is automatically 
        /// reinitialized to immediately apply the new rotation and retention settings.
        /// </para>
        /// <para>
        /// Settings established here govern the behavior of the <c>InternalInitialize</c> process, which 
        /// manages the physical file handles and archive logic for the <see cref="Servy.Service"/>.
        /// </para>
        /// </remarks>
        /// <param name="logLevel">The minimum severity level required for an entry to be recorded. Defaults to <see cref="LogLevel.Info"/>.</param>
        /// <param name="logRotationSizeMB">The maximum size of the log file in MB before rotation occurs. Defaults to 10MB.</param>
        /// <param name="dateRotationType">
        /// Specifies the interval (Daily, Weekly, Monthly) for time-based log rotation. 
        /// Defaults to <see cref="DateRotationType.None"/>.
        /// </param>
        /// <param name="useLocalTimeForRotation">Indicates whether to use local system time for log rotation (Default: false (UTC)).</param>
        /// <param name="maxBackupLogFiles">The maximum number of backup files to retain. Defaults to 10. Set to 0 for unlimited backups.</param>
        public static void Initialize(
            LogLevel logLevel = LogLevel.Info,
            int logRotationSizeMB = AppConfig.DefaultRotationSizeMB,
            DateRotationType dateRotationType = DateRotationType.None,
            bool useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation,
            int maxBackupLogFiles = DefaultMaxBackupLogFiles
            )
        {
            lock (_lock)
            {
                _currentLogLevel = (int)logLevel;
                _logRotationSizeMB = logRotationSizeMB;
                _dateRotationType = dateRotationType;
                _useLocalTimeForRotation = useLocalTimeForRotation;
                _maxBackupLogFiles = maxBackupLogFiles;

                // Re-arm or cycle the writer if we have a valid baseline path,
                // ensuring re-initialization requests that follow a Shutdown() do not silently lose their state.
                if (!string.IsNullOrEmpty(_fileName))
                {
                    InternalInitialize();
                }
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
                long rotationSizeInBytes = AppConfig.ToBytes(_logRotationSizeMB);

                _writer = new RotatingStreamWriter(
                    path: logPath,
                    enableSizeRotation: true,
                    rotationSizeInBytes: rotationSizeInBytes,
                    enableDateRotation: _dateRotationType != DateRotationType.None,
                    dateRotationType: _dateRotationType,
                    maxRotations: _maxBackupLogFiles,
                    useLocalTimeForRotation: _useLocalTimeForRotation
                );

                // Primary writer just came back online; allow another fallback budget.
                Interlocked.Exchange(ref _initFallbackWriteCount, 0);
                Interlocked.Exchange(ref _logFallbackWriteCount, 0);
            }
            catch (Exception ex)
            {
                try
                {
                    // Fail-silent, but limit writes to prevent disk exhaustion
                    if (Interlocked.Increment(ref _initFallbackWriteCount) <= MaxFallbackWrites)
                    {
                        EnsureLogsDir();
                        var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
                        string tzMarker = _useLocalTimeForRotation ? now.ToString("zzz") : "Z";
                        File.AppendAllText(Path.Combine(LogsPath, "LoggerInitializationErrors.log"),
                            $"[{now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}{tzMarker}] Failed to initialize logger with file '{_fileName}'. Exception: {ex}{Environment.NewLine}");
                    }
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
        public static void SetDateRotationType(DateRotationType dateRotationType)
        {
            lock (_lock)
            {
                if (_dateRotationType != dateRotationType)
                {
                    _dateRotationType = dateRotationType;

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
        /// <para>
        /// <b>Architectural Warning:</b> This configuration couples two distinct behavioral domains: log file rotation policy 
        /// and log line token rendering. Modifying this setting dynamically at runtime will instantly alter the time-base format 
        /// of all subsequent entries appended to active logs. 
        /// </para>
        /// <para>
        /// This format drift can negatively affect downstream log indexers, automated SIEM regex ingestion rules, and forensic 
        /// timeline reconstruction when troubleshooting across different infrastructure nodes.
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
                _currentLogLevel = (int)level;
            }
        }

        /// <summary>
        /// Logs a debug message and optional exception details at the DEBUG level.
        /// </summary>
        /// <param name="message">The operational message to log.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Debug(string message, Exception ex = null)
        {
            if ((LogLevel)_currentLogLevel <= LogLevel.Debug)
            {
                Log(LogLevel.Debug, ex != null ? $"{message} | Exception: {FormatException(ex)}" : message);
            }
        }

        /// <summary>
        /// Logs an informational message, optionally including detailed exception data.
        /// </summary>
        /// <param name="message">The informational message to log.</param>
        /// <param name="ex">An optional exception to include in the log entry. If provided, the exception is formatted and appended to the message.</param>
        /// <remarks>
        /// <para>
        /// This method checks the current <see cref="LogLevel"/> before proceeding. The log is only written 
        /// if the system is configured for <see cref="LogLevel.Info"/> or more verbose output.
        /// </para>
        /// <para>
        /// When an exception is provided, it is processed via <c>FormatException</c> and appended to the 
        /// message using the format: <c>{message} | Exception: {formattedException}</c>.
        /// </para>
        /// </remarks>
        public static void Info(string message, Exception ex = null)
        {
            if ((LogLevel)_currentLogLevel <= LogLevel.Info)
            {
                Log(LogLevel.Info, ex != null ? $"{message} | Exception: {FormatException(ex)}" : message);
            }
        }

        /// <summary>
        /// Logs a message at the WARN level. 
        /// Use this for non-critical issues or unexpected states that do not halt execution.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Warn(string message, Exception ex = null)
        {
            if ((LogLevel)_currentLogLevel <= LogLevel.Warn)
            {
                Log(LogLevel.Warn, ex != null ? $"{message} | Exception: {FormatException(ex)}" : message);
            }
        }

        /// <summary>
        /// Logs an error message and optional exception details at the ERROR level.
        /// </summary>
        /// <param name="message">The description of the error.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Error(string message, Exception ex = null)
        {
            if ((LogLevel)_currentLogLevel <= LogLevel.Error)
            {
                Log(LogLevel.Error, ex != null ? $"{message} | Exception: {FormatException(ex)}" : message);
            }
        }

        /// <summary>
        /// Core logging logic that handles thread synchronization and delegated I/O via the rotating writer.
        /// </summary>
        /// <param name="level">The severity level enum.</param>
        /// <param name="message">The content of the log entry.</param>
        private static void Log(LogLevel level, string message)
        {
            // 1. Volatile read: Thread sees 'null' as soon as InternalInitialize starts
            if (_writer == null) return;

            if (string.IsNullOrEmpty(message)) return;

            try
            {
                lock (_lock)
                {
                    // 2. Double-check: Correctly see either null or the NEW writer.
                    if (_writer == null) return;

                    string levelName = (int)level >= 0 && (int)level < LevelStrings.Length
                        ? LevelStrings[(int)level]
                        : level.ToString().ToUpperInvariant(); // Fallback for safety

                    // Sanitize message into a single-line representation for better scannability
                    var sanitizedMessage = message
                        .Replace("\r", "")
                        .Replace("\n", " ; ")
                        .Trim();

                    // Format: [2026-05-06 08:58:20+01:00] [INFO] Message text OR [2026-05-06 08:58:20Z] [INFO] Message text
                    var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
                    string tzMarker = _useLocalTimeForRotation ? now.ToString("zzz") : "Z";
                    string logEntry = $"[{now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}{tzMarker}] [{levelName}] {sanitizedMessage}";

                    _writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    // Fail-silent, but limit writes to prevent disk exhaustion.
                    // Interlocked is critical here as Log() can be called concurrently from multiple threads.
                    if (Interlocked.Increment(ref _logFallbackWriteCount) <= MaxFallbackWrites)
                    {
                        EnsureLogsDir();
                        var now = _useLocalTimeForRotation ? DateTime.Now : DateTime.UtcNow;
                        string tzMarker = _useLocalTimeForRotation ? now.ToString("zzz") : "Z";
                        File.AppendAllText(Path.Combine(LogsPath, "LoggerWriteErrors.log"),
                            $"[{now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}{tzMarker}] Failed to write log entry: {ex.Message}{Environment.NewLine}");
                    }
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

        /// <summary>
        /// Formats an exception into a scannable, single-line representation with depth and length limits.
        /// </summary>
        /// <param name="ex">The exception to format.</param>
        /// <returns>A formatted string with frame separators instead of newlines.</returns>
        private static string FormatException(Exception ex)
        {
            if (ex == null) return string.Empty;

            var sb = new StringBuilder();
            var current = ex;
            int processedDepth = 0;

            while (current != null && processedDepth < AppConfig.LoggerMaxInnerExceptionDepth)
            {
                if (processedDepth > 0)
                {
                    sb.Append(" [Inner -> ");
                }

                // Append the core exception details
                sb.Append(current.GetType().Name).Append(": ").Append(current.Message);

                if (!string.IsNullOrWhiteSpace(current.StackTrace))
                {
                    // Sanitize the stack trace for single-line output
                    var sanitizedStack = current.StackTrace
                        .Replace("\r", "")
                        .Replace("\n", " ; ")
                        .Trim();

                    sb.Append(" (at ").Append(sanitizedStack).Append(')');
                }

                // Hard size limit check to prevent OOM and disk pressure
                if (sb.Length > AppConfig.LoggerMaxFormattedExceptionLength)
                {
                    const string truncMarker = "... [truncated]";
                    int reserved = truncMarker.Length + processedDepth; // reserve room for the marker and ']' chars too
                    int target = Math.Max(0, AppConfig.LoggerMaxFormattedExceptionLength - reserved);
                    sb.Length = target;
                    sb.Append(truncMarker);
                    // Skip the trailing-bracket loop on the truncated path:
                    return sb.ToString();
                }

                current = current.InnerException;
                processedDepth++;
            }

            // Append closing brackets for all opened [Inner -> tags
            for (int i = 1; i < processedDepth; i++)
            {
                sb.Append(']');
            }

            return sb.ToString();
        }

        #endregion
    }
}