using System;
using System.Diagnostics;

namespace Servy.Service
{
    public class EventLogLogger : ILogger
    {
        private readonly EventLog _eventLog;

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

        public void Info(string message)
        {
            _eventLog.WriteEntry(message, EventLogEntryType.Information);
        }

        public void Warning(string message)
        {
            _eventLog.WriteEntry(message, EventLogEntryType.Warning);
        }

        public void Error(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}\n{ex}" : message;
            _eventLog.WriteEntry(fullMessage, EventLogEntryType.Error);
        }
    }

}
