using Servy.Manager.Models;
using System;
using System.Collections.Generic;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Represents the result of an initial log history load, containing the captured log lines 
    /// and the file state metadata required to begin live tailing.
    /// </summary>
    public class HistoryResult
    {
        /// <summary>
        /// Gets the collection of <see cref="LogLine"/> objects read from the file.
        /// </summary>
        public List<LogLine> Lines { get; }

        /// <summary>
        /// Gets the byte position in the file where the history read ended. 
        /// This serves as the starting point for subsequent live tailing.
        /// </summary>
        public long Position { get; }

        /// <summary>
        /// Gets the creation time of the log file at the time of reading. 
        /// Used to detect file rotations or resets during live monitoring.
        /// </summary>
        public DateTime CreationTime { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryResult"/> class.
        /// </summary>
        /// <param name="lines">The list of initial log lines.</param>
        /// <param name="position">The file pointer position after the read.</param>
        /// <param name="creationTime">The creation timestamp of the source file.</param>
        public HistoryResult(List<LogLine> lines, long position, DateTime creationTime)
        {
            Lines = lines;
            Position = position;
            CreationTime = creationTime;
        }
    }
}