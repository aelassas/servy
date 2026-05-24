using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    public class StatusConverterTests
    {
        private readonly StatusConverter _converter = new StatusConverter();

        [Theory]
        [InlineData(ServiceStatus.None, nameof(Strings.Label_Fetching))]
        [InlineData(ServiceStatus.Running, nameof(Strings.Status_Running))]
        [InlineData(ServiceStatus.Stopped, nameof(Strings.Status_Stopped))]
        public void Convert_ValidStatus_ReturnsLocalizedResource(ServiceStatus status, string resourceName)
        {
            // Act
            var result = _converter.Convert(status, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            var expected = typeof(Strings).GetProperty(resourceName)?.GetValue(null!);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_UnknownStatus_ReturnsToString()
        {
            // Arrange: Cast an undefined integer to the enum
            var unknownStatus = (ServiceStatus)999;

            // Act
            var result = _converter.Convert(unknownStatus, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(unknownStatus.ToString(), result);
        }

        [Fact]
        public void Convert_NullInput_ReturnsEmptyString()
        {
            // Act
            var result = _converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData(nameof(Strings.Status_Running), ServiceStatus.Running)]
        [InlineData(nameof(Strings.Status_Stopped), ServiceStatus.Stopped)]
        public void ConvertBack_ValidString_ReturnsEnum(string resourceName, ServiceStatus expected)
        {
            // Arrange
            var input = (string)typeof(Strings).GetProperty(resourceName)!.GetValue(null!)!;

            // Act
            var result = _converter.ConvertBack(input, typeof(ServiceStatus), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_InvalidString_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack("Unknown status string", typeof(ServiceStatus), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}