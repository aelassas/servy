using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Servy.UI.Converters
{
    /// <summary>
    /// Converts a <see cref="bool"/> value to <see cref="Visibility"/> and vice versa.
    /// True maps to <see cref="Visibility.Visible"/>, false maps to <see cref="Visibility.Collapsed"/>.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}
