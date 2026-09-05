using Servy.Manager.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Represents the result of an initial log history load, containing the captured log lines
    /// and the file state metadata required to begin live tailing.
    /// </summary>
    public class HistoryResult
    {
        /// <summary>
        /// Gets a read-only collection of <see cref="LogLine"/> objects read from the file.
        /// </summary>
        /// <remarks>
        /// Using IReadOnlyList prevents callers from accidentally modifying the historical
        /// snapshot (e.g., adding or clearing lines) which would desynchronize the tailing state.
        /// </remarks>
        public IReadOnlyList<LogLine> Lines { get; }

        /// <summary>
        /// Gets the byte position in the file where the history read ended.
        /// This serves as the starting point for subsequent live tailing.
        /// </summary>
        public long Position { get; }

        /// <summary>
        /// Gets the UTC creation time of the log file at the time of reading
        /// (<see cref="FileInfo.CreationTimeUtc"/>, not <see cref="FileInfo.CreationTime"/>).
        /// Used to detect file rotations or resets during live monitoring; compared with
        /// <c>!=</c>, which ignores <see cref="DateTime.Kind"/>, so a local-time value here
        /// reports a rotation on every poll.
        /// </summary>
        public DateTime CreationTimeUtc { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryResult"/> class.
        /// </summary>
        /// <param name="lines">The list of initial log lines.</param>
        /// <param name="position">The file pointer position after the read.</param>
        /// <param name="creationTimeUtc">The UTC creation timestamp of the source file.</param>
        public HistoryResult(List<LogLine> lines, long position, DateTime creationTimeUtc)
        {
            if (creationTimeUtc.Kind == DateTimeKind.Local)
                throw new ArgumentException("Creation time must be UTC.", nameof(creationTimeUtc));
            // We still accept List<T> in the constructor for convenience,
            // but it is stored and exposed as a defensively copied IReadOnlyList.
            Lines = lines != null ? new List<LogLine>(lines) : new List<LogLine>();
            Position = position;
            CreationTimeUtc = creationTimeUtc;
        }
    }
}
