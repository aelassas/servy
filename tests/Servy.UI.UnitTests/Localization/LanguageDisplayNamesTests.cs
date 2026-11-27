using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
using System.Globalization;
using Xunit;

namespace Servy.UI.UnitTests.Localization
{
    public class LanguageDisplayNamesTests
    {
        [Fact]
        public void For_EveryLanguage_ReturnsItsOwnResourceString()
        {
            // Act
            var system = LanguageDisplayNames.For(AppLanguage.System);
            var english = LanguageDisplayNames.For(AppLanguage.English);
            var chinese = LanguageDisplayNames.For(AppLanguage.ChineseSimplified);
            var french = LanguageDisplayNames.For(AppLanguage.French);
            var german = LanguageDisplayNames.For(AppLanguage.German);
            var spanish = LanguageDisplayNames.For(AppLanguage.Spanish);
            var japanese = LanguageDisplayNames.For(AppLanguage.Japanese);
            var italian = LanguageDisplayNames.For(AppLanguage.Italian);
            var portuguesePortugal = LanguageDisplayNames.For(AppLanguage.PortuguesePortugal);
            var portugueseBrazil = LanguageDisplayNames.For(AppLanguage.PortugueseBrazil);

            // Assert
            Assert.Equal(Strings.Language_System, system);
            Assert.Equal(Strings.Language_English, english);
            Assert.Equal(Strings.Language_ChineseSimplified, chinese);
            Assert.Equal(Strings.Language_French, french);
            Assert.Equal(Strings.Language_German, german);
            Assert.Equal(Strings.Language_Spanish, spanish);
            Assert.Equal(Strings.Language_Japanese, japanese);
            Assert.Equal(Strings.Language_Italian, italian);
            Assert.Equal(Strings.Language_PortuguesePortugal, portuguesePortugal);
            Assert.Equal(Strings.Language_PortugueseBrazil, portugueseBrazil);
            Assert.Equal(10, new[] { system, english, chinese, french, german, spanish, japanese, italian, portuguesePortugal, portugueseBrazil }.Distinct().Count());
        }

        [Theory]
        [InlineData("")]
        [InlineData("de")]
        [InlineData("es")]
        [InlineData("fr")]
        [InlineData("ja")]
        [InlineData("it")]
        [InlineData("pt-PT")]
        [InlineData("pt-BR")]
        [InlineData("zh-CN")]
        public void LanguageEnglish_EveryCulture_StartsWithTheNativeName(string cultureName)
        {
            // Arrange: the Language menu lists each language under its own name first, so a user
            // who cannot read the current UI language can still find English
            var culture = CultureInfo.GetCultureInfo(cultureName);

            // Act
            var name = Strings.ResourceManager.GetString("Language_English", culture);

            // Assert
            Assert.NotNull(name);
            Assert.StartsWith("English", name, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("de")]
        [InlineData("es")]
        [InlineData("fr")]
        [InlineData("ja")]
        [InlineData("it")]
        [InlineData("pt-PT")]
        [InlineData("pt-BR")]
        [InlineData("zh-CN")]
        public void LanguageChineseSimplified_EveryCulture_StartsWithTheNativeName(string cultureName)
        {
            // Arrange: the native name of Simplified Chinese is \u7B80\u4F53\u4E2D\u6587, spelled with
            // escapes so this file stays ASCII
            var culture = CultureInfo.GetCultureInfo(cultureName);
            const string nativeName = "\u7B80\u4F53\u4E2D\u6587";

            // Act
            var name = Strings.ResourceManager.GetString("Language_ChineseSimplified", culture);

            // Assert
            Assert.NotNull(name);
            Assert.StartsWith(nativeName, name, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("Language_Italian", "Italiano")]
        [InlineData("Language_PortuguesePortugal", "Portugu\u00EAs (Portugal)")]
        [InlineData("Language_PortugueseBrazil", "Portugu\u00EAs (Brasil)")]
        public void LanguageItalianAndPortuguese_EveryCulture_StartWithTheNativeName(string key, string nativeName)
        {
            // Arrange: the native names are spelled with escapes so this file stays ASCII
            var cultureNames = new[] { "", "de", "es", "fr", "ja", "it", "pt-PT", "pt-BR", "zh-CN" };

            // Act
            var names = cultureNames
                .Select(cultureName => Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(cultureName)))
                .ToList();

            // Assert
            Assert.All(names, name =>
            {
                Assert.NotNull(name);
                Assert.StartsWith(nativeName, name, StringComparison.Ordinal);
            });
        }
    }
}
