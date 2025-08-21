using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides functionality to query Windows Event Viewer logs.
    /// </summary>
    public class EventLogService : IEventLogService
    {
        private static readonly string LogName = "Application";
        private static readonly string SourceName = AppConfig.ServiceNameEventSource;

        private readonly IEventLogReader _reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogService"/> class.
        /// </summary>
        /// <param name="reader">
        /// The <see cref="IEventLogReader"/> used to read events from the Windows Event Viewer.
        /// </param>
        public EventLogService(IEventLogReader reader)
        {
            _reader = reader;
        }

        /// <inheritdoc />
        public IEnumerable<EventLogEntry> Search(EventLogLevel? level, DateTime? startDate, DateTime? endDate, string keyword)
        {
            // Build query dynamically based on parameters
            string timeFilter = string.Empty;
            if (startDate.HasValue)
            {
                timeFilter = $@"TimeCreated[@SystemTime >= '{startDate.Value.ToUniversalTime():o}']";
            }
            if (endDate.HasValue)
            {
                if (!string.IsNullOrEmpty(timeFilter))
                {
                    timeFilter += " and ";
                }
                timeFilter += $@"TimeCreated[@SystemTime <= '{endDate.Value.ToUniversalTime():o}']";
            }

            string keywordFilter = string.Empty;
            if (!string.IsNullOrEmpty(keyword))
            {
                keywordFilter = $@"
                    *[EventData[
                        contains(Data, '{keyword}') or contains(*, '{keyword}')
                    ]]
                ";
            }

            string levelFilter = string.Empty;
            if (level.HasValue)
            {
                levelFilter = $@"Level={(int)level.Value}";
            }

            string query = $@"
                *[System[
                    Provider[@Name='{SourceName}']
                    {(string.IsNullOrEmpty(levelFilter) ? "" : "and " + levelFilter)}
                    {(string.IsNullOrEmpty(timeFilter) ? "" : "and " + timeFilter)}
                ]]
                {keywordFilter}
            ";

            var eventQuery = new EventLogQuery(LogName, PathType.LogName, query);
            var records = _reader.ReadEvents(eventQuery);

            var results = new List<EventLogEntry>();
            foreach (var evt in records)
            {
                results.Add(new EventLogEntry
                {
                    EventId = evt.Id,
                    Time = evt.TimeCreated ?? DateTime.MinValue,
                    Level = ParseLevel(evt.Level ?? 0),
                    Message = evt.FormatDescription()
                });
            }

            return results;
        }

        /// <summary>
        /// Converts a raw event log level (byte) into a strongly typed <see cref="EventLogLevel"/>.
        /// </summary>
        /// <param name="level">The raw level value from the event record.</param>
        /// <returns>The corresponding <see cref="EventLogLevel"/> value. Defaults to <see cref="EventLogLevel.Information"/> if the level is unknown.</returns>
        private static EventLogLevel ParseLevel(byte level)
        {
            switch (level)
            {
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
