namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines the supported date-based log rotation intervals.
    /// </summary>
    public enum DateRotationType
    {
        /// <summary>
        /// Rotates the log file once per calendar day (local).
        /// </summary>
        Daily,

        /// <summary>
        /// Rotates the log file once per calendar week (local),
        /// determined using the ISO week-numbering system
        /// (FirstFourDayWeek, Monday as first day).
        /// </summary>
        Weekly,

        /// <summary>
        /// Rotates the log file once per calendar month (local).
        /// </summary>
        Monthly,

        /// <summary>
        /// Disables date-based rotation. The log file will not be rotated based 
        /// on time intervals, regardless of the date change.
        /// </summary>
        None,
    }

}
