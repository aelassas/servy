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
        private static readonly object _lock = new object();
        private static RotatingStreamWriter _writer;

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
        public static void Initialize(string fileName)
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

                    // Clean up existing writer if Initialize is called multiple times
                    _writer?.Dispose();

                    // Initialize the rotating stream using named parameters for clarity.
                    // We enable size-based rotation and disable date-based rotation by default.
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
                    // Fail-silent: If stream cannot be initialized, Logger will bypass I/O
                    File.AppendAllText(Path.Combine(AppConfig.ProgramDataPath, "logs", "LoggerInitializationErrors.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to initialize logger with file '{fileName}'. Exception: {ex}{Environment.NewLine}");
                }
            }
        }

        /// <summary>
        /// Logs a message at the DEBUG level. 
        /// Use this for high-verbosity diagnostic information useful during development.
        /// </summary>
        /// <param name="message">The diagnostic message to log.</param>
        public static void Debug(string message) => Log("DEBUG", message);

        /// <summary>
        /// Logs a message at the INFO level. 
        /// Use this for general operational milestones and state changes.
        /// </summary>
        /// <param name="message">The operational message to log.</param>
        public static void Info(string message) => Log("INFO", message);

        /// <summary>
        /// Logs a message at the WARN level. 
        /// Use this for non-critical issues or unexpected states that do not halt execution.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public static void Warn(string message) => Log("WARN", message);

        /// <summary>
        /// Logs an error message and optional exception details at the ERROR level.
        /// </summary>
        /// <param name="message">The description of the error.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Error(string message, Exception ex = null)
            => Log("ERROR", ex != null ? $"{message}{Environment.NewLine}Exception: {ex}" : message);

        /// <summary>
        /// Core logging logic that handles thread synchronization and delegated I/O via the rotating writer.
        /// </summary>
        /// <param name="level">The severity level (e.g., INFO, ERROR).</param>
        /// <param name="message">The content of the log entry.</param>
        private static void Log(string level, string message)
        {
            // Fail-fast if the writer wasn't initialized
            if (_writer == null) return;

            try
            {
                lock (_lock)
                {
                    // Format: [2026-03-12 22:00:00] [INFO] Message text
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

                    // RotatingStreamWriter handles the rotation logic and size checks internally
                    _writer.WriteLine(logEntry);

                    // Force a flush if RotatingStreamWriter doesn't have AutoFlush enabled.
                    // This prevents NULL holes if the service is killed suddenly.
                    _writer.Flush();
                }
            }
            catch
            {
                // Fail-silent: Logging should never be a breaking point for the application.
            }
        }

        /// <summary>
        /// Gracefully shuts down the logger by disposing the underlying stream writer.
        /// This should be called during the application exit sequence to ensure 
        /// all file handles are released immediately.
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
                        // Fail-silent during shutdown
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