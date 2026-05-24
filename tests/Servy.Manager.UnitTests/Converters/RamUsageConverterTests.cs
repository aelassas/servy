using Moq;
using Servy.Core.Helpers;
using Servy.Manager.Converters;
using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    public class RamUsageConverterTests
    {
        private readonly Mock<IProcessHelper> _mockProcessHelper;

        public RamUsageConverterTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
        }

        [Fact]
        public void Convert_ValidLong_ReturnsFormattedString()
        {
            // Arrange
            var converter = new RamUsageConverter();
            long input = 1024 * 1024 * 10; // 10MB

            // Note: This relies on the internal DI resolution. 
            // Ensure App.Services is mocked or configured if running in a full test host.
            _mockProcessHelper.Setup(h => h.FormatRamUsage(input)).Returns("10 MB");

            // Act
            var result = converter.Convert(input, typeof(string), null!, CultureInfo.CurrentUICulture);

            // Assert
            Assert.Equal("10.0 MB", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Not a long")]
        [InlineData(10.5)] // Double, not a long
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object? input)
        {
            // Arrange
            var converter = new RamUsageConverter();

            // Act
            var result = converter.Convert(input!, typeof(string), null!, CultureInfo.CurrentUICulture);

            // Assert
            Assert.Equal(UiConstants.NotAvailable, result);
        }

        [Fact]
        public void ConvertBack_ReturnsDoNothing()
        {
            // Arrange
            var converter = new RamUsageConverter();

            // Act
            var result = converter.ConvertBack(null!, typeof(long), null!, CultureInfo.CurrentUICulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}