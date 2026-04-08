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
        private const int MaxResults = 10_000;

        private readonly string _sourceName;
        private readonly IEventLogReader _reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogService"/> class.
        /// </summary>
        /// <param name="reader">
        /// The <see cref="IEventLogReader"/> used to read events from the Windows Event Viewer.
        /// </param>
        /// <param name="sourceName">
        /// The optional event source name to filter by. If <see langword="null"/>, 
        /// the service defaults to the value defined in <see cref="AppConfig.ServiceNameEventSource"/>.
        /// Pass an empty string to disable the provider filter and enable wildcard querying.
        /// </param>
        public EventLogService(IEventLogReader reader, string sourceName = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _sourceName = sourceName ?? AppConfig.ServiceNameEventSource;
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

                var systemFilters = new List<string>();

                // ESCAPE SOURCE NAME
                if (!string.IsNullOrEmpty(_sourceName))
                {
                    var escapedSource = _sourceName.Replace("'", "&apos;");
                    systemFilters.Add($"Provider[@Name='{escapedSource}']");
                }

                if (level.HasValue && level != EventLogLevel.All)
                {
                    systemFilters.Add($"Level={(int)level.Value}");
                }

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
                string query = string.IsNullOrEmpty(systemFilterString) ? "*" : $"*[System[{systemFilterString}]]";

                var eventQuery = new EventLogQuery(LogName, PathType.LogName, query);
                var records = _reader.ReadEvents(eventQuery);

                var results = new List<EventLogEntry>();

                foreach (var evt in records)
                {
                    //
                    // All evt.* accesses must stay inside the using block to avoid ObjectDisposedException
                    //
                    token.ThrowIfCancellationRequested();

                    var message = evt.Message ?? string.Empty;

                    // Only include Servy service logs: messages with [..]
                    if (message.IndexOf("[", StringComparison.OrdinalIgnoreCase) < 0 ||
                        message.IndexOf("]", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!string.IsNullOrEmpty(keyword) &&
                        message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    results.Add(evt);

                    if (results.Count >= MaxResults)
                        break;
                }

                return results
                    .OrderByDescending(r => r.Time)
                    .ToList();
            }, token);
        }

    }
}
