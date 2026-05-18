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
                for (EventRecord? evt = reader.ReadEvent(); evt != null; evt = reader.ReadEvent())
                {
                    using (evt)
                    {
                        // Check the limit inside the using block so 'evt' is disposed on break
                        if (processedCount >= maxReadCount) yield break;

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
            string message;
            try
            {
                message = evt.FormatDescription() ?? string.Empty;
            }
            catch (Exception ex) when (ex is EventLogException || ex is InvalidOperationException)
            {
                message = $"<unavailable: {ex.Message}>";
            }

            return new ServyEventLogEntry
            {
                EventId = evt.Id,
                // Avoid implicit conversion from DateTime.MinValue.
                // We treat the event timestamp as local on this OS and fallback directly to DateTimeOffset.MinValue 
                // to prevent ArgumentOutOfRangeException in positive-offset time zones.
                Time = evt.TimeCreated.HasValue
                    ? new DateTimeOffset(evt.TimeCreated.Value)
                    : DateTimeOffset.MinValue,
                Level = ParseLevel(evt.Level ?? 0),
                ProviderName = evt.ProviderName,
                Message = message,
            };
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
        /// This method preserves the fidelity of Windows Event Log levels. 
        /// High-severity 'Critical' events and low-severity 'Verbose' events are mapped 
        /// explicitly to prevent information loss during DTO conversion.
        /// </remarks>
        public static EventLogLevel ParseLevel(byte level)
        {
            switch (level)
            {
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
    }
}