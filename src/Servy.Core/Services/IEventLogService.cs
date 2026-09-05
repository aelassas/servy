using Servy.Core.DTOs;
using Servy.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Services
{
    /// <summary>
    /// Defines methods for querying Windows Event Viewer logs.
    /// </summary>
    public interface IEventLogService
    {
        /// <summary>
        /// Searches the Windows Event Viewer logs for events matching the given filters.
        /// </summary>
        /// <param name="level">The severity level to filter by (null for all).</param>
        /// <param name="startDate">
        /// Lower bound of the search range, inclusive; null for no lower bound. Only the date part is used:
        /// the bound is widened to local midnight at the start of that day. The value is interpreted as
        /// local time regardless of its <see cref="DateTimeKind"/>; pass local or unspecified values.
        /// </param>
        /// <param name="endDate">
        /// Upper bound of the search range, inclusive; null for no upper bound. Only the date part is used:
        /// the bound is widened to the last tick of that local day. Interpreted as local time, as above.
        /// </param>
        /// <param name="keyword">The keyword to search for in event data (null or empty for no keyword filtering).</param>
        /// <param name="token">A cancellation token to cancel the operation.</param>
        /// <returns>A collection of matching <see cref="ServyEventLogEntry"/> records.</returns>
        /// <exception cref="System.Security.SecurityException">
        /// The configured event source name fails the allowlist, or the process lacks permission to read the log.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The Windows Event Log service is unavailable, or the composed query was rejected.
        /// </exception>
        Task<IEnumerable<ServyEventLogEntry>> SearchAsync(EventLogLevel? level, DateTime? startDate, DateTime? endDate, string keyword, CancellationToken token = default);
    }
}
