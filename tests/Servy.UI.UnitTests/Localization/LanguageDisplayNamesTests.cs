using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
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

            // Assert
            Assert.Equal(Strings.Language_System, system);
            Assert.Equal(Strings.Language_English, english);
            Assert.Equal(Strings.Language_ChineseSimplified, chinese);
            Assert.Equal(Strings.Language_French, french);
            Assert.Equal(Strings.Language_German, german);
            Assert.Equal(Strings.Language_Spanish, spanish);
            Assert.Equal(Strings.Language_Japanese, japanese);
            Assert.Equal(7, new[] { system, english, chinese, french, german, spanish, japanese }.Distinct().Count());
        }
    }
}
