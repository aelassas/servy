using System;
using System.Globalization;
using System.Windows.Data;
using Servy.Manager.Converters;
using Xunit;

namespace Servy.Manager.UnitTests.Converters
{
    public class MessageConverterTests
    {
        private readonly MessageConverter _converter = new MessageConverter();

        [Fact]
        public void Convert_NullValue_ReturnsEmptyString()
        {
            // Act
            var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("Single line", "Single line")]
        [InlineData("First line\nSecond line", "First line")]
        [InlineData("First line\r\nSecond line", "First line")]
        [InlineData("First line\rSecond line", "First line")]
        [InlineData("", "")]
        public void Convert_ValidString_ReturnsFirstLine(string input, string expected)
        {
            // Act
            var result = _converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack("test", typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}