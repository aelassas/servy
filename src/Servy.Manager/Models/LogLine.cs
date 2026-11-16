namespace Servy.Manager.Models
{
    /// <summary>
    /// Defines the source stream of a log entry.
    /// </summary>
    public enum LogType
    {
        /// <summary> Standard output stream (typically informational). </summary>
        StdOut,
        /// <summary> Standard error stream (typically warnings or failures). </summary>
        StdErr
    }

    /// <summary>
    /// Represents a single line of text captured from a service log file.
    /// </summary>
    public class LogLine
    {
        /// <summary>
        /// Gets or sets the raw text content of the log line.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the source stream type, used for UI color coding.
        /// </summary>
        public LogType Type { get; set; }

        /// <summary>
        /// Gets the line's timestamp in UTC: the time the line was read for a live-tailed line, or a time derived
        /// from the log file's last write time for a line loaded from history.
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogLine"/> class.
        /// </summary>
        /// <param name="text">The string content read from the file.</param>
        /// <param name="type">The stream type (StdOut or StdErr).</param>
        /// <param name="timestamp">A timestamp that will be normalized to UTC, or <see langword="null"/> to use the current UTC time.</param>
        public LogLine(string text, LogType type, DateTime? timestamp = null)
        {
            Text = text;
            Type = type;

            // Normalize at the boundary. Unspecified is assumed to be UTC: every producer in this
            // codebase reads UTC file times, and ToUniversalTime() would otherwise shift it by the
            // reading machine's offset (see EventLogReader.SafeToOffset for the same rule on .evtx).
            Timestamp = timestamp.HasValue
                ? (timestamp.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc)
                    : timestamp.Value.ToUniversalTime())
                : DateTime.UtcNow;
        }

    }
}
