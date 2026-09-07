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
        [InlineData("Not a pid", "Not a pid")] // Incompatible type: echoed, never replaced by the placeholder
        public void Convert_NonNullValue_ReturnsStringRepresentation(object input, string expected)
        {
            // Act
            var result = _converter.Convert(input, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Any UI value")]
        public void ConvertBack_ReturnsDoNothing(object? value)
        {
            // Act
            var result = _converter.ConvertBack(value!, typeof(int), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}
