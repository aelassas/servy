using System;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Converters
{
    /// <summary>
    /// Converts a boolean value to its inverse.
    /// Useful for data binding scenarios where you want to negate a boolean.
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to its inverse.
        /// </summary>
        /// <param name="value">The source data being passed to the target (expected to be a bool).</param>
        /// <param name="targetType">The type of the binding target property (ignored).</param>
        /// <param name="parameter">Optional parameter (ignored).</param>
        /// <param name="culture">The culture to use in the converter (ignored).</param>
        /// <returns>The inverted boolean value if <paramref name="value"/> is a bool; otherwise <c>true</c>.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }

        /// <summary>
        /// Converts the value back by inverting it again.
        /// </summary>
        /// <param name="value">The value from the binding target (expected to be a bool).</param>
        /// <param name="targetType">The type to convert to (ignored).</param>
        /// <param name="parameter">Optional parameter (ignored).</param>
        /// <param name="culture">The culture to use in the converter (ignored).</param>
        /// <returns>The inverted boolean value if <paramref name="value"/> is a bool; otherwise <c>true</c>.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }
    }
}
