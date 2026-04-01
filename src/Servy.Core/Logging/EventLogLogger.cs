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
        #region Private Fields

        private readonly EventLog _eventLog;
        private LogLevel _currentLogLevel = LogLevel.Info;

        #endregion

        #region Constants

        // Default Event IDs per level
        private const int InfoEventId = 1000;
        private const int WarningEventId = 2000;
        private const int ErrorEventId = 3000;

        #endregion

        #region ILogger implementation

        ///<inheritdoc/>
        public string Prefix { get; set; }

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
        public void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
        }

        /// <inheritdoc/>
        public void Debug(string message, Exception ex = null)
        {
            if (_currentLogLevel <= LogLevel.Debug)
            {
                var fullMessage = Format(ex != null ? $"{message}\n{ex}" : message);
                // We skip the Event Log for Debug, but keep the File Log
                Logger.Debug(fullMessage);
            }
        }

        /// <inheritdoc/>
        public void Info(string message)
        {
            if (_currentLogLevel <= LogLevel.Info)
            {
                var formattedMessage = Format(message);
                _eventLog.WriteEntry(formattedMessage, EventLogEntryType.Information, InfoEventId);
                Logger.Info(formattedMessage);
            }
        }

        /// <inheritdoc/>
        public void Warn(string message)
        {
            if (_currentLogLevel <= LogLevel.Warn)
            {
                var formattedMessage = Format(message);
                _eventLog.WriteEntry(formattedMessage, EventLogEntryType.Warning, WarningEventId);
                Logger.Warn(formattedMessage);
            }
        }

        /// <inheritdoc/>
        public void Error(string message, Exception ex = null)
        {
            if (_currentLogLevel <= LogLevel.Error)
            {
                var fullMessage = Format(ex != null ? $"{message}\n{ex}" : message);
                _eventLog.WriteEntry(fullMessage, EventLogEntryType.Error, ErrorEventId);
                Logger.Error(fullMessage);
            }
        }

        #endregion

        #region IDisposable implementation

        private bool _disposed = false;

        /// <summary>
        /// Releases all resources used by the <see cref="EventLogLogger"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Free managed objects here
                _eventLog?.Dispose();
            }

            _disposed = true;
        }

        #endregion

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
