using Servy.Core.DTOs;
using Servy.Core.Enums;
using System;
using System.Collections.Generic;
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

                for (EventRecord evt = reader.ReadEvent(); evt != null; evt = reader.ReadEvent())
                {
                    // Hard stop: Stop reading from the Windows SCM cursor immediately
                    if (processedCount >= maxReadCount) yield break;

                    ServyEventLogEntry dto;
                    using (evt)
                    {
                        dto = MapToDto(evt);
                    }

                    processedCount++;
                    yield return dto;
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
            var message = evt.FormatDescription() ?? string.Empty;

            return new ServyEventLogEntry
            {
                EventId = evt.Id,
                Time = evt.TimeCreated ?? DateTime.MinValue,
                Level = ParseLevel(evt.Level ?? 0),
                ProviderName = evt.ProviderName,
                Message = message,
            };
        }

        /// <summary>
        /// Converts a raw event log level (byte) into a strongly typed <see cref="EventLogLevel"/>.
        /// </summary>
        /// <param name="level">The raw level value from the event record.</param>
        /// <returns>The corresponding <see cref="EventLogLevel"/> value. Defaults to <see cref="EventLogLevel.Information"/> if the level is unknown.</returns>
        public static EventLogLevel ParseLevel(byte level)
        {
            switch (level)
            {
                case 1:
                    return EventLogLevel.Error;  // Critical -> map to Error (closest match)
                case 2:
                    return EventLogLevel.Error;
                case 3:
                    return EventLogLevel.Warning;
                case 4:
                    return EventLogLevel.Information;
                default:
                    return EventLogLevel.Information;
            }
        }
    }
}
