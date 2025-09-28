using System;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a PID to a string.
    /// </summary>
    public class PidConverter : IValueConverter
    {
        /// <summary>
        /// PID not available string.
        /// </summary>
        const string UnknownPid = "N/A";

        /// <summary>
        /// Returns the PID as string.
        /// </summary>
        /// <param name="value">The PID.</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>The PID as string.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return UnknownPid;

            return value.ToString();
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
