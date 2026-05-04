using Servy.UI.Converters;
using System.Globalization;
using System.Windows;
using Xunit;

namespace Servy.UI.UnitTests.Converters
{
    public class BoolToVisibilityConverterTests
    {
        private readonly BoolToVisibilityConverter _converter;

        public BoolToVisibilityConverterTests()
        {
            _converter = new BoolToVisibilityConverter();
        }

        #region Convert Tests

        [Theory]
        [InlineData(true, Visibility.Visible)]
        [InlineData(false, Visibility.Collapsed)]
        public void Convert_BoolValue_ReturnsExpectedVisibility(bool input, Visibility expected)
        {
            // Act
            var result = _converter.Convert(input, typeof(Visibility), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not a bool")]
        [InlineData(1)]
        public void Convert_InvalidValue_ReturnsCollapsed(object input)
        {
            // Act
            var result = _converter.Convert(input, typeof(Visibility), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        #endregion

        #region ConvertBack Tests

        [Fact]
        public void ConvertBack_Visible_ReturnsTrue()
        {
            // Act
            var result = _converter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.True((bool)result);
        }

        [Theory]
        [InlineData(Visibility.Collapsed)]
        [InlineData(Visibility.Hidden)]
        public void ConvertBack_NotVisible_ReturnsFalse(Visibility input)
        {
            // Act
            var result = _converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.False((bool)result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Visible")]
        [InlineData(true)]
        public void ConvertBack_InvalidType_ReturnsFalse(object input)
        {
            // Act
            var result = _converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.False((bool)result);
        }

        #endregion
    }
}