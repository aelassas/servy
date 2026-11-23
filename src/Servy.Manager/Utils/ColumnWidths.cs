using System.Globalization;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Reads and writes the persisted column widths of a grid, as stored in the
    /// <c>ServicesColumnWidths</c> and <c>LogsColumnWidths</c> settings between two sessions.
    /// </summary>
    /// <remarks>
    /// The persisted form is a comma-separated list of <c>Header=Width</c> entries, for example
    /// <c>Name=180,Status=95.5</c>. A column is identified by the English text of its header, whatever the display
    /// language (see <see cref="ColumnIds"/>), as in the hidden-columns settings, and the width is in
    /// device-independent units written with the invariant culture.
    /// </remarks>
    public static class ColumnWidths
    {
        /// <summary>
        /// The largest column width that is read or written. A larger value is treated as corrupt and ignored.
        /// </summary>
        public const double MaxWidth = 10000;

        /// <summary>
        /// The character that separates two entries in the persisted form.
        /// </summary>
        private const char EntrySeparator = ',';

        /// <summary>
        /// The character that separates the header from the width inside an entry.
        /// </summary>
        private const char WidthSeparator = '=';

        /// <summary>
        /// The format applied to each persisted width: at most two decimals, no trailing zeros.
        /// </summary>
        private const string NumberFormat = "0.##";

        /// <summary>
        /// Parses the persisted form of a set of column widths.
        /// </summary>
        /// <param name="value">The persisted <c>Header=Width</c> list.</param>
        /// <returns>
        /// The usable widths in the order they were written, at most one per header (compared without
        /// regard to case, the last entry winning). An entry without a header, or whose width is not a
        /// finite number in the range (0, <see cref="MaxWidth"/>], is skipped, so one damaged entry never
        /// discards the others. The list is empty when <paramref name="value"/> is null or blank.
        /// </returns>
        public static IReadOnlyList<KeyValuePair<string, double>> Parse(string? value)
        {
            var result = new List<KeyValuePair<string, double>>();

            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            foreach (var entry in value.Split(EntrySeparator))
            {
                var separatorIndex = entry.LastIndexOf(WidthSeparator);
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var header = entry.Substring(0, separatorIndex).Trim();
                var widthText = entry.Substring(separatorIndex + 1);

                if (header.Length == 0
                    || !double.TryParse(widthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
                    || !IsValidWidth(width))
                {
                    continue;
                }

                result.RemoveAll(e => string.Equals(e.Key, header, StringComparison.OrdinalIgnoreCase));
                result.Add(new KeyValuePair<string, double>(header, width));
            }

            return result;
        }

        /// <summary>
        /// Looks up the width persisted for a column.
        /// </summary>
        /// <param name="widths">The widths returned by <see cref="Parse(string)"/>.</param>
        /// <param name="header">The header text of the column.</param>
        /// <param name="width">The persisted width of the column.</param>
        /// <returns><see langword="true"/> when a width is persisted for <paramref name="header"/>.</returns>
        public static bool TryGetWidth(IReadOnlyList<KeyValuePair<string, double>> widths, string? header, out double width)
        {
            width = 0;

            if (widths == null || string.IsNullOrEmpty(header))
            {
                return false;
            }

            foreach (var entry in widths)
            {
                if (string.Equals(entry.Key, header, StringComparison.OrdinalIgnoreCase))
                {
                    width = entry.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds the persisted form from the widths measured on screen, keeping the previously persisted
        /// width of every column that could not be measured.
        /// </summary>
        /// <param name="previous">The currently persisted <c>Header=Width</c> list.</param>
        /// <param name="measured">
        /// The header and current width of each column that can be measured, in display order. A hidden
        /// column has no width on screen and is left out by the caller.
        /// </param>
        /// <returns>
        /// The <c>Header=Width</c> list to persist: the measured columns first, then the entries of
        /// <paramref name="previous"/> that were not measured, so hiding a column does not lose its width.
        /// A measured entry that cannot be written back unambiguously (an empty header, a header containing
        /// a separator, or a width outside the range (0, <see cref="MaxWidth"/>]) is skipped.
        /// </returns>
        public static string Merge(string? previous, IEnumerable<KeyValuePair<string, double>> measured)
        {
            var result = new List<KeyValuePair<string, double>>();

            if (measured != null)
            {
                foreach (var entry in measured)
                {
                    var header = entry.Key?.Trim();
                    if (string.IsNullOrEmpty(header)
                        || header.IndexOf(EntrySeparator) >= 0
                        || header.IndexOf(WidthSeparator) >= 0
                        || !IsValidWidth(entry.Value)
                        || TryGetWidth(result, header, out _))
                    {
                        continue;
                    }

                    result.Add(new KeyValuePair<string, double>(header, entry.Value));
                }
            }

            foreach (var entry in Parse(previous))
            {
                if (!TryGetWidth(result, entry.Key, out _))
                {
                    result.Add(entry);
                }
            }

            return string.Join(
                EntrySeparator.ToString(),
                result.Select(e => e.Key + WidthSeparator + e.Value.ToString(NumberFormat, CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Determines whether a width can be persisted and restored.
        /// </summary>
        /// <param name="width">The width to test.</param>
        /// <returns><see langword="true"/> when <paramref name="width"/> is a finite number in the range (0, <see cref="MaxWidth"/>].</returns>
        private static bool IsValidWidth(double width)
        {
            return !double.IsNaN(width) && !double.IsInfinity(width) && width > 0 && width <= MaxWidth;
        }
    }
}
