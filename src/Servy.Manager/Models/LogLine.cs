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
        private static long _nextId;

        /// <summary>
        /// Gets the unique identifier for the log line.
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// Gets or sets the raw text content of the log line.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the source stream type, used for UI color coding.
        /// </summary>
        public LogType Type { get; set; }

        /// <summary>
        /// Gets the UTC time when the log line was processed by the manager.
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the timestamp was synthetically generated 
        /// during history loading rather than parsed from the log stream.
        /// </summary>
        public bool IsSyntheticTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogLine"/> class.
        /// </summary>
        /// <param name="text">The string content read from the file.</param>
        /// <param name="type">The stream type (StdOut or StdErr).</param>
        /// <param name="timestamp"> A timestamp that will be normalized to UTC.</param>
        public LogLine(string text, LogType type, DateTime? timestamp = null)
        {
            Id = Interlocked.Increment(ref _nextId);
            Text = text;
            Type = type;

            // Normalize at the boundary: convert to UTC if provided, otherwise use current UTC.
            Timestamp = timestamp?.ToUniversalTime() ?? DateTime.UtcNow;

            IsSyntheticTime = false;
        }

    }
}