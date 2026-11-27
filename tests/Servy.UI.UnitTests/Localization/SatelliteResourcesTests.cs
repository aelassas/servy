using Servy.UI.Resources;
using System.Globalization;
using Xunit;

namespace Servy.UI.UnitTests.Localization
{
    public class SatelliteResourcesTests
    {
        [Theory]
        [InlineData("Language_System")]
        [InlineData("Language_ChineseSimplified")]
        [InlineData("Msg_LanguageRestartConfirm")]
        public void ResourceManager_ChineseCulture_LoadsTheTranslationFromTheSatelliteAssembly(string key)
        {
            // Arrange
            var chinese = CultureInfo.GetCultureInfo("zh-CN");

            // Act
            var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
            var translated = Strings.ResourceManager.GetString(key, chinese);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.False(string.IsNullOrWhiteSpace(translated));
            Assert.NotEqual(english, translated);
        }

        [Fact]
        public void ResourceManager_FrenchCulture_FallsBackToTheEnglishText()
        {
            // Arrange
            var french = CultureInfo.GetCultureInfo("fr-FR");

            // Act
            var english = Strings.ResourceManager.GetString("Language_System", CultureInfo.InvariantCulture);
            var fallback = Strings.ResourceManager.GetString("Language_System", french);

            // Assert
            Assert.Equal("Follow system language", english);
            Assert.Equal(english, fallback);
        }
    }
}
