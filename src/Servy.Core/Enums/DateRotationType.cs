namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines the supported date-based log rotation intervals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>CRITICAL ARCHITECTURAL NOTE:</b> The integer values assigned to this enumeration are actively 
    /// persisted as <c>INTEGER</c> rows within the underlying SQLite database schema. They must never be 
    /// renumbered, reordered, or deleted, as doing so will cause catastrophic silent data corruption 
    /// across existing production customer databases.
    /// </para>
    /// <para>
    /// Note: <see cref="None"/> is explicitly pinned to <c>3</c> instead of <c>0</c> to match the legacy 
    /// layout order from early releases. Consequently, <c>default(DateRotationType)</c> yields <see cref="Daily"/>. 
    /// When initializing new fields, configurations, or data transfer objects, you must explicitly assign 
    /// an appropriate default state value rather than relying on the CLR zero-initialization layout.
    /// </para>
    /// </remarks>
    public enum DateRotationType
    {
        /// <summary>
        /// Rotates the log file once per calendar day, anchored to the clock selected by 
        /// <c>useLocalTimeForRotation</c> (defaults to UTC).
        /// </summary>
        Daily = 0,

        /// <summary>
        /// Rotates the log file once per calendar week, anchored to the clock selected by 
        /// <c>useLocalTimeForRotation</c> (defaults to UTC). Determined using the 
        /// ISO week-numbering system (FirstFourDayWeek, Monday as first day).
        /// </summary>
        Weekly = 1,

        /// <summary>
        /// Rotates the log file once per calendar month, anchored to the clock selected by 
        /// <c>useLocalTimeForRotation</c> (defaults to UTC).
        /// </summary>
        Monthly = 2,

        /// <summary>
        /// Disables date-based rotation. The log file will not be rotated based 
        /// on time intervals, regardless of the date change.
        /// </summary>
        None = 3,
    }
}