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
        [InlineData("fr")]
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
        [InlineData("fr-FR")]
        [InlineData("de-DE")]
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
        [InlineData("zh-TW", false)]
        [InlineData("zh-Hant-TW", false)]
        [InlineData("zh-CHT", false)]
        [InlineData("zh-MO", false)]
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
