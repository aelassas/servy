using Servy.Manager.Converters;
using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    public class PidConverterTests
    {
        private readonly PidConverter _converter = new PidConverter();

        [Fact]
        public void Convert_NullValue_ReturnsNotAvailablePlaceholder()
        {
            // Act
            var result = _converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(UiConstants.NotAvailable, result);
        }

        [Theory]
        [InlineData(1234, "1234")]
        [InlineData(0, "0")]
        [InlineData("999", "999")]
        public void Convert_ValidValue_ReturnsStringRepresentation(object input, string expected)
        {
            // Act
            var result = _converter.Convert(input, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack("1234", typeof(int), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}