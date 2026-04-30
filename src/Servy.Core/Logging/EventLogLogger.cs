using Servy.Core.Config;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Logs messages to the Windows Event Log with Event IDs.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EventLogLogger : IServyLogger
    {
        #region Private Fields

        // The Windows Event Log has a strict per-entry size limit (approx 31,839 chars).
        // We truncate at 31,000 to leave a comfortable safety margin for Unicode bytes.
        private const int EventLogMessageMaxChars = 31000;

        private EventLog _eventLog;
        private LogLevel _currentLogLevel;
        private bool _isEventLogEnabled;
        private readonly string _source;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether logging to the Windows Event Log is enabled.
        /// </summary>
        public bool IsEventLogEnabled => _isEventLogEnabled;

        #endregion

        #region ILogger implementation

        ///<inheritdoc/>
        public string Prefix { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogLogger"/> class.
        /// </summary>
        /// <param name="source">The event source name used for logging.</param>
        /// <param name="level">Log level. Messages below this level are ignored.</param>
        /// <param name="isEventLogEnabled">Whether to enable writing to Windows Event Log.</param>
        /// <param name="prefix">Optional immutable prefix for this logger instance.</param>
        public EventLogLogger(
            string source,
            LogLevel level = LogLevel.Info,
            bool isEventLogEnabled = true,
            string prefix = null)
        {
            _source = source;
            _isEventLogEnabled = isEventLogEnabled;
            _currentLogLevel = level;
            Prefix = prefix; // Immutable assignment

            if (_isEventLogEnabled)
            {
                InitializeEventLog();
            }
        }

        /// <summary>
        /// Enables or disables the Windows Event Log writing.
        /// </summary>
        /// <param name="isEnabled">True to enable, false to disable.</param>
        public void SetIsEventLogEnabled(bool isEnabled)
        {
            if (isEnabled && _eventLog == null)
            {
                InitializeEventLog();
            }
            _isEventLogEnabled = isEnabled;
        }

        /// <inheritdoc/>
        public IServyLogger CreateScoped(string prefix)
        {
            // Inherit the parent's settings but apply the new immutable prefix.
            // If we want nested prefixes (e.g., [Parent][Child]), use:
            // var newPrefix = string.IsNullOrEmpty(Prefix) ? prefix : $"{Prefix}][{prefix}";

            return new EventLogLogger(
                _source,
                _currentLogLevel,
                _isEventLogEnabled,
                prefix
            );
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
                // Debug logs are traditionally skipped for Event Log to avoid clutter, 
                // but we always keep the File Log.
                Logger.Debug(fullMessage);
            }
        }

        /// <inheritdoc/>
        public void Info(string message)
        {
            if (_currentLogLevel <= LogLevel.Info)
            {
                var formattedMessage = Format(message);
                if (_isEventLogEnabled)
                {
                    SafeWriteToEventLog(formattedMessage, EventLogEntryType.Information, EventIds.Info);
                }
                Logger.Info(formattedMessage);
            }
        }

        /// <inheritdoc/>
        public void Warn(string message)
        {
            if (_currentLogLevel <= LogLevel.Warn)
            {
                var formattedMessage = Format(message);
                if (_isEventLogEnabled)
                {
                    SafeWriteToEventLog(formattedMessage, EventLogEntryType.Warning, EventIds.Warning);
                }
                Logger.Warn(formattedMessage);
            }
        }

        /// <inheritdoc/>
        public void Error(string message, Exception ex = null)
        {
            if (_currentLogLevel <= LogLevel.Error)
            {
                var fullMessage = Format(ex != null ? $"{message}\n{ex}" : message);
                if (_isEventLogEnabled)
                {
                    SafeWriteToEventLog(fullMessage, EventLogEntryType.Error, EventIds.Error);
                }
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
                _eventLog?.Dispose();
            }

            _disposed = true;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Safely writes an entry to the Windows Event Log, truncating oversized messages
        /// and catching any transient I/O exceptions so the pipeline continues to file logging.
        /// </summary>
        /// <param name="message">The formatted log message.</param>
        /// <param name="type">The severity level of the event.</param>
        /// <param name="eventId">The application-specific event identifier.</param>
        private void SafeWriteToEventLog(string message, EventLogEntryType type, int eventId)
        {
            try
            {
                var safeMessage = message.Length > EventLogMessageMaxChars
                    ? message.Substring(0, EventLogMessageMaxChars) + "...[truncated]"
                    : message;

                _eventLog?.WriteEntry(safeMessage, type, eventId);
            }
            catch (Exception ex)
            {
                Logger.Warn($"EventLog write failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the EventLog component and ensures the event source exists.
        /// </summary>
        private void InitializeEventLog()
        {
            try
            {
                if (!EventLog.SourceExists(_source))
                {
                    EventLog.CreateEventSource(_source, AppConfig.EventLogName);
                }

                _eventLog = new EventLog
                {
                    Log = AppConfig.EventLogName,
                    Source = _source,
                };
            }
            catch (Exception ex)
            {
                // If we fail to initialize (e.g. lack of admin rights), 
                // we fall back to file-only logging.
                _isEventLogEnabled = false;
                Logger.Error($"Failed to initialize Windows Event Log for source '{_source}'. Falling back to file-only logging.", ex);
            }
        }

        /// <summary>
        /// Formats a log message by prepending the <see cref="Prefix"/> if it is set.
        /// </summary>
        /// <param name="message">The original log message.</param>
        /// <returns>The formatted message with prefix if available.</returns>
        private string Format(string message)
        {
            // Since Prefix is now immutable, this is thread-safe.
            return string.IsNullOrWhiteSpace(Prefix) ? message : $"[{Prefix}] {message}";
        }

        #endregion
    }
}