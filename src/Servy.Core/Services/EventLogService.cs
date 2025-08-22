using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        public async Task<IEnumerable<EventLogEntry>> SearchAsync(
            EventLogLevel? level,
            DateTime? startDate,
            DateTime? endDate,
            string keyword,
            CancellationToken token = default)
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                // Build System filters
                var systemFilters = new List<string>();

                if (!string.IsNullOrEmpty(SourceName))
                    systemFilters.Add($"Provider[@Name='{SourceName}']");

                if (level.HasValue && level != EventLogLevel.All)
                    systemFilters.Add($"Level={(int)level.Value}");

                if (startDate.HasValue)
                {
                    var startUtc = startDate.Value.Date.ToUniversalTime();
                    systemFilters.Add($"TimeCreated[@SystemTime >= '{startUtc:o}']");
                }

                if (endDate.HasValue)
                {
                    var endUtc = endDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
                    systemFilters.Add($"TimeCreated[@SystemTime <= '{endUtc:o}']");
                }

                string systemFilterString = string.Join(" and ", systemFilters);
                string query = $"*[System[{systemFilterString}]]";

                var eventQuery = new EventLogQuery(LogName, PathType.LogName, query);
                var records = _reader.ReadEvents(eventQuery);

                var results = new List<EventLogEntry>();

                foreach (var evt in records)
                {
                    token.ThrowIfCancellationRequested();

                    var message = evt.FormatDescription() ?? string.Empty;

                    // Only include Servy service logs: messages with [..]
                    if (message.IndexOf("[", StringComparison.OrdinalIgnoreCase) < 0 ||
                        message.IndexOf("]", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!string.IsNullOrEmpty(keyword) &&
                        message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    results.Add(new EventLogEntry
                    {
                        EventId = evt.Id,
                        Time = evt.TimeCreated ?? DateTime.MinValue,
                        Level = ParseLevel(evt.Level ?? 0),
                        Message = message
                    });
                }

                return results
                    .OrderByDescending(r => r.Time)
                    .ToList();
            }, token);
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
