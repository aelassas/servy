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

        [Theory]
        [InlineData("fr", "fr-CA", "Language_System")]
        [InlineData("fr", "fr-CA", "Msg_LanguageRestartConfirm")]
        [InlineData("de", "de-CH", "Language_System")]
        [InlineData("de", "de-CH", "Msg_LanguageRestartConfirm")]
        [InlineData("es", "es-419", "Language_System")]
        [InlineData("es", "es-419", "Msg_LanguageRestartConfirm")]
        [InlineData("ja", "ja-JP", "Language_System")]
        [InlineData("ja", "ja-JP", "Msg_LanguageRestartConfirm")]
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
            var english = Strings.ResourceManager.GetString("Language_System", CultureInfo.InvariantCulture);
            var fallback = Strings.ResourceManager.GetString("Language_System", italian);

            // Assert
            Assert.Equal("Follow system language", english);
            Assert.Equal(english, fallback);
        }
    }
}
