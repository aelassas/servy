using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using Servy.UI.Localization;
using System.Globalization;

namespace Servy.Manager.UnitTests.Converters
{
    /// <summary>
    /// Pins that the converters read the resources when they convert, so their text follows the culture
    /// the application applied at start-up instead of the one in effect when the type was first loaded.
    /// </summary>
    public class LocalizedConverterCultureTests
    {
        #region StatusConverter

        [Fact]
        public void StatusConverter_ConvertsWithTheCultureInEffectAtConversionTime()
        {
            // Arrange
            var converter = new StatusConverter();

            using (new TemporaryCultureScope("zh-CN", processWide: false))
            {
                // Act
                var chinese = converter.Convert(ServiceStatus.Running, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal("正在运行", chinese);
            }
        }

        #endregion

        #region StartupTypeConverter

        [Fact]
        public void StartupTypeConverter_ConvertsWithTheCultureInEffectAtConversionTime()
        {
            // Arrange
            var converter = new StartupTypeConverter();

            using (new TemporaryCultureScope("zh-CN", processWide: false))
            {
                // Act
                var chinese = converter.Convert(ServiceStartType.Manual, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal("手动", chinese);
            }
        }

        #endregion

        #region LogLevelConverter

        [Fact]
        public void LogLevelConverter_ConvertsWithTheCultureInEffectAtConversionTime()
        {
            // Arrange
            var converter = new LogLevelConverter();

            using (new TemporaryCultureScope("zh-CN", processWide: false))
            {
                // Act
                var chinese = converter.Convert(EventLogLevel.Warning, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal("警告", chinese);
            }
        }

        #endregion

        #region Not available placeholder

        [Fact]
        public void PidConverter_NullPid_ReturnsThePlaceholderOfTheCurrentCulture()
        {
            // Arrange
            var converter = new PidConverter();

            using (new TemporaryCultureScope("zh-CN", processWide: false))
            {
                // Act
                var chinese = converter.Convert(null, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal("不适用", chinese);
            }
        }

        #endregion
    }
}
