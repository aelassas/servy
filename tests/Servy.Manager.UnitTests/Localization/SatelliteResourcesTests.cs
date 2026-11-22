using Servy.Manager.Resources;
using System.Globalization;
using Xunit;

namespace Servy.Manager.UnitTests.Localization
{
    public class SatelliteResourcesTests
    {
        [Theory]
        [InlineData("Menu_Language")]
        [InlineData("Language_System")]
        [InlineData("Automation_LanguageMenu_HelpText")]
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
            var english = Strings.ResourceManager.GetString("Menu_Language", CultureInfo.InvariantCulture);
            var fallback = Strings.ResourceManager.GetString("Menu_Language", french);

            // Assert
            Assert.Equal("Language", english);
            Assert.Equal(english, fallback);
        }
    }
}
