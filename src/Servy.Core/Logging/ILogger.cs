namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines methods for logging informational, warning, and error messages.
    /// </summary>
    public interface ILogger: IDisposable
    {
        /// <summary>
        /// Prefix to prepend to log messages.
        /// </summary>
        string? Prefix { get; set; }

        /// <summary>
        /// Sets the minimum log level to be recorded. 
        /// Messages below this level will be ignored.
        /// </summary>
        /// <param name="level">The new <see cref="LogLevel"/>.</param>
        void SetLogLevel(LogLevel level);

        /// <summary>
        /// Logs a debug message and optional exception details at the DEBUG level.
        /// </summary>
        /// <param name="message">The operational message to log.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to include in the log trace.</param>
        void Debug(string message, Exception? ex = null);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Info(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        void Warn(string message);

        /// <summary>
        /// Logs an error message and optional exception.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">The exception associated with the error, or <c>null</c> if none.</param>
        void Error(string message, Exception? ex = null);
    }
}
