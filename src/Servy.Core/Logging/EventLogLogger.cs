using Servy.Core.Config;
using System.Diagnostics;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Logs messages to the Windows Event Log with Event IDs.
    /// </summary>
    public class EventLogLogger : IServyLogger
    {
        #region Private Fields

        private EventLog? _eventLog;
        private readonly object _eventLogLock = new object();

        // Volatile backing fields ensure thread visibility when updated dynamically
        private volatile int _currentLogLevel;
        private volatile bool _isEventLogEnabled;

        private readonly string _source;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether logging to the Windows Event Log is enabled.
        /// </summary>
        public bool IsEventLogEnabled => _isEventLogEnabled;

        #endregion

        #region ILogger implementation

        /// <inheritdoc/>
        public string? Prefix { get; }

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
            string? prefix = null)
        {
            _source = source;
            _isEventLogEnabled = isEventLogEnabled;
            _currentLogLevel = (int)level;

            // Enforce clean layout validation if a top-level global instance prefix is supplied.
            Prefix = string.IsNullOrWhiteSpace(prefix) ? null : $"[{prefix.Trim()}]";

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
            lock (_eventLogLock)
            {
                if (isEnabled)
                {
                    if (_eventLog == null)
                    {
                        InitializeEventLog();
                        // InitializeEventLog sets _isEventLogEnabled to false on failure.
                    }
                    else
                    {
                        _isEventLogEnabled = true;
                    }
                }
                else
                {
                    _isEventLogEnabled = false;

                    // CLEANUP: Dispose and null the handle when logging is explicitly turned off.
                    _eventLog?.Dispose();
                    _eventLog = null;
                }
            }
        }

        /// <inheritdoc/>
        public IServyLogger CreateScoped(string? prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return this;
            }

            string sanitizedSegment = ScopedEventLogLogger.SanitizePrefixSegment(prefix);
            string combined = string.IsNullOrWhiteSpace(Prefix)
                ? $"[{sanitizedSegment}]"
                : $"{Prefix} [{sanitizedSegment}]";

            return new ScopedEventLogLogger(this, combined, (LogLevel)_currentLogLevel, _isEventLogEnabled);
        }

        /// <inheritdoc/>
        public void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = (int)level;
        }

        /// <inheritdoc/>
        public void Debug(string message, Exception? ex = null)
        {
            if ((LogLevel)_currentLogLevel <= LogLevel.Debug)
            {
                // Debug logs are traditionally skipped for Event Log to avoid clutter, 
                // but we always keep the File Log.
                Logger.Debug(Format(message), ex);
            }
        }

        /// <inheritdoc/>
        public void Info(string message, Exception? ex = null)
        {
            WriteLeveled(LogLevel.Info, EventLogEntryType.Information, EventIds.Info, message, ex, Format, _isEventLogEnabled, Logger.Info);
        }

        /// <inheritdoc/>
        public void Warn(string message, Exception? ex = null)
        {
            WriteLeveled(LogLevel.Warn, EventLogEntryType.Warning, EventIds.Warning, message, ex, Format, _isEventLogEnabled, Logger.Warn);
        }

        /// <inheritdoc/>
        public void Error(string message, Exception? ex = null)
        {
            WriteLeveled(LogLevel.Error, EventLogEntryType.Error, EventIds.Error, message, ex, Format, _isEventLogEnabled, Logger.Error);
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
                lock (_eventLogLock)
                {
                    _eventLog?.Dispose();
                    _eventLog = null;
                }
            }

            _disposed = true;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Centralized parameterized log pipeline handler to eliminate code duplication across log severity variants.
        /// Orchestrates the conditional flow between the Windows Event Log sink and the secondary file system logger, 
        /// while enforcing thread-local log level thresholds and standardized message formatting.
        /// </summary>
        /// <param name="targetLevel">The <see cref="LogLevel"/> required for this entry to be processed.</param>
        /// <param name="entryType">The <see cref="EventLogEntryType"/> to categorize the Windows Event Log entry.</param>
        /// <param name="eventId">The application-specific ID for the log entry.</param>
        /// <param name="message">The primary log text content.</param>
        /// <param name="ex">An optional <see cref="Exception"/> to be appended to the log.</param>
        /// <param name="formatSelector">A delegate used to apply instance-specific prefixing or structural sanitization to the message.</param>
        /// <param name="isEventLogSinkEnabled">Indicates whether the Windows Event Log sink is active for the current logger scope.</param>
        /// <param name="fileSink">The action delegate (e.g., <see cref="Logger.Info"/>) representing the file-based logging destination.</param>
        private void WriteLeveled(
            LogLevel targetLevel,
            EventLogEntryType entryType,
            int eventId,
            string message,
            Exception? ex,
            Func<string, string> formatSelector,
            bool isEventLogSinkEnabled,
            Action<string, Exception?> fileSink)
        {
            if ((LogLevel)_currentLogLevel <= targetLevel)
            {
                var fullMessage = formatSelector(ex != null ? $"{message}\n{ex}" : message);
                if (isEventLogSinkEnabled)
                {
                    SafeWriteToEventLog(fullMessage, entryType, eventId);
                }
                fileSink(formatSelector(message), ex);
            }
        }

        /// <summary>
        /// Safely writes an entry to the Windows Event Log, truncating oversized messages
        /// and catching any transient I/O exceptions so the pipeline continues to file logging.
        /// </summary>
        /// <param name="message">The formatted log message.</param>
        /// <param name="type">The severity level of the event.</param>
        /// <param name="eventId">The application-specific event identifier.</param>
        private void SafeWriteToEventLog(string message, EventLogEntryType type, int eventId)
        {
            EventLog? captured;
            lock (_eventLogLock)
            {
                captured = _eventLog;
            }

            if (captured == null) return;

            try
            {
                var safeMessage = message.Length > AppConfig.EventLogMessageMaxChars
                    ? message.Substring(0, AppConfig.EventLogMessageMaxChars) + "...[truncated]"
                    : message;

                captured.WriteEntry(safeMessage, type, eventId);
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
                else
                {
                    var currentLog = EventLog.LogNameFromSourceName(_source, ".");
                    if (!string.Equals(currentLog, AppConfig.EventLogName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Re-register requires DeleteEventSource + CreateEventSource (admin only).
                        // At minimum, refuse silent misrouting and tell the operator.
                        _isEventLogEnabled = false;
                        Logger.Error(
                            $"Event source '{_source}' is already registered to log '{currentLog}', " +
                            $"not '{AppConfig.EventLogName}'. Refusing to write to the wrong log - " +
                            $"falling back to file-only logging. Run as admin and delete/recreate the source to fix.");
                        return;
                    }
                }

                _eventLog = new EventLog
                {
                    Log = AppConfig.EventLogName,
                    Source = _source,
                };

                _isEventLogEnabled = true; // Ensure flag matches successful initialization
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
        protected virtual string Format(string message)
        {
            // Since Prefix is now immutable, this is thread-safe.
            return string.IsNullOrWhiteSpace(Prefix) ? message : $"{Prefix} {message}";
        }

        #endregion

        #region ScopedLogger

        /// <summary>
        /// A lightweight wrapper that delegates logging to the parent <see cref="EventLogLogger"/> instance.
        /// This prevents allocating a new native <see cref="EventLog"/> handle for every scope creation.
        /// </summary>
        /// <remarks>
        /// Unlike the parent logger, scoped instances maintain their own independent LogLevel and enabled status.
        /// Exception: enabling event logging on a scope also (re-)enables the parent, because the parent owns
        /// the native EventLog handle ("self-heal"). Disabling a scope never affects the parent.
        /// </remarks>
        private sealed class ScopedEventLogLogger : IServyLogger
        {
            private readonly EventLogLogger _parent;
            private volatile int _currentLogLevel;
            private volatile bool _isEventLogEnabled;

            /// <inheritdoc/>
            public string? Prefix { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ScopedEventLogLogger"/> class.
            /// </summary>
            /// <param name="parent">The parent logger instance that holds the unmanaged EventLog handle.</param>
            /// <param name="prefix">The prefix for this scoped logger.</param>
            /// <param name="level">The active structural log level baseline context applied for filtering.</param>
            /// <param name="isEventLogEnabled">Specifies the initial pipeline tracking state for the Event Log sink.</param>
            public ScopedEventLogLogger(EventLogLogger parent, string prefix, LogLevel level, bool isEventLogEnabled)
            {
                _parent = parent;
                Prefix = prefix;

                // Structural configuration properties are bound atomically directly from the parameter pass 
                // instead of performing multi-phase updates that cascade mutations back to the core parent layer.
                _currentLogLevel = (int)level;
                _isEventLogEnabled = isEventLogEnabled;
            }

            /// <inheritdoc/>
            public void Debug(string message, Exception? ex = null)
            {
                if ((LogLevel)_currentLogLevel <= LogLevel.Debug)
                {
                    Logger.Debug(Format(message), ex);
                }
            }

            /// <inheritdoc/>
            public void Info(string message, Exception? ex = null)
            {
                _parent.WriteLeveled(LogLevel.Info, EventLogEntryType.Information, EventIds.Info, message, ex, Format, _isEventLogEnabled, Logger.Info);
            }

            /// <inheritdoc/>
            public void Warn(string message, Exception? ex = null)
            {
                _parent.WriteLeveled(LogLevel.Warn, EventLogEntryType.Warning, EventIds.Warning, message, ex, Format, _isEventLogEnabled, Logger.Warn);
            }

            /// <inheritdoc/>
            public void Error(string message, Exception? ex = null)
            {
                _parent.WriteLeveled(LogLevel.Error, EventLogEntryType.Error, EventIds.Error, message, ex, Format, _isEventLogEnabled, Logger.Error);
            }

            /// <inheritdoc/>
            public void SetLogLevel(LogLevel level)
            {
                _currentLogLevel = (int)level;
            }

            /// <inheritdoc/>
            public void SetIsEventLogEnabled(bool isEnabled)
            {
                if (isEnabled) _parent.SetIsEventLogEnabled(true);   // self-heal via parent
                _isEventLogEnabled = isEnabled;
            }

            /// <summary>
            /// No-op implementation. The parent <see cref="EventLogLogger"/> owns the unmanaged resources.
            /// </summary>
            public void Dispose() { /* no-op; parent owns the EventLog */ }

            /// <inheritdoc/>
            public IServyLogger CreateScoped(string? prefix) => CreateScopedInstance(prefix);

            /// <summary>
            /// Factory method to construct a new scoped logger instance, ensuring proper prefix hierarchy 
            /// and inheriting current operational settings (log level, enabled status) from the current scope.
            /// </summary>
            /// <param name="prefix">The logging label for the new scope; will be sanitized for structural safety.</param>
            /// <returns>
            /// An <see cref="IServyLogger"/> representing the child scope, or <c>this</c> if the prefix is null or whitespace.
            /// </returns>
            private IServyLogger CreateScopedInstance(string? prefix)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    return this;
                }

                // Sanitize the segment to prevent user-supplied brackets from breaking log text token parsing.
                string sanitizedSegment = SanitizePrefixSegment(prefix);

                // Each segment is wrapped in its own brackets; user-supplied brackets are sanitized to parentheses.
                string combined = string.IsNullOrWhiteSpace(Prefix)
                    ? $"[{sanitizedSegment}]"
                    : $"{Prefix} [{sanitizedSegment}]";

                // Leverages explicit multi-argument constructor pass ensuring clean context inheritance 
                // down to secondary nested downstream client contexts.
                return new ScopedEventLogLogger(_parent, combined, (LogLevel)_currentLogLevel, _isEventLogEnabled);
            }

            /// <summary>
            /// Replaces nested or structural bracket symbols with parenthetical characters to guarantee string tokenization integrity.
            /// </summary>
            internal static string SanitizePrefixSegment(string segment)
            {
                if (string.IsNullOrWhiteSpace(segment)) return string.Empty;
                return segment.Replace('[', '(').Replace(']', ')').Trim();
            }

            /// <summary>
            /// Formats a log message by prepending the pre-constructed <see cref="Prefix"/> if it is set.
            /// </summary>
            /// <param name="message">The original log message.</param>
            /// <returns>The formatted message with structural prefixes if available.</returns>
            private string Format(string message)
            {
                // Prefix is now pre-wrapped safely in its own bracket sets (e.g., "[A]" or "[A] [B]")
                // so Format simply appends it directly without performing outer re-wrapping logic.
                return string.IsNullOrWhiteSpace(Prefix) ? message : $"{Prefix} {message}";
            }
        }

        #endregion
    }
}