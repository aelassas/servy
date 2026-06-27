using Servy.Core.DTOs;
using Servy.Core.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Wraps <see cref="System.Diagnostics.Eventing.Reader.EventLogReader"/> to allow mocking in unit tests.
    /// </summary>
    public class EventLogReader : IEventLogReader
    {
        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public IEnumerable<ServyEventLogEntry> ReadEvents(EventLogQuery query, int maxReadCount)
        {
            using (var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query))
            {
                int processedCount = 0;

                // Explicit count constraint evaluation at the gate prevents 
                // reading the (maxReadCount + 1)th record entirely.
                while (processedCount < maxReadCount)
                {
                    EventRecord? evt = reader.ReadEvent();
                    if (evt == null)
                    {
                        yield break;
                    }

                    // Enforces immediate context disposal of the native EVT_HANDLE 
                    // as soon as the mapped data transfer object is yielded.
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
        internal static ServyEventLogEntry MapToDto(EventRecord evt)
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
        /// a distinct Critical bucket. Verbose is folded into <see cref="EventLogLevel.Information"/> 
        /// to match the filter contract.
        /// </remarks>
        public static EventLogLevel ParseLevel(byte level)
        {
            switch (level)
            {
                case 0: 
                    return EventLogLevel.Information; // LogAlways - explicit "always emit" sentinel, treat as informational
                case 1:
                    return EventLogLevel.Error; // Critical - fold into Error to match the filter contract
                case 2:
                    return EventLogLevel.Error;
                case 3:
                    return EventLogLevel.Warning;
                case 4:
                    return EventLogLevel.Information;
                case 5:
                    return EventLogLevel.Information; // Verbose - fold into Information to match the filter contract
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
        /// .evtx timestamps are persisted as UTC FILETIMEs. This method evaluates the incoming 
        /// <see cref="DateTimeKind"/> to preserve correct offsets while preventing local-offset 
        /// arithmetic overflows for values approaching <see cref="DateTime.MinValue"/>.
        /// </remarks>
        internal static DateTimeOffset SafeToOffset(DateTime? raw)
        {
            if (!raw.HasValue) return DateTimeOffset.MinValue;

            // Guard against edge cases near MinValue that would overflow during local offset shifts
            if (raw.Value < DateTime.MinValue.AddDays(1))
                return DateTimeOffset.MinValue;

            // If the framework passes it as explicitly UTC, 
            // project it directly with a Zero offset to satisfy the UTC contract.
            if (raw.Value.Kind == DateTimeKind.Utc )
            {
                return new DateTimeOffset(DateTime.SpecifyKind(raw.Value, DateTimeKind.Utc), TimeSpan.Zero);
            }

            // Otherwise, handle it safely as local system time (observed as UTC+1 during local non-DST)
            return new DateTimeOffset(raw.Value);
        }
    }
}