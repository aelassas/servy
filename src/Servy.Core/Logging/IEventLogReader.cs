using Servy.Core.DTOs;
using System.Collections.Generic;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines an abstraction for reading events from the Windows Event Log.
    /// This allows decoupling the <see cref="System.Diagnostics.Eventing.Reader.EventLogReader"/> implementation
    /// from consumers, enabling easier unit testing and mocking.
    /// </summary>
    public interface IEventLogReader
    {
        /// <summary>
        /// Reads events from the Windows Event Log matching the specified XPath selector.
        /// </summary>
        /// <param name="logName">Name of the log to read, for example "Application".</param>
        /// <param name="xpathQuery">The XPath selector applied to the log.</param>
        /// <param name="newestFirst">When <see langword="true"/>, events are returned newest first.</param>
        /// <param name="maxReadCount">
        /// Maximum number of events to read. A value of zero or less reads nothing.
        /// </param>
        /// <returns>
        /// A collection of <see cref="ServyEventLogEntry"/> objects that match the query.
        /// </returns>
        IEnumerable<ServyEventLogEntry> ReadEvents(string logName, string xpathQuery, bool newestFirst, int maxReadCount);
    }
}
