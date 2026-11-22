using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using Servy.Testing;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    public class LogLevelConverterTests
    {
        private readonly LogLevelConverter _converter = new LogLevelConverter();

        [Theory]
        [InlineData(EventLogLevel.Information)]
        [InlineData(EventLogLevel.Warning)]
        [InlineData(EventLogLevel.Error)]
        [InlineData(EventLogLevel.All)]
        public void Convert_MappedLogLevelEnum_ReturnsLocalizedString(EventLogLevel level)
        {
            // Act
            // Both the conversion and the expected value below read the resources, so they share the English culture.
            var result = EnglishCulture.Run(() => _converter.Convert(level, typeof(string), null, CultureInfo.InvariantCulture));
            var expectedResource = EnglishCulture.Run(() =>
            {
                switch (level)
                {
                    case EventLogLevel.Information: return Strings.LogLevel_Information;
                    case EventLogLevel.Warning: return Strings.LogLevel_Warning;
                    case EventLogLevel.Error: return Strings.LogLevel_Error;
                    default: return Strings.LogLevel_All;
                }
            });

            // Assert
            Assert.Equal(expectedResource, result);
        }

        [Theory]
        [InlineData(EventLogLevel.Critical)]
        [InlineData(EventLogLevel.Verbose)]
        public void Convert_UnmappedLogLevelEnum_ReturnsFallbackToString(EventLogLevel level)
        {
            // Act
            var result = _converter.Convert(level, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(level.ToString(), result);
        }

        [Fact]
        public void Convert_NullInput_ReturnsFallbackEmptyString()
        {
            // Act
            var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Convert_NonEnumInput_ReturnsFallbackToString()
        {
            // Arrange
            const string invalidValue = "NotAnEventLogLevel";

            // Act
            var result = _converter.Convert(invalidValue, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(invalidValue, result);
        }

        [Fact]
        public void ConvertBack_ReturnsBindingDoNothing()
        {
            // Act
            var result = _converter.ConvertBack("Information", typeof(EventLogLevel), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }
    }
}
