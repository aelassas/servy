using Servy.Core.DTOs;
using System.Diagnostics.Eventing.Reader;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines an abstraction for reading events from the Windows Event Viewer.
    /// This allows decoupling the <see cref="System.Diagnostics.Eventing.Reader.EventLogReader"/> implementation 
    /// from consumers, enabling easier unit testing and mocking.
    /// </summary>
    public interface IEventLogReader
    {
        /// <summary>
        /// Reads events from the Windows Event Viewer using the specified <see cref="EventLogQuery"/>.
        /// </summary>
        /// <param name="query">
        /// The query that defines which log to read and the conditions to filter events.
        /// </param>
        /// <param name="maxReadCount">Maximum number of events to read.</param>
        /// <returns>
        /// A collection of <see cref="ServyEventLogEntry"/> objects that match the query.
        /// </returns>
        IEnumerable<ServyEventLogEntry> ReadEvents(EventLogQuery query, int maxReadCount);
    }
}
