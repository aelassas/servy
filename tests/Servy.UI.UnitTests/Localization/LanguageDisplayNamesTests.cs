using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
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

            // Assert
            Assert.Equal(Strings.Language_System, system);
            Assert.Equal(Strings.Language_English, english);
            Assert.Equal(Strings.Language_ChineseSimplified, chinese);
            Assert.Equal(3, new[] { system, english, chinese }.Distinct().Count());
        }
    }
}
