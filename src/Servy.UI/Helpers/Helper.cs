using System;

namespace Servy.UI.Helpers
{
    /// <summary>
    /// Provides utility methods for formatting durations, numbers, and row information.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Formats a <see cref="TimeSpan"/> into a human-readable string.
        /// </summary>
        /// <param name="duration">The duration to format.</param>
        /// <returns>
        /// A formatted string representing the duration, for example:
        /// <c>1h 23m 45s</c>, <c>5m 12s</c>, <c>15s 50ms</c>, or <c>250ms</c>.
        /// </returns>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
            else if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
            else if (duration.TotalSeconds >= 1)
                return $"{(int)duration.TotalSeconds}s {duration.Milliseconds}ms";
            else
                return $"{duration.Milliseconds}ms";
        }

        /// <summary>
        /// Formats an integer with thousands separators.
        /// </summary>
        /// <param name="number">The number to format.</param>
        /// <returns>
        /// A string containing the formatted number.  
        /// For example: <c>1,234</c> or <c>1,000,000</c>.
        /// </returns>
        public static string FormatNumber(int number)
        {
            return number.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Generates a message describing how many rows were processed within a given duration.
        /// </summary>
        /// <param name="count">The number of rows.</param>
        /// <param name="duration">The duration of the operation.</param>
        /// <param name="rowText">The singular form of the row name (e.g., "row", "item"). The method will append "s" for plural cases.</param>
        /// <returns>
        /// A string such as:  
        /// <c>No items in 500ms</c>,  
        /// <c>1 item in 2s</c>,  
        /// <c>1,234 items in 1m 20s</c>.
        /// </returns>
        public static string GetRowsInfo(int count, TimeSpan duration, string rowText)
        {
            var durationText = FormatDuration(duration);

            if (count == 0)
                return $"No {rowText}s loaded in {durationText}";
            if (count == 1)
                return $"Loaded 1 {rowText} in {durationText}";
            else
                return $"Loaded {FormatNumber(count)} {rowText}s in {durationText}";
        }
    }
}
