using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.IO;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines the severity levels for log entries.
    /// </summary>
    public enum LogLevel
    {
        /// <summary> High-verbosity diagnostic information. </summary>
        Debug = 0,
        /// <summary> General operational milestones. </summary>
        Info = 1,
        /// <summary> Non-critical issues or unexpected states. </summary>
        Warn = 2,
        /// <summary> Critical failures and exceptions. </summary>
        Error = 3
    }

    /// <summary>
    /// Provides a thread-safe, fail-silent static logging utility for the Servy ecosystem.
    /// Uses <see cref="RotatingStreamWriter"/> to ensure logs are rotated based on size 
    /// while preventing unbounded file growth.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static RotatingStreamWriter? _writer;
        private static LogLevel _currentLogLevel = LogLevel.Info;

        /// <summary>
        /// The maximum size a log file can reach before rotation is triggered.
        /// Defaults to 10MB.
        /// </summary>
        private const long MaxLogSizeBuffer = 10 * 1024 * 1024;

        /// <summary>
        /// The maximum number of backup log files to keep. 
        /// Set to 0 to allow an unlimited number of backup files.
        /// </summary>
        private const int MaxBackupFiles = 0;

        /// <summary>
        /// Initializes the logger with a specific file name and sets up the rotating stream. 
        /// This should be called once at the beginning of the application lifecycle.
        /// </summary>
        /// <param name="fileName">The name of the log file (e.g., "Servy.Manager.log").</param>
        /// <param name="initialLevel">The starting log level. Defaults to <see cref="LogLevel.Info"/>.</param>
        public static void Initialize(string fileName, LogLevel initialLevel = LogLevel.Info)
        {
            lock (_lock)
            {
                try
                {
                    var logDir = Path.Combine(AppConfig.ProgramDataPath, "logs");
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    string logPath = Path.Combine(logDir, fileName);

                    _currentLogLevel = initialLevel;

                    // Clean up existing writer if Initialize is called multiple times
                    _writer?.Dispose();

                    _writer = new RotatingStreamWriter(
                        path: logPath,
                        enableSizeRotation: true,
                        rotationSizeInBytes: MaxLogSizeBuffer,
                        enableDateRotation: false,
                        dateRotationType: DateRotationType.Daily,
                        maxRotations: MaxBackupFiles
                    );
                }
                catch (Exception ex)
                {
                    try
                    {
                        var logDir = Path.Combine(AppConfig.ProgramDataPath, "logs");
                        if (!Directory.Exists(logDir))
                        {
                            Directory.CreateDirectory(logDir);
                        }

                        File.AppendAllText(Path.Combine(logDir, "LoggerInitializationErrors.log"),
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to initialize logger with file '{fileName}'. Exception: {ex}{Environment.NewLine}");
                    }
                    catch
                    {
                        // Fail-silent
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
        /// Enables or disables DEBUG level logging at runtime. 
        /// Provided for backward compatibility with binary flags.
        /// </summary>
        /// <param name="enable">True to set level to DEBUG; false to set level to INFO.</param>
        public static void EnableDebug(bool enable) => SetLogLevel(enable ? LogLevel.Debug : LogLevel.Info);

        /// <summary>
        /// Logs a debug message and optional exception details at the DEBUG level.
        /// </summary>
        /// <param name="message">The operational message to log.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Debug(string message, Exception? ex = null)
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
        public static void Info(string message)
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
        public static void Warn(string message)
        {
            if (_currentLogLevel <= LogLevel.Warn)
            {
                Log(LogLevel.Warn, message);
            }
        }

        /// <summary>
        /// Logs an error message and optional exception details at the ERROR level.
        /// </summary>
        /// <param name="message">The description of the error.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Error(string message, Exception? ex = null)
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
        private static void Log(LogLevel level, string message)
        {
            if (_writer == null) return;

            try
            {
                lock (_lock)
                {
                    // Format: [2026-03-12 22:00:00] [INFO] Message text
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString().ToUpper()}] {message}";

                    _writer.WriteLine(logEntry);
                    _writer.Flush();
                }
            }
            catch
            {
                // Fail-silent
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
    }
}