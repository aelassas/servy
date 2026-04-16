using Servy.Core.Helpers;
using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a RAM usage to a string.
    /// </summary>
    public class RamUsageConverter : IValueConverter
    {
        /// <summary>
        /// RAM usage not available.
        /// </summary>
        const string UnknownRamUsage = UiConstants.NotAvailable;

        /// <summary>
        /// Returns the RAM usage as string.
        /// </summary>
        /// <param name="value">The PID.</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>The RAM usage as string.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Pattern matching handles the null check and unboxing in one go.
            // If the bound property is double? and is null, this will safely return false.
            if (value is long ramUsage)
            {
                return ProcessHelper.FormatRamUsage(ramUsage);
            }

            return UnknownRamUsage;
        }

        /// <summary>
        /// Not implemented (one-way binding only).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
