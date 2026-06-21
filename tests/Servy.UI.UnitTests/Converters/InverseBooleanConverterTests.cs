using Servy.UI.Converters;
using System.Globalization;
using System.Windows.Data;
using Xunit;

namespace Servy.UI.UnitTests.Converters
{
    public class InverseBooleanConverterTests
    {
        private readonly InverseBooleanConverter _converter;

        public InverseBooleanConverterTests()
        {
            _converter = new InverseBooleanConverter();
        }

        #region Convert Tests

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void Convert_WhenValueIsBool_ReturnsInvertedValue(bool input, bool expected)
        {
            // Act
            var result = _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("string")]
        [InlineData(123)]
        public void Convert_WhenValueIsNotBool_ReturnsBindingDoNothing(object input)
        {
            // Act
            var result = _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        #endregion

        #region ConvertBack Tests

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ConvertBack_WhenValueIsBool_ReturnsInvertedValue(bool input, bool expected)
        {
            // Act
            var result = _converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not a bool")]
        public void ConvertBack_WhenValueIsNotBool_ReturnsDoNothing(object input)
        {
            // Act
            var result = _converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        #endregion
    }
}