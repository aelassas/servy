using Servy.UI.Localization;
using System.Globalization;

namespace Servy.UI.UnitTests.Localization
{
    public class LocalizationCultureTests
    {
        #region Normalize

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("en", "")]
        [InlineData("en-US", "")]
        [InlineData("fr-FR", "")]
        [InlineData("zh", "zh-CN")]
        [InlineData("zh-CN", "zh-CN")]
        [InlineData("zh-Hans", "zh-CN")]
        [InlineData("zh-Hans-CN", "zh-CN")]
        [InlineData("zh-SG", "zh-CN")]
        [InlineData("ZH-cn", "zh-CN")]
        public void Normalize_MapsToTheShippedCultures(string? input, string expected)
        {
            Assert.Equal(expected, LocalizationCulture.Normalize(input));
        }

        [Theory]
        [InlineData("zh-TW")]
        [InlineData("zh-HK")]
        [InlineData("zh-MO")]
        [InlineData("zh-Hant")]
        public void Normalize_TraditionalChinese_FallsBackToEnglish(string input)
        {
            Assert.Equal(LocalizationCulture.English, LocalizationCulture.Normalize(input));
        }

        #endregion

        #region FromCulture

        [Fact]
        public void FromCulture_Null_ReturnsEnglish()
        {
            Assert.Equal(LocalizationCulture.English, LocalizationCulture.FromCulture(null));
        }

        [Fact]
        public void FromCulture_SimplifiedChineseCulture_ReturnsChinese()
        {
            Assert.Equal(LocalizationCulture.ChineseSimplified,
                LocalizationCulture.FromCulture(CultureInfo.GetCultureInfo("zh-CN")));
        }

        [Fact]
        public void FromCulture_InvariantCulture_ReturnsEnglish()
        {
            Assert.Equal(LocalizationCulture.English, LocalizationCulture.FromCulture(CultureInfo.InvariantCulture));
        }

        #endregion

        #region Resolve

        [Theory]
        [InlineData(AppLanguage.System, "zh-CN", "zh-CN")]
        [InlineData(AppLanguage.System, "en-US", "")]
        [InlineData(AppLanguage.System, "zh-TW", "")]
        [InlineData(AppLanguage.English, "zh-CN", "")]
        [InlineData(AppLanguage.ChineseSimplified, "en-US", "zh-CN")]
        [InlineData(AppLanguage.ChineseSimplified, "zh-CN", "zh-CN")]
        public void Resolve_HonorsTheOverrideAboveTheSystemCulture(AppLanguage language, string systemCulture, string expected)
        {
            Assert.Equal(expected, LocalizationCulture.Resolve(language, CultureInfo.GetCultureInfo(systemCulture)));
        }

        [Fact]
        public void Resolve_SystemWithNullCulture_ReturnsEnglish()
        {
            Assert.Equal(LocalizationCulture.English, LocalizationCulture.Resolve(AppLanguage.System, null));
        }

        #endregion

        #region ToCulture

        [Fact]
        public void ToCulture_NeutralName_ReturnsInvariantCulture()
        {
            Assert.Same(CultureInfo.InvariantCulture, LocalizationCulture.ToCulture(""));
        }

        [Fact]
        public void ToCulture_ChineseName_ReturnsZhCn()
        {
            Assert.Equal("zh-CN", LocalizationCulture.ToCulture("zh").Name);
        }

        [Fact]
        public void ToCulture_UnsupportedName_ReturnsInvariantCulture()
        {
            Assert.Same(CultureInfo.InvariantCulture, LocalizationCulture.ToCulture("fr-FR"));
        }

        #endregion

        #region Parse / Serialize

        [Theory]
        [InlineData("System", AppLanguage.System)]
        [InlineData("English", AppLanguage.English)]
        [InlineData("ChineseSimplified", AppLanguage.ChineseSimplified)]
        [InlineData("english", AppLanguage.English)]
        [InlineData("  chinesesimplified  ", AppLanguage.ChineseSimplified)]
        public void Parse_KnownNames_ReturnsTheLanguage(string value, AppLanguage expected)
        {
            Assert.Equal(expected, LocalizationCulture.Parse(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Klingon")]
        [InlineData("7")]
        public void Parse_UnknownNames_ReturnSystem(string? value)
        {
            Assert.Equal(AppLanguage.System, LocalizationCulture.Parse(value));
        }

        [Theory]
        [InlineData(AppLanguage.System, "System")]
        [InlineData(AppLanguage.English, "English")]
        [InlineData(AppLanguage.ChineseSimplified, "ChineseSimplified")]
        public void Serialize_RoundTripsThroughParse(AppLanguage language, string expected)
        {
            var text = LocalizationCulture.Serialize(language);
            Assert.Equal(expected, text);
            Assert.Equal(language, LocalizationCulture.Parse(text));
        }

        #endregion

        #region SupportedCultures

        [Fact]
        public void SupportedCultures_AreTheNeutralCultureAndChinese()
        {
            Assert.Equal(new[] { "", "zh-CN" }, LocalizationCulture.SupportedCultures);
        }

        #endregion
    }
}
