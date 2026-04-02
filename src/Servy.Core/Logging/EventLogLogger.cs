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

        private EventLog? _eventLog;
        private LogLevel _currentLogLevel;
        private bool _isEventLogEnabled;
        private readonly string _source;

        #endregion

        #region Constants

        // Default Event IDs per level
        private const int InfoEventId = 1000;
        private const int WarningEventId = 2000;
        private const int ErrorEventId = 3000;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether logging to the Windows Event Log is enabled.
        /// </summary>
        public bool IsEventLogEnabled => _isEventLogEnabled;

        #endregion

        #region ILogger implementation

        ///<inheritdoc/>
        public string? Prefix { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogLogger"/> class.
        /// </summary>
        /// <param name="source">The event source name used for logging.</param>
        /// <param name="level">Log level to set. Defaults to <see cref="LogLevel.Info"/>. Messages below this level will be ignored.</param>
        /// <param name="isEventLogEnabled">Whether to enable writing to the Windows Event Log. Defaults to <c>true</c>.</param>
        public EventLogLogger(string source, LogLevel level = LogLevel.Info, bool isEventLogEnabled = true)
        {
            _source = source;
            _isEventLogEnabled = isEventLogEnabled;
            _currentLogLevel = level;

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
        public void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
        }

        /// <inheritdoc/>
        public void Debug(string message, Exception? ex = null)
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
                    _eventLog?.WriteEntry(formattedMessage, EventLogEntryType.Information, InfoEventId);
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
                    _eventLog?.WriteEntry(formattedMessage, EventLogEntryType.Warning, WarningEventId);
                }
                Logger.Warn(formattedMessage);
            }
        }

        /// <inheritdoc/>
        public void Error(string message, Exception? ex = null)
        {
            if (_currentLogLevel <= LogLevel.Error)
            {
                var fullMessage = Format(ex != null ? $"{message}\n{ex}" : message);
                if (_isEventLogEnabled)
                {
                    _eventLog?.WriteEntry(fullMessage, EventLogEntryType.Error, ErrorEventId);
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
        /// Initializes the EventLog component and ensures the event source exists.
        /// </summary>
        private void InitializeEventLog()
        {
            try
            {
                if (!EventLog.SourceExists(_source))
                {
                    EventLog.CreateEventSource(_source, "Application");
                }

                _eventLog = new EventLog()
                {
                    Log = "Application",
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
            return string.IsNullOrEmpty(Prefix) ? message : $"[{Prefix}] {message}";
        }

        #endregion
    }
}