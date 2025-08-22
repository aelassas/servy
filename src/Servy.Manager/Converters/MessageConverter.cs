using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a multi-line message to its first line.
    /// </summary>
    public class MessageConverter : IValueConverter
    {
        /// <summary>
        /// Returns the first line of the message (splitting on \r, \n).
        /// </summary>
        /// <param name="value">The original message (string).</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>The first line, or empty string if null/empty.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var text = value.ToString();
            var firstLine = text
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .FirstOrDefault();

            return firstLine ?? string.Empty;
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
