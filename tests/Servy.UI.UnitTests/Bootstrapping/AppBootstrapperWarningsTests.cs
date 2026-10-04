using Servy.Testing;
using Servy.UI.Bootstrapping;
using System;
using System.Collections.Generic;
using Xunit;

namespace Servy.UI.UnitTests.Bootstrapping
{
    /// <summary>
    /// Covers <c>AppBootstrapper.CombineWarnings</c>, which turns the non-critical start-up warnings collected
    /// while the Servy services were paused into the one dialog shown after the resume, instead of one dialog
    /// per warning in a row.
    /// </summary>
    public class AppBootstrapperWarningsTests
    {
        /// <summary>
        /// Invokes the private <c>CombineWarnings</c> helper of <see cref="AppBootstrapper"/>.
        /// </summary>
        /// <param name="warnings">The collected warnings, in order.</param>
        /// <returns>The message and caption of the single dialog.</returns>
        private static (string Message, string Title) Combine(params (string Message, string Title)[] warnings)
            => ((string Message, string Title))TestReflection.InvokeNonPublicStatic(
                typeof(AppBootstrapper), "CombineWarnings", new List<(string Message, string Title)>(warnings));

        [Fact]
        public void CombineWarnings_OneWarning_KeepsItsOwnMessageAndCaption()
        {
            // Arrange
            var warning = ("The Servy host could not be started.", "Servy Host Unavailable");

            // Act
            var (message, title) = Combine(warning);

            // Assert
            Assert.Equal("The Servy host could not be started.", message);
            Assert.Equal("Servy Host Unavailable", title);
        }

        [Fact]
        public void CombineWarnings_SeveralWarnings_ListsEveryMessageInOrderUnderTheGenericCaption()
        {
            // Arrange
            var first = ("Failed to copy handle64.exe.", "Resource Extraction Warning");
            var second = ("The Servy host could not be started.", "Servy Host Unavailable");

            // Act
            var (message, title) = Combine(first, second);

            // Assert
            var separator = Environment.NewLine + Environment.NewLine;
            Assert.Equal("Failed to copy handle64.exe." + separator + "The Servy host could not be started.", message);
            Assert.Equal(Resources.Strings.Msg_StartupWarningsTitle, title);
            Assert.Equal("Servy Startup Warnings", title);
        }
    }
}
