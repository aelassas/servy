namespace Servy.Core.Enums
{
    /// <summary>
    /// Represents the severity level of an event log entry.
    /// </summary>
    public enum EventLogLevel
    {
        /// <summary>
        /// Verbose event.
        /// </summary>
        Verbose = 5,

        /// <summary>
        /// Informational event.
        /// </summary>
        Information = 4,

        /// <summary>
        /// Warning event that indicates a potential issue.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Error event indicating a failure or problem.
        /// </summary>
        Error = 2,

        /// <summary>
        /// Error event indicating a critical failure or problem.
        /// </summary>
        Critical = 1,

        /// <summary>
        /// Represents all levels of events (used for filtering purposes).
        /// </summary>
        All = 0,
    }
}
