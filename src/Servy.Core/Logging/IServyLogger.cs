namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines methods for logging informational, warning, and error messages.
    /// </summary>
    public interface IServyLogger: IDisposable
    {
        /// <summary>
        /// Gets the optional prefix to be prepended to every log message 
        /// emitted by this logger instance.
        /// </summary>
        string? Prefix { get; }

        /// <summary>
        /// Creates a new <see cref="IServyLogger"/> instance that inherits the settings 
        /// of the current logger but applies a specific prefix to all its messages.
        /// </summary>
        /// <remarks>
        /// This is useful for identifying logs originating from a specific 
        /// sub-component or background task.
        /// </remarks>
        /// <param name="prefix">The string to prepend to messages in the new scoped logger.</param>
        /// <returns>A new <see cref="IServyLogger"/> instance configured with the specified prefix.</returns>
        IServyLogger CreateScoped(string prefix);

        /// <summary>
        /// Sets the minimum log level to be recorded. 
        /// Messages below this level will be ignored.
        /// </summary>
        /// <param name="level">The new <see cref="LogLevel"/>.</param>
        void SetLogLevel(LogLevel level);

        /// <summary>
        /// Sets whether logging to the Windows Event Log is enabled.
        /// </summary>
        /// <param name="isEnabled"><see langword="true"/> to enable Event Log logging; <see langword="false"/> to disable it.</param>
        void SetIsEventLogEnabled(bool isEnabled);

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
