using Servy.Manager.Resources;
using System.Collections;
using System.Globalization;
using System.Resources;
using System.Windows.Controls;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Gives a grid column an identifier that does not change with the display language.
    /// </summary>
    /// <remarks>
    /// The persisted column settings (hidden columns, column widths) identify a column by its header text.
    /// That text is localized, so the identifier is the header text of the neutral (English) resources:
    /// settings saved before the language was changed, and settings saved by a version without translations,
    /// keep matching their columns.
    /// </remarks>
    public static class ColumnIds
    {
        private const string ColumnKeyPrefix = "Column_";

        /// <summary>
        /// Returns the language-independent identifier of a column.
        /// </summary>
        /// <param name="column">The column to identify.</param>
        /// <returns>The identifier, or <see langword="null"/> when the column has no header text.</returns>
        public static string? For(DataGridColumn? column)
        {
            return column?.Header is string header ? FromHeader(header) : null;
        }

        /// <summary>
        /// Maps a header text shown in the current display language to its neutral (English) text.
        /// </summary>
        /// <param name="header">The header text as displayed.</param>
        /// <returns>
        /// The neutral text of the column resource whose current value is <paramref name="header"/>;
        /// <paramref name="header"/> itself when no column resource has that value.
        /// </returns>
        public static string? FromHeader(string? header)
        {
            return FromHeader(header, Strings.ResourceManager, CultureInfo.CurrentUICulture);
        }

        /// <summary>
        /// Maps a header text shown in <paramref name="culture"/> to its neutral text.
        /// </summary>
        /// <param name="header">The header text as displayed.</param>
        /// <param name="resources">The resources the header texts come from.</param>
        /// <param name="culture">The culture the header is displayed in.</param>
        /// <returns>The neutral text, or <paramref name="header"/> itself when no column resource has that value.</returns>
        internal static string? FromHeader(string? header, ResourceManager resources, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(header))
            {
                return header;
            }

            ResourceSet? localized;
            try
            {
                localized = resources.GetResourceSet(culture, true, true);
            }
            catch (MissingManifestResourceException)
            {
                return header;
            }

            if (localized == null)
            {
                return header;
            }

            foreach (DictionaryEntry entry in localized)
            {
                if (entry.Key is string key
                    && key.StartsWith(ColumnKeyPrefix, StringComparison.Ordinal)
                    && entry.Value is string value
                    && string.Equals(value, header, StringComparison.Ordinal))
                {
                    return resources.GetString(key, CultureInfo.InvariantCulture) ?? header;
                }
            }

            return header;
        }
    }
}
