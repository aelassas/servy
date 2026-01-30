using System;
using System.Threading;

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
        /// Gets or sets the local time when the log line was processed by the manager.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogLine"/> class.
        /// </summary>
        /// <param name="text">The string content read from the file.</param>
        /// <param name="type">The stream type (StdOut or StdErr).</param>
        /// <param name="timestamp">Local time when the log line was processed.</param>
        public LogLine(string text, LogType type, DateTime? timestamp = null)
        {
            Id = Interlocked.Increment(ref _nextId);
            Text = text;
            Type = type;
            // Use provided timestamp (for history) or current time (for live)
            Timestamp = timestamp ?? DateTime.Now;
        }
    }
}