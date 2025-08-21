using Servy.Core.Enums;
using System;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Represents a single event log entry from Windows Event Viewer.
    /// </summary>
    public class EventLogEntry
    {
        /// <summary>
        /// Gets or sets the event identifier.
        /// </summary>
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event was created.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the event log entry.
        /// </summary>
        public EventLogLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the event message (description).
        /// </summary>
        public string Message { get; set; }
    }
}
