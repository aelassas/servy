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
    }
}
