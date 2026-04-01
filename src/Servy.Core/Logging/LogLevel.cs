namespace Servy.Core.Logging
{
    /// <summary>
    /// Defines the severity levels for log entries.
    /// </summary>
    public enum LogLevel
    {
        /// <summary> High-verbosity diagnostic information. </summary>
        Debug = 0,
        /// <summary> General operational milestones. </summary>
        Info = 1,
        /// <summary> Non-critical issues or unexpected states. </summary>
        Warn = 2,
        /// <summary> Critical failures and exceptions. </summary>
        Error = 3,
        /// <summary> Disables all logging. </summary>
        None = 4,
    }
}
