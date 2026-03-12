using Servy.Core.Config;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Provides a thread-safe, fail-silent static logging utility for the Servy ecosystem.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logFileName = "Servy.log";

        /// <summary>
        /// Initializes the logger with a specific file name. 
        /// Should be called at the very beginning of the application lifecycle.
        /// </summary>
        /// <param name="fileName">The name of the log file (e.g., "Servy.Manager.log").</param>
        public static void Initialize(string fileName)
        {
            _logFileName = fileName;
        }

        /// <summary>
        /// Logs a message at the DEBUG level. Use for high-verbosity diagnostic information.
        /// </summary>
        /// <param name="message">The diagnostic message to log.</param>
        public static void Debug(string message) => Log("DEBUG", message);

        /// <summary>
        /// Logs a message at the INFO level. Use for general operational milestones.
        /// </summary>
        /// <param name="message">The operational message to log.</param>
        public static void Info(string message) => Log("INFO", message);

        /// <summary>
        /// Logs a message at the WARN level. Use for non-critical issues or unexpected states 
        /// that do not halt execution.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public static void Warn(string message) => Log("WARN", message);

        /// <summary>
        /// Logs an error message and optional exception details at the ERROR level.
        /// </summary>
        /// <param name="message">The error description.</param>
        /// <param name="ex">The optional <see cref="Exception"/> to include in the log trace.</param>
        public static void Error(string message, Exception? ex = null)
            => Log("ERROR", ex != null ? $"{message}{Environment.NewLine}Exception: {ex}" : message);

        /// <summary>
        /// Core logging logic that handles directory creation, thread synchronization, and I/O.
        /// </summary>
        /// <remarks>
        /// This method is wrapped in a broad try-catch to ensure that logging failures 
        /// never crash the calling application.
        /// </remarks>
        /// <param name="level">The severity level string.</param>
        /// <param name="message">The message body.</param>
        private static void Log(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    // Ensure the ProgramData path exists (e.g., C:\ProgramData\Servy)
                    if (!Directory.Exists(AppConfig.ProgramDataPath))
                    {
                        Directory.CreateDirectory(AppConfig.ProgramDataPath);
                    }

                    string logPath = Path.Combine(AppConfig.ProgramDataPath, _logFileName);

                    // Format: [2026-03-12 22:00:00] [INFO] Message text
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

                    File.AppendAllText(logPath, logEntry);
                }
            }
            catch
            {
                // Fail-silent: Logging should never be a breaking point for the application.
            }
        }
    }
}