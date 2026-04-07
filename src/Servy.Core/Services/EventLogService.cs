using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
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
                    //
                    // All evt.* accesses must stay inside the using block to avoid ObjectDisposedException
                    //
                    token.ThrowIfCancellationRequested();

                    var message = evt.Message?? string.Empty;

                    // Only include Servy service logs: messages with [..]
                    if (message.IndexOf("[", StringComparison.OrdinalIgnoreCase) < 0 ||
                        message.IndexOf("]", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!string.IsNullOrEmpty(keyword) &&
                        message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    results.Add(evt);
                }

                return results
                    .OrderByDescending(r => r.Time)
                    .ToList();
            }, token);
        }

    }
}
