using Servy.Core.Helpers;
using System;
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
        const string UnknownRamUsage = "N/A";

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
            long? ramUsage = null;
            long val;
            if (value != null && long.TryParse(value.ToString(), out val))
            {
                ramUsage = val;
            }
            if (!ramUsage.HasValue)
                return UnknownRamUsage;

            return ProcessHelper.FormatRamUsage(ramUsage.Value);
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
