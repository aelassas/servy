using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.IO;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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
        /// Default log rotation size in Megabytes. When the log file exceeds this size, it will be rotated.
        /// </summary>
        public const int DefaultLogRotationSizeMB = 10;

        private static readonly object _lock = new object();
        private static RotatingStreamWriter _writer;
        private static LogLevel _currentLogLevel = LogLevel.Info;
        private static string _fileName;
        private static long _logRotationSizeMB = DefaultLogRotationSizeMB;

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
        /// <param name="logRotationSizeMB">The maximum size of the log file in MB before rotation. Defaults to 10MB.</param>
        public static void Initialize(string fileName, LogLevel initialLevel = LogLevel.Info, int logRotationSizeMB = 10)
        {
            lock (_lock)
            {
                _fileName = fileName;
                _currentLogLevel = initialLevel;
                _logRotationSizeMB = logRotationSizeMB;

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
                var logDir = Path.Combine(AppConfig.ProgramDataPath, "logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logPath = Path.Combine(logDir, _fileName);

                // Clean up existing writer if re-initializing or changing size
                _writer?.Dispose();

                // Convert MB to Bytes for the underlying writer
                long rotationSizeInBytes = _logRotationSizeMB * 1024L * 1024;

                _writer = new RotatingStreamWriter(
                    path: logPath,
                    enableSizeRotation: true,
                    rotationSizeInBytes: rotationSizeInBytes,
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
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to initialize logger with file '{_fileName}'. Exception: {ex}{Environment.NewLine}");
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
        public static void Debug(string message, Exception ex = null)
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
        public static void Error(string message, Exception ex = null)
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