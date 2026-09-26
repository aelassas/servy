using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using System;
using System.Globalization;
using System.Windows.Data;
using Xunit;

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
            var result = _converter.Convert(level, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            string expected;
            switch (level)
            {
                case EventLogLevel.Information:
                    expected = Strings.LogLevel_Information;
                    break;
                case EventLogLevel.Warning:
                    expected = Strings.LogLevel_Warning;
                    break;
                case EventLogLevel.Error:
                    expected = Strings.LogLevel_Error;
                    break;
                case EventLogLevel.All:
                    expected = Strings.LogLevel_All;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }

            Assert.Equal(expected, result);
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
