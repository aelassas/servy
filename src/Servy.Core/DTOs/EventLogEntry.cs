using Servy.Core.Enums;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Represents a single event log entry retrieved from the Windows Event Viewer.
    /// </summary>
    public class EventLogEntry
    {
        /// <summary>
        /// Gets or sets the specific event identifier.
        /// </summary>
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the event was logged.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Gets or sets the severity level (e.g., Information, Warning, Error) of the entry.
        /// </summary>
        public EventLogLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the name of the software or component that published the event.
        /// </summary>
        /// <remarks>
        /// This property is essential for distinguishing Servy-specific logs from generic 
        /// system events when performing wildcard searches across the Application log.
        /// </remarks>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the descriptive message associated with the event.
        /// </summary>
        public string? Message { get; set; }
    }
}