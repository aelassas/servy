using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a Process ID (PID) numerical value into a display-friendly string. 
    /// Handles null values by returning a predefined placeholder constant.
    /// </summary>
    public class PidConverter : IValueConverter
    {
        /// <summary>
        /// The sentinel value used when a process ID cannot be resolved or is currently null.
        /// </summary>
        private const string UnknownPid = UiConstants.NotAvailable;

        /// <summary>
        /// Converts the provided PID object into its string representation.
        /// </summary>
        /// <param name="value">The PID value to convert.</param>
        /// <param name="targetType">The type expected by the binding target property.</param>
        /// <param name="parameter">An optional converter parameter, currently unused.</param>
        /// <param name="culture">The culture information used for localization, currently unused.</param>
        /// <returns>The string representation of the PID, or <see cref="UiConstants.NotAvailable"/> if the input is null.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() ?? UnknownPid;
        }

        /// <summary>
        /// Not implemented as this converter supports one-way data binding exclusively.
        /// </summary>
        /// <param name="value">The value produced by the binding target.</param>
        /// <param name="targetType">The type to convert back to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A <see cref="Binding.DoNothing"/> instance to indicate the operation is not supported.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}