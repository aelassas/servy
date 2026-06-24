using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using System.Diagnostics.Eventing.Reader;
using System.Security;
using System.Text.RegularExpressions;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides functionality to query Windows Event Viewer logs.
    /// </summary>
    public class EventLogService : IEventLogService
    {
        // Strict allowlist: Alphanumeric, spaces, dots, underscores, and hyphens.
        // This covers virtually all valid Windows Service and Event Source names.
        private static readonly Regex SourceNameValidator = new Regex(@"^[a-zA-Z0-9\.\- _]+$", RegexOptions.Compiled, AppConfig.InputRegexTimeout);

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
        /// the service defaults to the value defined in <see cref="AppConfig.EventSource"/>.
        /// Pass an empty string to disable the provider filter and enable wildcard querying.
        /// </param>
        public EventLogService(IEventLogReader reader, string? sourceName = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _sourceName = sourceName ?? AppConfig.EventSource;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ServyEventLogEntry>> SearchAsync(
            EventLogLevel? level,
            DateTime? startDate,
            DateTime? endDate,
            string? keyword,
            CancellationToken token = default)
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var systemFilters = new List<string>();

                // ESCAPE SOURCE NAME
                if (!string.IsNullOrWhiteSpace(_sourceName))
                {
                    // 1. SECURITY GUARD: Validate against allowlist
                    // This prevents any character-based injection before escaping even starts.
                    if (!SourceNameValidator.IsMatch(_sourceName))
                    {
                        throw new SecurityException($"Invalid Event Source name: '{_sourceName}'. " +
                            "Only alphanumeric characters, spaces, dots, hyphens, and underscores are allowed.");
                    }

                    // 2. ROBUST ESCAPING: Use standard XML escaping
                    // SecurityElement.Escape handles &, <, >, ", and ' correctly.
                    var escapedSource = SecurityElement.Escape(_sourceName);

                    systemFilters.Add($"Provider[@Name='{escapedSource}']");
                }

                if (level.HasValue && level != EventLogLevel.All)
                {
                    if (level.Value == EventLogLevel.Error)
                    {
                        // Since ParseLevel maps both Windows Level 1 (Critical) and 2 (Error) 
                        // to EventLogLevel.Error, the query must include both levels.
                        systemFilters.Add("(Level=1 or Level=2)");
                    }
                    else
                    {
                        systemFilters.Add($"Level={(int)level.Value}");
                    }
                }

                if (startDate.HasValue)
                {
                    var localMidnight = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Local);
                    var startUtc = localMidnight.ToUniversalTime();
                    systemFilters.Add($"TimeCreated[@SystemTime >= '{startUtc:o}']");
                }

                if (endDate.HasValue)
                {
                    var localEnd = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
                    var endUtc = localEnd.ToUniversalTime();
                    systemFilters.Add($"TimeCreated[@SystemTime <= '{endUtc:o}']");
                }

                string systemFilterString = string.Join(" and ", systemFilters);
                string query = string.IsNullOrEmpty(systemFilterString) ? "*" : $"*[System[{systemFilterString}]]";

                IEnumerable<ServyEventLogEntry> records;
                try
                {
                    var eventQuery = new EventLogQuery(AppConfig.EventLogName, PathType.LogName, query)
                    {
                        ReverseDirection = true   // newest events first, so the MaxResults cap keeps the most recent
                    };

                    // This is where the service handle is requested and the query is validated
                    records = _reader.ReadEvents(eventQuery, AppConfig.EventLogMaxResults * AppConfig.EventLogPrefetchCushion); // Generous cushion
                }
                catch (EventLogException ex)
                {
                    // InvalidOperationException is appropriate when a required system service (Event Log) 
                    // is not in the correct state to perform the operation.
                    throw new InvalidOperationException("Cannot access Windows Event Log. Ensure the 'Windows Event Log' service is running and the query is valid.", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    // SecurityException is the standard .NET way to signal that a call failed 
                    // due to insufficient permissions/privileges.
                    throw new SecurityException("Access denied to Windows Event Log. Please ensure the application is running with sufficient privileges (Administrator).", ex);
                }

                var results = new List<ServyEventLogEntry>();

                foreach (var evt in records)
                {
                    // records is a lazy iterator over the underlying EventLogReader; the foreach's
                    // hidden enumerator disposal will cascade through to EventLogReader.ReadEvents
                    // and dispose the EventLog handle on break/exception. Avoid holding 'records'
                    // past this loop.
                    token.ThrowIfCancellationRequested();

                    var message = evt.Message ?? string.Empty;
                    var provider = evt.ProviderName ?? string.Empty;

                    // 1. Heuristic only meaningful in wildcard mode; the XPath provider filter handles non-empty source names exactly.
                    // This prevents capturing unrelated system/app logs even when _sourceName is empty.
                    if (string.IsNullOrEmpty(_sourceName) &&
                        provider.IndexOf(AppConfig.EventSource, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    // 2. Formatting Check: Ensure it follows the Servy log pattern [Service] Message
                    if (!message.Contains('[') || !message.Contains(']'))
                        continue;

                    // 3. Optional keyword filter
                    if (!string.IsNullOrEmpty(keyword) &&
                        message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    results.Add(evt);

                    if (results.Count >= AppConfig.EventLogMaxResults)
                        break;
                }

                return results
                    .OrderByDescending(r => r.Time)
                    .ToList();
            }, token);
        }

    }
}
