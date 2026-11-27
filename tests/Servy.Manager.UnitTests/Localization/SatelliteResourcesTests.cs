using Servy.Manager.Resources;
using System.Globalization;
using Xunit;

namespace Servy.Manager.UnitTests.Localization
{
    public class SatelliteResourcesTests
    {
        [Theory]
        [InlineData("Menu_Language")]
        [InlineData("Automation_LanguageSystemMenuItem_HelpText")]
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

        [Theory]
        [InlineData("fr", "fr-CA", "Menu_Language")]
        [InlineData("fr", "fr-CA", "Automation_LanguageMenu_HelpText")]
        [InlineData("de", "de-CH", "Menu_Language")]
        [InlineData("de", "de-CH", "Automation_LanguageMenu_HelpText")]
        [InlineData("es", "es-419", "Menu_Language")]
        [InlineData("es", "es-419", "Automation_LanguageMenu_HelpText")]
        [InlineData("ja", "ja-JP", "Menu_Language")]
        [InlineData("ja", "ja-JP", "Automation_LanguageMenu_HelpText")]
        public void ResourceManager_TranslatedCulture_LoadsTheTranslationFromTheSatelliteAssembly(string neutralName, string regionalName, string key)
        {
            // Arrange: a regional culture of the language falls back to the satellite of its neutral culture
            var neutral = CultureInfo.GetCultureInfo(neutralName);
            var regional = CultureInfo.GetCultureInfo(regionalName);

            // Act
            var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
            var translated = Strings.ResourceManager.GetString(key, neutral);
            var regionalText = Strings.ResourceManager.GetString(key, regional);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.False(string.IsNullOrWhiteSpace(translated));
            Assert.NotEqual(english, translated);
            Assert.Equal(translated, regionalText);
        }

        [Fact]
        public void ResourceManager_UntranslatedCulture_FallsBackToTheEnglishText()
        {
            // Arrange
            var italian = CultureInfo.GetCultureInfo("it-IT");

            // Act
            var english = Strings.ResourceManager.GetString("Menu_Language", CultureInfo.InvariantCulture);
            var fallback = Strings.ResourceManager.GetString("Menu_Language", italian);

            // Assert
            Assert.Equal("Language", english);
            Assert.Equal(english, fallback);
        }
    }
}
