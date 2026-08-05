using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Servy.UI.Converters
{
    /// <summary>
    /// Converts a <see cref="bool"/> value to <see cref="Visibility"/> and vice versa.
    /// <c>true</c> maps to <see cref="Visibility.Visible"/>, <c>false</c> maps to <see cref="Visibility.Collapsed"/>.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a WPF <see cref="Visibility"/> enumeration.
        /// </summary>
        /// <param name="value">The boolean value produced by the source binding.</param>
        /// <param name="targetType">The type of the binding target property (expected to be <see cref="Visibility"/>).</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>
        /// <see cref="Visibility.Visible"/> if <paramref name="value"/> is <c>true</c>;
        /// <see cref="Visibility.Collapsed"/> if <c>false</c>; otherwise <see cref="Binding.DoNothing"/>.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;
            return Binding.DoNothing;
        }

        /// <summary>
        /// Converts a <see cref="Visibility"/> value back to a <see cref="bool"/>.
        /// </summary>
        /// <param name="value">The <see cref="Visibility"/> value produced by the binding target.</param>
        /// <param name="targetType">The type of the binding source property (expected to be <see cref="bool"/>).</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="value"/> is <see cref="Visibility.Visible"/>;
        /// <c>false</c> if <see cref="Visibility.Collapsed"/> or <see cref="Visibility.Hidden"/>;
        /// otherwise <see cref="Binding.DoNothing"/>.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v == Visibility.Visible;
            return Binding.DoNothing;
        }
    }
}
