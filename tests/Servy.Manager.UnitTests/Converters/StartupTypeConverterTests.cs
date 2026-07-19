using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using System.Globalization;
using System.Windows.Data;
using Xunit;

namespace Servy.Manager.UnitTests.Converters
{
    public class StartupTypeConverterTests
    {
        private readonly StartupTypeConverter _converter = new StartupTypeConverter();

        [Theory]
        [InlineData(ServiceStartType.Automatic, nameof(Strings.StartupType_Automatic))]
        [InlineData(ServiceStartType.AutomaticDelayedStart, nameof(Strings.StartupType_AutomaticDelayedStart))]
        [InlineData(ServiceStartType.Manual, nameof(Strings.StartupType_Manual))]
        [InlineData(ServiceStartType.Disabled, nameof(Strings.StartupType_Disabled))]
        [InlineData(ServiceStartType.Unknown, nameof(Strings.StartupType_Unknown))]
        public void Convert_ValidEnum_ReturnsLocalizedResource(ServiceStartType input, string resourceName)
        {
            // Act
            var result = _converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert - Retrieve the expected value from the ResourceManager dynamically
            var expected = typeof(Strings).GetProperty(resourceName)?.GetValue(null);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_NullReturnsFetching_InvalidEchoesInput()
        {
            // Act & Assert
            Assert.Equal(Strings.Label_Fetching, _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Equal("Invalid", _converter.Convert("Invalid", typeof(string), null, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void ConvertBack_NullInput_ReturnsDoNothing()
        {
            // Act & Assert
            Assert.Equal(Binding.DoNothing, _converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));
        }

        [Theory]
        [InlineData(nameof(Strings.StartupType_Automatic), ServiceStartType.Automatic)]
        [InlineData(nameof(Strings.StartupType_AutomaticDelayedStart), ServiceStartType.AutomaticDelayedStart)]
        [InlineData(nameof(Strings.StartupType_Manual), ServiceStartType.Manual)]
        [InlineData(nameof(Strings.StartupType_Disabled), ServiceStartType.Disabled)]
        [InlineData(nameof(Strings.StartupType_Unknown), ServiceStartType.Unknown)]
        public void ConvertBack_ValidString_ReturnsEnum(string resourceName, ServiceStartType expected)
        {
            // Arrange
            var input = typeof(Strings).GetProperty(resourceName)?.GetValue(null) as string;

            // Act
            var result = _converter.ConvertBack(input, typeof(ServiceStartType), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_InvalidString_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack("Invalid String", typeof(ServiceStartType), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}