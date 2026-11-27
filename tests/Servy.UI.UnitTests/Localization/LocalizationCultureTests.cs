using Servy.UI.Localization;
using System.Globalization;
using Xunit;

namespace Servy.UI.UnitTests.Localization
{
    public class LocalizationCultureTests
    {
        [Theory]
        [InlineData("system", AppLanguage.System)]
        [InlineData("SYSTEM", AppLanguage.System)]
        [InlineData("auto", AppLanguage.System)]
        [InlineData("en", AppLanguage.English)]
        [InlineData("EN", AppLanguage.English)]
        [InlineData("en-US", AppLanguage.English)]
        [InlineData("english", AppLanguage.English)]
        [InlineData(" en ", AppLanguage.English)]
        [InlineData("zh-CN", AppLanguage.ChineseSimplified)]
        [InlineData("zh-cn", AppLanguage.ChineseSimplified)]
        [InlineData("zh", AppLanguage.ChineseSimplified)]
        [InlineData("zh-Hans", AppLanguage.ChineseSimplified)]
        [InlineData("zh-Hans-SG", AppLanguage.ChineseSimplified)]
        [InlineData("zh-SG", AppLanguage.ChineseSimplified)]
        [InlineData("ChineseSimplified", AppLanguage.ChineseSimplified)]
        [InlineData("fr", AppLanguage.French)]
        [InlineData("FR", AppLanguage.French)]
        [InlineData("fr-FR", AppLanguage.French)]
        [InlineData("fr-CA", AppLanguage.French)]
        [InlineData("french", AppLanguage.French)]
        [InlineData("de", AppLanguage.German)]
        [InlineData("de-DE", AppLanguage.German)]
        [InlineData("de-AT", AppLanguage.German)]
        [InlineData("de-CH", AppLanguage.German)]
        [InlineData("German", AppLanguage.German)]
        [InlineData("es", AppLanguage.Spanish)]
        [InlineData("es-ES", AppLanguage.Spanish)]
        [InlineData("es-MX", AppLanguage.Spanish)]
        [InlineData("es-419", AppLanguage.Spanish)]
        [InlineData("spanish", AppLanguage.Spanish)]
        [InlineData("ja", AppLanguage.Japanese)]
        [InlineData("ja-JP", AppLanguage.Japanese)]
        [InlineData(" JA ", AppLanguage.Japanese)]
        [InlineData("Japanese", AppLanguage.Japanese)]
        [InlineData("it", AppLanguage.Italian)]
        [InlineData("it-IT", AppLanguage.Italian)]
        [InlineData("it-CH", AppLanguage.Italian)]
        [InlineData("Italian", AppLanguage.Italian)]
        [InlineData("pt-PT", AppLanguage.PortuguesePortugal)]
        [InlineData("PT-pt", AppLanguage.PortuguesePortugal)]
        [InlineData("pt-AO", AppLanguage.PortuguesePortugal)]
        [InlineData("PortuguesePortugal", AppLanguage.PortuguesePortugal)]
        [InlineData("pt-BR", AppLanguage.PortugueseBrazil)]
        [InlineData(" pt-br ", AppLanguage.PortugueseBrazil)]
        [InlineData("pt", AppLanguage.PortugueseBrazil)]
        [InlineData("PortugueseBrazil", AppLanguage.PortugueseBrazil)]
        public void TryParse_SupportedName_ReturnsTrueAndTheLanguage(string value, AppLanguage expected)
        {
            // Act
            var parsed = LocalizationCulture.TryParse(value, out var language);

            // Assert
            Assert.True(parsed);
            Assert.Equal(expected, language);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ko")]
        [InlineData("ru-RU")]
        [InlineData("ita")]
        [InlineData("ptx")]
        [InlineData("portuguese")]
        [InlineData("fra")]
        [InlineData("deu")]
        [InlineData("esp")]
        [InlineData("jap")]
        [InlineData("zh-TW")]
        [InlineData("zh-Hant")]
        [InlineData("zh-HK")]
        [InlineData("zhx")]
        [InlineData("end")]
        public void TryParse_UnsupportedName_ReturnsFalseAndSystem(string value)
        {
            // Act
            var parsed = LocalizationCulture.TryParse(value, out var language);

            // Assert
            Assert.False(parsed);
            Assert.Equal(AppLanguage.System, language);
        }

        [Theory]
        [InlineData("zh-CN", AppLanguage.ChineseSimplified)]
        [InlineData("klingon", AppLanguage.System)]
        [InlineData(null, AppLanguage.System)]
        public void ParseOrSystem_AnyName_FallsBackToSystemForAnUnsupportedOne(string value, AppLanguage expected)
        {
            // Act
            var language = LocalizationCulture.ParseOrSystem(value);

            // Assert
            Assert.Equal(expected, language);
        }

        [Theory]
        [InlineData(AppLanguage.System, "system")]
        [InlineData(AppLanguage.English, "en")]
        [InlineData(AppLanguage.ChineseSimplified, "zh-CN")]
        [InlineData(AppLanguage.French, "fr")]
        [InlineData(AppLanguage.German, "de")]
        [InlineData(AppLanguage.Spanish, "es")]
        [InlineData(AppLanguage.Japanese, "ja")]
        [InlineData(AppLanguage.Italian, "it")]
        [InlineData(AppLanguage.PortuguesePortugal, "pt-PT")]
        [InlineData(AppLanguage.PortugueseBrazil, "pt-BR")]
        public void Serialize_EveryLanguage_WritesTheNameTryParseReadsBack(AppLanguage language, string expected)
        {
            // Act
            var name = LocalizationCulture.Serialize(language);

            // Assert
            Assert.Equal(expected, name);
            Assert.True(LocalizationCulture.TryParse(name, out var roundTripped));
            Assert.Equal(language, roundTripped);
        }

        [Fact]
        public void Resolve_English_ReturnsTheEnglishCultureWhateverTheSystemLanguageIs()
        {
            // Act
            var culture = LocalizationCulture.Resolve(AppLanguage.English, CultureInfo.GetCultureInfo("zh-CN"));

            // Assert
            Assert.NotNull(culture);
            Assert.Equal("en", culture.Name);
        }

        [Fact]
        public void Resolve_ChineseSimplified_ReturnsZhCnWhateverTheSystemLanguageIs()
        {
            // Act
            var culture = LocalizationCulture.Resolve(AppLanguage.ChineseSimplified, CultureInfo.GetCultureInfo("fr-FR"));

            // Assert
            Assert.NotNull(culture);
            Assert.Equal("zh-CN", culture.Name);
        }

        [Theory]
        [InlineData(AppLanguage.French, "fr")]
        [InlineData(AppLanguage.German, "de")]
        [InlineData(AppLanguage.Spanish, "es")]
        [InlineData(AppLanguage.Japanese, "ja")]
        [InlineData(AppLanguage.Italian, "it")]
        public void Resolve_TranslatedLanguage_ReturnsItsNeutralCultureWhateverTheSystemLanguageIs(AppLanguage language, string expected)
        {
            // Act
            var culture = LocalizationCulture.Resolve(language, CultureInfo.GetCultureInfo("zh-CN"));

            // Assert
            Assert.NotNull(culture);
            Assert.Equal(expected, culture.Name);
        }

        [Theory]
        [InlineData("fr", "fr")]
        [InlineData("fr-FR", "fr")]
        [InlineData("fr-CA", "fr")]
        [InlineData("fr-BE", "fr")]
        [InlineData("de", "de")]
        [InlineData("de-DE", "de")]
        [InlineData("de-AT", "de")]
        [InlineData("de-CH", "de")]
        [InlineData("es", "es")]
        [InlineData("es-ES", "es")]
        [InlineData("es-MX", "es")]
        [InlineData("es-419", "es")]
        [InlineData("ja", "ja")]
        [InlineData("ja-JP", "ja")]
        [InlineData("it", "it")]
        [InlineData("it-IT", "it")]
        [InlineData("it-CH", "it")]
        public void Resolve_SystemOnATranslatedDisplayLanguage_ReturnsTheNeutralCultureOfThatLanguage(string systemCulture, string expected)
        {
            // Act
            var culture = LocalizationCulture.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo(systemCulture));

            // Assert
            Assert.NotNull(culture);
            Assert.Equal(expected, culture.Name);
        }

        [Theory]
        [InlineData(AppLanguage.PortuguesePortugal, "pt-PT")]
        [InlineData(AppLanguage.PortugueseBrazil, "pt-BR")]
        public void Resolve_Portuguese_ReturnsItsRegionalCultureWhateverTheSystemLanguageIs(AppLanguage language, string expected)
        {
            // Act
            var culture = LocalizationCulture.Resolve(language, CultureInfo.GetCultureInfo("ja-JP"));

            // Assert
            Assert.NotNull(culture);
            Assert.Equal(expected, culture.Name);
        }

        [Theory]
        [InlineData("pt-PT", "pt-PT")]
        [InlineData("pt-AO", "pt-PT")]
        [InlineData("pt-MZ", "pt-PT")]
        [InlineData("pt-BR", "pt-BR")]
        [InlineData("pt", "pt-BR")]
        public void Resolve_SystemOnAPortugueseDisplayLanguage_ReturnsBrazilianForBrazilAndNeutralPtAndEuropeanForTheOtherRegions(string systemCulture, string expected)
        {
            // Act
            var culture = LocalizationCulture.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo(systemCulture));

            // Assert
            Assert.NotNull(culture);
            Assert.Equal(expected, culture.Name);
        }

        [Theory]
        [InlineData("zh-CN")]
        [InlineData("zh-SG")]
        [InlineData("zh-Hans")]
        public void Resolve_SystemOnASimplifiedChineseDisplayLanguage_ReturnsZhCn(string systemCulture)
        {
            // Act
            var culture = LocalizationCulture.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo(systemCulture));

            // Assert
            Assert.NotNull(culture);
            Assert.Equal("zh-CN", culture.Name);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("ru-RU")]
        [InlineData("nl-NL")]
        [InlineData("ko-KR")]
        [InlineData("zh-TW")]
        [InlineData("zh-HK")]
        [InlineData("zh-Hant")]
        public void Resolve_SystemOnAnyOtherDisplayLanguage_LeavesTheCultureAlone(string systemCulture)
        {
            // Act
            var culture = LocalizationCulture.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo(systemCulture));

            // Assert
            Assert.Null(culture);
        }

        [Fact]
        public void Resolve_SystemWithoutASystemCulture_LeavesTheCultureAlone()
        {
            // Act
            var culture = LocalizationCulture.Resolve(AppLanguage.System, null);

            // Assert
            Assert.Null(culture);
        }

        [Theory]
        [InlineData("zh", true)]
        [InlineData("zh-CN", true)]
        [InlineData("ZH-cn", true)]
        [InlineData("zh-Hans-CN", true)]
        [InlineData("zh-CHS", true)]
        [InlineData("zh-Hans-HK", true)]
        [InlineData("zh-Hans-MO", true)]
        [InlineData("zh-TW", false)]
        [InlineData("zh-Hant-TW", false)]
        [InlineData("zh-CHT", false)]
        [InlineData("zh-MO", false)]
        [InlineData("zh-Hant-CN", false)]
        [InlineData("en-US", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsSimplifiedChineseName_CultureName_TellsSimplifiedFromTraditionalAndOtherLanguages(string name, bool expected)
        {
            // Act
            var simplified = LocalizationCulture.IsSimplifiedChineseName(name);

            // Assert
            Assert.Equal(expected, simplified);
        }
    }
}
