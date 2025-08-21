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
        /// <param name="startDate">The start date of the search range (null for no lower bound).</param>
        /// <param name="endDate">The end date of the search range (null for no upper bound).</param>
        /// <param name="keyword">The keyword to search for in event data (null or empty for no keyword filtering).</param>
        /// <param name="token">A cancellation token to cancel the operation.</param>
        /// <returns>A collection of matching <see cref="EventLogEntry"/> records.</returns>
        Task<IEnumerable<EventLogEntry>> SearchAsync(EventLogLevel? level, DateTime? startDate, DateTime? endDate, string keyword, CancellationToken token = default);
    }
}
