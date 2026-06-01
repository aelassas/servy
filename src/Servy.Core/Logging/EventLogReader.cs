using Servy.Core.DTOs;
using Servy.Core.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Wraps <see cref="System.Diagnostics.Eventing.Reader.EventLogReader"/> to allow mocking in unit tests.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EventLogReader : IEventLogReader
    {
        ///<inheritdoc/>
        public IEnumerable<ServyEventLogEntry> ReadEvents(EventLogQuery query, int maxReadCount)
        {
            using (var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query))
            {
                int processedCount = 0;

                // Use a nullable EventRecord and wrap the entire loop body in 'using'
                for (EventRecord? evt = reader.ReadEvent(); evt != null && processedCount < maxReadCount; evt = reader.ReadEvent())
                {
                    using (evt)
                    {
                        ServyEventLogEntry dto = MapToDto(evt);
                        processedCount++;
                        yield return dto;
                    }
                }
            }
        }

        /// <summary>
        /// Maps a native <see cref="EventRecord"/> to a managed <see cref="ServyEventLogEntry"/> DTO.
        /// </summary>
        /// <remarks>
        /// This method must be called while the <paramref name="evt"/> object is still active 
        /// and its parent <see cref="System.Diagnostics.Eventing.Reader.EventLogReader"/> has not been disposed, 
        /// as <see cref="EventRecord.FormatDescription()"/> requires an active handle to the event metadata provider.
        /// </remarks>
        /// <param name="evt">The raw event record retrieved from the Windows Event Log.</param>
        /// <returns>
        /// A populated <see cref="ServyEventLogEntry"/> containing the formatted message,
        /// event id, level, provider name and timestamp pulled from <paramref name="evt"/>.
        /// </returns>
        private static ServyEventLogEntry MapToDto(EventRecord evt)
        {
            int eventId = 0;
            DateTimeOffset time = DateTimeOffset.MinValue;
            EventLogLevel level = EventLogLevel.Information;
            string? provider = null;
            string message;

            try { eventId = evt.Id; } catch (EventLogException) { /* leave default */ }
            try { time = SafeToOffset(evt.TimeCreated); } catch (EventLogException) { /* leave default */ }
            try { level = ParseLevel(evt.Level ?? 0); } catch (EventLogException) { /* leave default */ }
            try { provider = evt.ProviderName; } catch (EventLogException) { provider = "<unavailable>"; }

            try { message = evt.FormatDescription() ?? string.Empty; }
            catch (Exception ex) when (ex is EventLogException || ex is InvalidOperationException) { message = $"<unavailable: {ex.Message}>"; }

            return new ServyEventLogEntry { EventId = eventId, Time = time, Level = level, ProviderName = provider, Message = message };
        }

        /// <summary>
        /// Converts a raw event log level (byte) into a strongly typed <see cref="EventLogLevel"/>.
        /// </summary>
        /// <param name="level">The raw level value from the event record.</param>
        /// <returns>
        /// The corresponding <see cref="EventLogLevel"/> value. 
        /// Defaults to <see cref="EventLogLevel.Information"/> if the level is unknown.
        /// </returns>
        /// <remarks>
        /// Maps Windows Event Log levels into the application's <see cref="EventLogLevel"/> taxonomy.
        /// Note: Critical (level 1) is intentionally folded into <see cref="EventLogLevel.Error"/>
        /// because the consuming filter contract (see LogsViewModel.GetLogLevels) does not surface
        /// a distinct Critical bucket. Verbose is preserved.
        /// </remarks>
        public static EventLogLevel ParseLevel(byte level)
        {
            switch (level)
            {
                case 0: // LogAlways - explicit "always emit" sentinel, treat as informational
                    return EventLogLevel.Information;
                case 1:  // Critical - fold into Error to match the filter contract
                    return EventLogLevel.Error;
                case 2:
                    return EventLogLevel.Error;
                case 3:
                    return EventLogLevel.Warning;
                case 4:
                    return EventLogLevel.Information;
                case 5:
                    return EventLogLevel.Verbose;
                default:
                    // Log the unknown level at a debug level to assist in future triage 
                    // without flooding production logs.
                    Logger.Debug($"Unknown event log level '{level}' encountered; collapsing to Information.");
                    return EventLogLevel.Information;
            }
        }

        /// <summary>
        /// Safely converts a nullable <see cref="DateTime"/> to a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="raw">The source <see cref="DateTime"/> to convert.</param>
        /// <returns>
        /// The converted <see cref="DateTimeOffset"/>; or <see cref="DateTimeOffset.MinValue"/> if the input is null.
        /// </returns>
        /// <remarks>
        /// .evtx timestamps are persisted as UTC FILETIMEs. This method explicitly coerces the input 
        /// <see cref="DateTimeKind"/> to <see cref="DateTimeKind.Utc"/> before conversion to prevent 
        /// local-offset arithmetic, which can trigger an <see cref="ArgumentOutOfRangeException"/> 
        /// for values approaching <see cref="DateTime.MinValue"/>.
        /// </remarks>
        private static DateTimeOffset SafeToOffset(DateTime? raw)
        {
            if (!raw.HasValue) return DateTimeOffset.MinValue;

            // .evtx timestamps are persisted as UTC FILETIMEs; coerce Kind to remove the
            // local-offset arithmetic that overflows for near-MinValue inputs.
            var utc = DateTime.SpecifyKind(raw.Value, DateTimeKind.Utc);
            return new DateTimeOffset(utc, TimeSpan.Zero);
        }
    }
}