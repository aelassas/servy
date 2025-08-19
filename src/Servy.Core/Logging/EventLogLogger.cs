using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Logs messages to the Windows Event Log.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EventLogLogger : ILogger
    {
        private readonly EventLog _eventLog;

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
            _eventLog.WriteEntry(message, EventLogEntryType.Information);
        }

        /// <inheritdoc/>
        public void Warning(string message)
        {
            _eventLog.WriteEntry(message, EventLogEntryType.Warning);
        }

        /// <inheritdoc/>
        public void Error(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}\n{ex}" : message;
            _eventLog.WriteEntry(fullMessage, EventLogEntryType.Error);
        }
    }
}
