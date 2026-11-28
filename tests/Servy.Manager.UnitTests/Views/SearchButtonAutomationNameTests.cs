using Servy.Core.Config;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Servy.Manager.UnitTests.Views
{
    /// <summary>
    /// Tests that every Servy Manager Search button whose caption binds <c>SearchButtonText</c> gives UI Automation
    /// the same bound text, so a screen reader announces "Searching..." while a search runs (issue #8241).
    /// </summary>
    public class SearchButtonAutomationNameTests
    {
        /// <summary>
        /// Matches one <c>Button</c> start tag, attributes included.
        /// </summary>
        private static readonly Regex ButtonTagPattern =
            new Regex(@"<Button\b[^>]*>", RegexOptions.Compiled);

        /// <summary>
        /// Matches a <c>Content</c> attribute that binds <c>SearchButtonText</c>.
        /// </summary>
        private static readonly Regex SearchContentPattern =
            new Regex(@"\sContent=""\{Binding SearchButtonText\b", RegexOptions.Compiled);

        /// <summary>
        /// Matches an <c>AutomationProperties.Name</c> attribute that binds <c>SearchButtonText</c>.
        /// </summary>
        private static readonly Regex SearchAutomationNamePattern =
            new Regex(@"\sAutomationProperties\.Name=""\{Binding SearchButtonText\b", RegexOptions.Compiled);

        /// <summary>
        /// Matches a fixed <c>Width</c> attribute (not <c>MinWidth</c> or <c>MaxWidth</c>).
        /// </summary>
        private static readonly Regex FixedWidthPattern =
            new Regex(@"\sWidth=""", RegexOptions.Compiled);

        /// <summary>
        /// Matches the <c>Padding="8,0"</c> attribute that keeps space around a caption wider than the minimum width.
        /// </summary>
        private static readonly Regex PaddingPattern =
            new Regex(@"\sPadding=""8,0""", RegexOptions.Compiled);

        /// <summary>
        /// Verifies that the Search button in the given production view under <c>src/Servy.Manager/Views</c> binds its
        /// <c>AutomationProperties.Name</c> to <c>SearchButtonText</c>, the same property its <c>Content</c> binds.
        /// </summary>
        /// <param name="relativePath">The view's path relative to <c>src/Servy.Manager/Views</c>, with <c>/</c> separators.</param>
        [Theory]
        [InlineData("MainWindow.xaml")]
        [InlineData("LogsView.xaml")]
        [InlineData("Controls/ServiceListControl.xaml")]
        public void SearchButton_AutomationNameBindsSearchButtonText(string relativePath)
        {
            // Arrange
            var segments = new[] { AppConfig.FindRepoRoot(AppContext.BaseDirectory), "src", "Servy.Manager", "Views" }
                .Concat(relativePath.Split('/'))
                .ToArray();
            var xaml = File.ReadAllText(Path.Combine(segments));

            // Act
            var searchButtons = ButtonTagPattern.Matches(xaml)
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(tag => SearchContentPattern.IsMatch(tag))
                .ToArray();

            // Assert
            Assert.Single(searchButtons);
            Assert.Matches(SearchAutomationNamePattern, searchButtons[0]);
        }

        /// <summary>
        /// Verifies that the Search button in the given production view under <c>src/Servy.Manager/Views</c> has a
        /// <c>MinWidth</c> and horizontal <c>Padding</c> instead of a fixed <c>Width</c>, so a longer translated caption
        /// such as the one shown while a search runs widens the button instead of being clipped (issue #8240).
        /// </summary>
        /// <param name="relativePath">The view's path relative to <c>src/Servy.Manager/Views</c>, with <c>/</c> separators.</param>
        /// <param name="expectedMinWidth">The button's expected <c>MinWidth</c> value, its former fixed width.</param>
        [Theory]
        [InlineData("MainWindow.xaml", "80")]
        [InlineData("LogsView.xaml", "80")]
        [InlineData("Controls/ServiceListControl.xaml", "92")]
        public void SearchButton_GrowsWithCaption(string relativePath, string expectedMinWidth)
        {
            // Arrange
            var segments = new[] { AppConfig.FindRepoRoot(AppContext.BaseDirectory), "src", "Servy.Manager", "Views" }
                .Concat(relativePath.Split('/'))
                .ToArray();
            var xaml = File.ReadAllText(Path.Combine(segments));
            var minWidthPattern = new Regex(@"\sMinWidth=""" + expectedMinWidth + @"""");

            // Act
            var searchButtons = ButtonTagPattern.Matches(xaml)
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(tag => SearchContentPattern.IsMatch(tag))
                .ToArray();

            // Assert
            Assert.Single(searchButtons);
            Assert.DoesNotMatch(FixedWidthPattern, searchButtons[0]);
            Assert.Matches(minWidthPattern, searchButtons[0]);
            Assert.Matches(PaddingPattern, searchButtons[0]);
        }
    }
}
