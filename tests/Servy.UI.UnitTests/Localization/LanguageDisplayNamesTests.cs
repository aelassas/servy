using Servy.UI.Localization;
using Servy.UI.Services;
using System.Globalization;

namespace Servy.UI.UnitTests.Localization
{
    public class LanguageDisplayNamesTests
    {
        [Fact]
        public void For_English_ReturnsTheLocalizedEnglishLabel()
        {
            Assert.Equal(Servy.UI.Resources.Strings.Language_English, LanguageDisplayNames.For(AppLanguage.English));
        }

        [Fact]
        public void For_ChineseSimplified_ReturnsTheChineseLabel()
        {
            Assert.Equal("简体中文", LanguageDisplayNames.For(AppLanguage.ChineseSimplified));
        }

        [Fact]
        public void For_System_ReturnsTheFollowSystemLabel()
        {
            Assert.Equal(Servy.UI.Resources.Strings.Language_System, LanguageDisplayNames.For(AppLanguage.System));
        }

        [Fact]
        public void For_ChineseSimplified_IsTranslatedInZhCn()
        {
            using (new TemporaryCultureScope("zh-CN"))
            {
                var chinese = LanguageDisplayNames.For(AppLanguage.System);
                var english = LanguageDisplayNames.For(AppLanguage.English);

                Assert.Equal("跟随系统语言", chinese);
                Assert.Equal("English", english);
            }
        }
    }
}
