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
        public IEnumerable<EventRecord> ReadEvents(EventLogQuery query)
        {
            var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query);
            var results = new List<EventRecord>();

            for (EventRecord evt = reader.ReadEvent(); evt != null; evt = reader.ReadEvent())
            {
                results.Add(evt);
            }

            return results;
        }
    }
}
