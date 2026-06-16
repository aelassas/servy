using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Servy.UI.Helpers
{
    /// <summary>
    /// Provides utility methods for formatting durations, numbers, and row information.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Recursively searches the WPF Visual Tree to find a child of a specific type.
        /// Used here to extract the internal <see cref="ScrollViewer"/> from the <see cref="ListBox"/>.
        /// </summary>
        /// <typeparam name="T">The type of the visual child to find.</typeparam>
        /// <param name="parent">The parent object to start the search from.</param>
        /// <returns>The found child of type T, or null if not found.</returns>
        public static T GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var res = GetVisualChild<T>(child);
                if (res != null) return res;
            }
            return null;
        }

        /// <summary>
        /// Formats a <see cref="TimeSpan"/> into a human-readable string, omitting zero-value components.
        /// </summary>
        /// <param name="duration">The duration to format.</param>
        /// <returns>
        /// A formatted string such as <c>1h 5s</c>, <c>15s</c>, or <c>0ms</c> if the duration is zero.
        /// </returns>
        public static string FormatDuration(TimeSpan duration)
        {
            var parts = new List<string>();

            // 1. Capture total hours (handling durations > 24h if necessary)
            if ((int)duration.TotalHours > 0)
            {
                parts.Add($"{(int)duration.TotalHours}h");
            }

            // 2. Capture minutes (0-59)
            if (duration.Minutes > 0)
            {
                parts.Add($"{duration.Minutes}m");
            }

            // 3. Capture seconds (0-59)
            if (duration.Seconds > 0)
            {
                parts.Add($"{duration.Seconds}s");
            }

            // 4. Capture milliseconds (0-999)
            if (duration.Milliseconds > 0)
            {
                parts.Add($"{duration.Milliseconds}ms");
            }

            // 5. Join parts with a space, or return "0ms" for zero/near-zero durations
            return parts.Count > 0 ? string.Join(" ", parts) : "0ms";
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
        /// <param name="count">The number of items processed.</param>
        /// <param name="duration">The time taken for the operation.</param>
        /// <param name="noneFormat">Template for zero items (e.g. "No services loaded in {0}").</param>
        /// <param name="oneFormat">Template for one item (e.g. "Loaded 1 service in {0}").</param>
        /// <param name="manyFormat">Template for multiple items (e.g. "Loaded {0} services in {1}").</param>
        /// <returns>
        /// A string such as:  
        /// <c>No services in 500ms</c>,  
        /// <c>1 service in 2s</c>,  
        /// <c>1,234 logs in 1m 20s</c>.
        /// </returns>
        public static string GetRowsInfo(
            int count,
            TimeSpan duration,
            string noneFormat,
            string oneFormat,
            string manyFormat)
        {
            var durationText = FormatDuration(duration);

            if (count == 0)
            {
                return string.Format(noneFormat, durationText);
            }

            if (count == 1)
            {
                return string.Format(oneFormat, durationText);
            }

            // Pass the formatted count and the duration to the plural template
            return string.Format(manyFormat, FormatNumber(count), durationText);
        }

    }
}
