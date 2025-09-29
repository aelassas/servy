using Servy.Core.Helpers;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a CPU usage to a string in percentage.
    /// </summary>
    public class CpuUsageConverter : IValueConverter
    {
        /// <summary>
        /// CPU usage not available.
        /// </summary>
        const string UnknownCpuUsage = "N/A";

        /// <summary>
        /// Returns the CPU usage as string in percentage.
        /// </summary>
        /// <param name="value">The PID.</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>The CPU usage as string.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double? cpuUsage = null;
            double val;
            if (value != null && double.TryParse(value.ToString(), out val))
            {
                cpuUsage = val;
            }
            if (!cpuUsage.HasValue)
                return UnknownCpuUsage;

            return ProcessHelper.FormatCpuUsage(cpuUsage.Value);
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
