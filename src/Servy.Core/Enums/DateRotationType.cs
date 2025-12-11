namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines the supported date-based log rotation intervals.
    /// </summary>
    public enum DateRotationType
    {
        /// <summary>
        /// Rotates the log file once per calendar day (UTC).
        /// </summary>
        Daily,

        /// <summary>
        /// Rotates the log file once per calendar week (UTC),
        /// determined using the ISO week-numbering system
        /// (FirstFourDayWeek, Monday as first day).
        /// </summary>
        Weekly,

        /// <summary>
        /// Rotates the log file once per calendar month (UTC).
        /// </summary>
        Monthly
    }

}
