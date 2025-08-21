using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines an abstraction for reading events from the Windows Event Viewer.
    /// This allows decoupling the <see cref="EventLogReader"/> implementation 
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
        /// <returns>
        /// A collection of <see cref="EventRecord"/> objects that match the query.
        /// </returns>
        IEnumerable<EventRecord> ReadEvents(EventLogQuery query);
    }
}
