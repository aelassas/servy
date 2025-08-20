using System;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines methods for logging informational, warning, and error messages.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Prefix to prepend to log messages.
        /// </summary>
        string Prefix { get; set; }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Info(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        void Warning(string message);

        /// <summary>
        /// Logs an error message and optional exception.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">The exception associated with the error, or <c>null</c> if none.</param>
        void Error(string message, Exception ex = null);
    }
}
