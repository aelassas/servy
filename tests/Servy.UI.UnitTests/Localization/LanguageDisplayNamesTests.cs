using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        public static IEnumerable<object[]> NativeNameCases()
        {
            // The native names are spelled with escapes so this file stays ASCII
            var keys = new[]
            {
                new[] { "Language_English", "English" },
                new[] { "Language_ChineseSimplified", "\u7B80\u4F53\u4E2D\u6587" },
                new[] { "Language_French", "Fran\u00E7ais" },
                new[] { "Language_German", "Deutsch" },
                new[] { "Language_Spanish", "Espa\u00F1ol" },
                new[] { "Language_Japanese", "\u65E5\u672C\u8A9E" },
                new[] { "Language_Italian", "Italiano" },
                new[] { "Language_PortuguesePortugal", "Portugu\u00EAs (Portugal)" },
                new[] { "Language_PortugueseBrazil", "Portugu\u00EAs (Brasil)" },
            };
            var cultures = new[] { "", "de", "es", "fr", "ja", "zh-CN", "it", "pt-PT", "pt-BR" };

            foreach (var culture in cultures)
            {
                foreach (var key in keys)
                {
                    yield return new object[] { key[0], key[1], culture };
                }
            }
        }

        [Theory]
        [MemberData(nameof(NativeNameCases))]
        public void LanguageName_EveryCulture_StartsWithTheNativeName(string key, string nativeName, string cultureName)
        {
            // Arrange: the Language menu lists each language under its own name first, so a user
            // who cannot read the current UI language can still find their own
            var culture = CultureInfo.GetCultureInfo(cultureName);

            // Act
            var name = Strings.ResourceManager.GetString(key, culture);

            // Assert
            Assert.NotNull(name);
            Assert.StartsWith(nativeName, name, StringComparison.Ordinal);
        }
    }
}
