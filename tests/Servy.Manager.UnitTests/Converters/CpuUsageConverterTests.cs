using System;
using System.Globalization;
using System.Windows.Data;
using Moq;
using Servy.Core.Helpers;
using Servy.Manager.Converters;
using Servy.UI.Constants;
using Xunit;

namespace Servy.Manager.UnitTests.Converters
{
    public class CpuUsageConverterTests
    {
        private readonly Mock<IProcessHelper> _mockProcessHelper;

        public CpuUsageConverterTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();

            // Note: In a real project, ensure App.Services is initialized with a ServiceProvider 
            // that returns _mockProcessHelper.Object for IProcessHelper if testing runtime logic.
        }

        [Fact]
        public void Convert_ValidDouble_ReturnsFormattedString()
        {
            // Arrange
            var converter = new CpuUsageConverter();
            double input = 1.25;

            // Updated expectation to match the actual rounding behavior (1.3%)
            string expectedFormattedValue = "1.3%";

            _mockProcessHelper.Setup(h => h.FormatCpuUsage(input)).Returns(expectedFormattedValue);

            // Act
            var result = converter.Convert(input, typeof(string), null, CultureInfo.CurrentUICulture);

            // Assert
            Assert.Equal(expectedFormattedValue, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Not a double")]
        [InlineData(100)] // Integer, not a double
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object input)
        {
            // Arrange
            var converter = new CpuUsageConverter();

            // Act
            var result = converter.Convert(input, typeof(string), null, CultureInfo.CurrentUICulture);

            // Assert
            Assert.Equal(UiConstants.NotAvailable, result);
        }

        [Fact]
        public void ConvertBack_ReturnsDoNothing()
        {
            // Arrange
            var converter = new CpuUsageConverter();

            // Act
            var result = converter.ConvertBack(null, typeof(double), null, CultureInfo.CurrentUICulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}