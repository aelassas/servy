using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Logs messages to the Windows Event Log with Event IDs.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EventLogLogger : ILogger
    {
        private readonly EventLog _eventLog;

        ///<inheritdoc/>
        public string Prefix { get; set; }

        // Default Event IDs per level
        private const int InfoEventId = 1000;
        private const int WarningEventId = 2000;
        private const int ErrorEventId = 3000;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogLogger"/> class with the specified source.
        /// </summary>
        /// <param name="source">The event source name used for logging.</param>
        public EventLogLogger(string source)
        {
            _eventLog = new EventLog();

            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, "Application");
            }

            _eventLog.Source = source;
            _eventLog.Log = "Application";
        }

        /// <inheritdoc/>
        public void Info(string message)
        {
            _eventLog.WriteEntry(Format(message), EventLogEntryType.Information, InfoEventId);
        }

        /// <inheritdoc/>
        public void Warning(string message)
        {
            _eventLog.WriteEntry(Format(message), EventLogEntryType.Warning, WarningEventId);
        }

        /// <inheritdoc/>
        public void Error(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}\n{ex}" : message;
            _eventLog.WriteEntry(Format(fullMessage), EventLogEntryType.Error, ErrorEventId);
        }

        #region Private Helpers

        /// <summary>
        /// Formats a log message by prepending the <see cref="Prefix"/> if it is set.
        /// </summary>
        /// <param name="message">The original log message.</param>
        /// <returns>The formatted message with prefix if available.</returns>
        private string Format(string message)
        {
            return string.IsNullOrEmpty(Prefix) ? message : $"[{Prefix}] {message}";
        }

        #endregion
    }
}
