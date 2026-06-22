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
        /// <summary>
        /// Converts a boolean value to a WPF <see cref="Visibility"/> state enumeration.
        /// </summary>
        /// <param name="value">The boolean flag produced by the source binding.</param>
        /// <param name="targetType">The type of the binding target property (expected to be <see cref="Visibility"/>).</param>
        /// <param name="parameter">An optional user parameter passed to the converter logic.</param>
        /// <param name="culture">The cultural optimization rules used during conversion formatting context.</param>
        /// <returns>
        /// <see cref="Visibility.Visible"/> if the source evaluation is <c>true</c>; 
        /// otherwise, <see cref="Visibility.Collapsed"/>.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a WPF <see cref="Visibility"/> state enumeration back to a underlying boolean flag representation.
        /// </summary>
        /// <param name="value">The current <see cref="Visibility"/> visual state element of the target property.</param>
        /// <param name="targetType">The type of the binding source property (expected to be <see cref="bool"/>).</param>
        /// <param name="parameter">An optional user parameter passed to the converter logic.</param>
        /// <param name="culture">The cultural optimization rules used during conversion formatting context.</param>
        /// <returns>
        /// <c>true</c> if the incoming element is explicitly set to <see cref="Visibility.Visible"/>; 
        /// otherwise, <c>false</c>.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}