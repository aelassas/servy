using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a multi-line message string into a single-line preview by extracting the first line.
    /// Used primarily in UI lists where long log messages or detailed error reports need to be truncated for display space.
    /// </summary>
    public class MessageConverter : IValueConverter
    {
        private static readonly string[] LineSeparators = { "\r\n", "\r", "\n" };

        /// <summary>
        /// Splits the input string by standard newline sequences and returns the first segment found.
        /// </summary>
        /// <param name="value">The source message object, typically a <see cref="string"/>.</param>
        /// <param name="targetType">The type expected by the binding target property.</param>
        /// <param name="parameter">An optional converter parameter, currently unused.</param>
        /// <param name="culture">The culture information used for localization, currently unused.</param>
        /// <returns>The first line of the provided text, or <see cref="string.Empty"/> if the input is null.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var text = value.ToString();
            var firstLine = text?
                .Split(LineSeparators, StringSplitOptions.None)
                .FirstOrDefault();

            return firstLine ?? string.Empty;
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