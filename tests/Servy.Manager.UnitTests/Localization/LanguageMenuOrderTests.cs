using Servy.Core.Config;
using Servy.UI.Localization;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Servy.Manager.UnitTests.Localization
{
    /// <summary>
    /// Tests that the Language menu of the Servy Manager main window lists the languages in the canonical order
    /// the maintainer set on issue #8236.
    /// </summary>
    public class LanguageMenuOrderTests
    {
        /// <summary>
        /// Matches the <see cref="AppLanguage"/> member each Language menu item passes as its command parameter.
        /// </summary>
        private static readonly Regex CommandParameterPattern =
            new Regex(@"CommandParameter=""\{x:Static loc:AppLanguage\.(?<name>\w+)\}""", RegexOptions.Compiled);

        /// <summary>
        /// Verifies that the production <c>src/Servy.Manager/Views/MainWindow.xaml</c> offers "Follow system language"
        /// first and then English, French, German, Spanish, Portuguese (Brazil), Portuguese (European), Italian,
        /// Chinese (Simplified), Japanese and Korean, in that order.
        /// </summary>
        [Fact]
        public void LanguageMenu_ListsTheLanguagesInTheCanonicalOrder()
        {
            // Arrange
            var expected = new[]
            {
                AppLanguage.System,
                AppLanguage.English,
                AppLanguage.French,
                AppLanguage.German,
                AppLanguage.Spanish,
                AppLanguage.PortugueseBrazil,
                AppLanguage.PortuguesePortugal,
                AppLanguage.Italian,
                AppLanguage.ChineseSimplified,
                AppLanguage.Japanese,
                AppLanguage.Korean,
            };
            var path = Path.Combine(AppConfig.FindRepoRoot(AppContext.BaseDirectory), "src", "Servy.Manager", "Views", "MainWindow.xaml");
            var xaml = File.ReadAllText(path);

            // Act
            var actual = CommandParameterPattern.Matches(xaml)
                .Cast<Match>()
                .Select(m => (AppLanguage)Enum.Parse(typeof(AppLanguage), m.Groups["name"].Value))
                .ToArray();

            // Assert
            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual);
        }
    }
}
