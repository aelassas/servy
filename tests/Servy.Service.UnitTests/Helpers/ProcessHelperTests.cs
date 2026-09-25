using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Core.RegexWrapper;
using Servy.Service.Helpers;
using System.Text.RegularExpressions;

namespace Servy.Service.UnitTests.Helpers
{
    [Collection(SequentialEnvTestsCollection.Name)]
    public class ProcessHelperTests
    {
        private readonly Mock<IServyLogger> _mockLogger;

        public ProcessHelperTests()
        {
            _mockLogger = new Mock<IServyLogger>();
        }

        /// <summary>
        /// Verifies that no unexpected warnings were logged, filtering out host/diagnostic noise.
        /// </summary>
        private void VerifyNoRealWarnings()
        {
            // Ignore diagnostic/host warnings injected during --blame-crash test runs
            _mockLogger.Verify(l => l.Warn(
                It.Is<string>(msg => !msg.Contains("COMPlus_") && !msg.Contains("DOTNET_")),
                It.IsAny<Exception>()),
                Times.Never);
        }

        [Theory]
        [InlineData("%PATH%")]
        [InlineData("%ProgramFiles(x86)%")]
        [InlineData("%CommonProgramFiles(x86)%")]
        [InlineData("%DOTNET_ROOT(x86)%")]
        [InlineData("%MY-VAR-NAME%")]
        [InlineData("%VAR.NAME%")]
        [InlineData("%_UNDER_SCORE_123%")]
        public void EnvVarRegex_ValidPlaceholders_MatchesExpectedVariables(string input)
        {
            // Act
            var matches = ProcessHelper.EnvVarRegex.MatchValues(input).ToList();

            // Assert
            Assert.Single(matches);
            Assert.Equal(input, matches[0]);
        }

        [Fact]
        public void EnvVarRegex_MultiplePlaceholdersInString_FindsAllMatches()
        {
            // Arrange
            string input = @"C:\Program Files\%MY_APP%\bin;%ProgramFiles(x86)%\Common;%DOTNET_ROOT%";

            // Act
            var matches = ProcessHelper.EnvVarRegex.MatchValues(input).ToList();

            // Assert
            Assert.Equal(3, matches.Count);
            Assert.Equal("%MY_APP%", matches[0]);
            Assert.Equal("%ProgramFiles(x86)%", matches[1]);
            Assert.Equal("%DOTNET_ROOT%", matches[2]);
        }

        [Theory]
        [InlineData("%%")]                  // Empty placeholder
        [InlineData("%VAR=VALUE%")]         // Contains assignment operator '='
        [InlineData("%VAR\rNAME%")]         // Contains carriage return
        [InlineData("%VAR\nNAME%")]         // Contains newline
        [InlineData("NO_PERCENTS_HERE")]    // Plain string
        public void EnvVarRegex_InvalidOrMalformedPlaceholders_DoesNotMatch(string input)
        {
            // Act
            var matches = ProcessHelper.EnvVarRegex.MatchValues(input).ToList();

            // Assert
            Assert.Empty(matches);
        }

        [Fact]
        public void ExpandAndAudit_WhenValid_ReturnsExpandedValuesWithoutWarnings()
        {
            // Arrange
            var vars = new List<EnvironmentVariable> { new EnvironmentVariable { Name = "VAR", Value = "Value" } };
            string args = "arg1";

            // Act
            var result = ProcessHelper.ExpandAndAudit(vars, args, _mockLogger.Object, "Test");

            // Assert
            Assert.Equal("Value", result.env["VAR"]);
            Assert.Equal("arg1", result.expandedArgs);

            VerifyNoRealWarnings();
        }

        [Fact]
        public void ExpandAndAudit_WithUnexpandedPlaceholders_LogsWarnings()
        {
            // Arrange
            // Simulate that expansion failed to resolve the placeholder
            var vars = new List<EnvironmentVariable> { new EnvironmentVariable { Name = "VAR", Value = "%MISSING%" } };
            string args = "run %UNKNOWN%";

            // Act
            ProcessHelper.ExpandAndAudit(vars, args, _mockLogger.Object, "Prefix");

            // Assert
            _mockLogger.Verify(l => l.Warn(
                It.Is<string>(msg => msg == "Unexpanded environment variable %MISSING% in [Prefix] Environment Variable 'VAR'"),
                It.IsAny<Exception>()),
                Times.Once);

            _mockLogger.Verify(l => l.Warn(
                It.Is<string>(msg => msg == "Unexpanded environment variable %UNKNOWN% in [Prefix] Arguments"),
                It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void ExpandAndAudit_HandlesEmptyInputGracefully()
        {
            // Act - should not throw or log for test inputs
            ProcessHelper.ExpandAndAudit(new List<EnvironmentVariable>(), "", _mockLogger.Object);

            // Assert
            VerifyNoRealWarnings();
        }

        [Fact]
        public void ExpandAndAudit_HandlesRegexTimeout_LogsError()
        {
            // Arrange
            var mockRegex = new Mock<IRegexWrapper>();
            mockRegex.Setup(r => r.MatchValues(It.IsAny<string>()))
                     .Throws(new RegexMatchTimeoutException());

            // Swap the static wrapper for the mock
            var original = ProcessHelper.EnvVarRegex;
            ProcessHelper.EnvVarRegex = mockRegex.Object;

            try
            {
                // Act
                // Pass a custom environment variable so the audit step executes
                ProcessHelper.ExpandAndAudit(new List<EnvironmentVariable> { new EnvironmentVariable { Name = "TRIGGER", Value = "trigger" } }, "trigger", _mockLogger.Object);

                // Assert
                _mockLogger.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Regex timeout")),
                    It.IsAny<RegexMatchTimeoutException>()),
                    Times.AtLeastOnce);
            }
            finally
            {
                // Restore original
                ProcessHelper.EnvVarRegex = original;
            }
        }

        [Fact]
        public void ExpandAndAudit_StubbedMatchValues_WarnsOncePerReturnedPlaceholder()
        {
            // Arrange
            // MatchValues returns strings, so the warning branch is now stubbable: the old
            // MatchCollection return type had no accessible constructor, which left Throws()
            // as the only outcome a mock of this seam could express.
            var mockRegex = new Mock<IRegexWrapper>();
            mockRegex.Setup(r => r.MatchValues(It.IsAny<string>()))
                     .Returns(new[] { "%FOO%", "%BAR%" });

            var original = ProcessHelper.EnvVarRegex;
            ProcessHelper.EnvVarRegex = mockRegex.Object;

            try
            {
                // Act
                ProcessHelper.ExpandAndAudit(new List<EnvironmentVariable>(), "anything", _mockLogger.Object);

                // Assert
                _mockLogger.Verify(l => l.Warn(
                    It.Is<string>(msg => msg == "Unexpanded environment variable %FOO% in Arguments"),
                    It.IsAny<Exception>()),
                    Times.Once);

                _mockLogger.Verify(l => l.Warn(
                    It.Is<string>(msg => msg == "Unexpanded environment variable %BAR% in Arguments"),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                // Restore original
                ProcessHelper.EnvVarRegex = original;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void ExpandAndAudit_HandlesNullOrEmptyContextPrefix(string? prefix)
        {
            // Arrange
            var emptyVars = new List<EnvironmentVariable>();
            string rawArgs = "cmd %VAR%";

            // Act
            ProcessHelper.ExpandAndAudit(emptyVars, rawArgs, _mockLogger.Object, prefix!);

            // Assert
            // Verify that a null, empty, or whitespace prefix does not leave stray brackets
            // like "[] Arguments" or "null Arguments" inside the audited error context message string.
            _mockLogger.Verify(l => l.Warn(
                It.Is<string>(msg => msg == "Unexpanded environment variable %VAR% in Arguments"),
                It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void ExpandAndAudit_ArgumentsReferencingCustomVariable_AreExpanded()
        {
            // Arrange
            var vars = new List<EnvironmentVariable> { new EnvironmentVariable { Name = "APP_HOME", Value = @"C:\App" } };
            string args = @"--config %APP_HOME%\config.json";

            // Act
            var result = ProcessHelper.ExpandAndAudit(vars, args, _mockLogger.Object, "Test");

            // Assert
            Assert.Equal(@"--config C:\App\config.json", result.expandedArgs);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Arguments")), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void ExpandAndAudit_NullLogger_SkipsWarningsAndStillExpands()
        {
            // Arrange
            // ProcessLauncher documents the logger parameter as "can be null", so every
            // logger?. call site inside the audit must take its null path.
            var vars = new List<EnvironmentVariable> { new EnvironmentVariable { Name = "VAR", Value = "%MISSING%" } };
            string args = "run %UNKNOWN%";

            // Act
            var result = ProcessHelper.ExpandAndAudit(vars, args, null, "Prefix");

            // Assert
            // Both placeholders stay unexpanded, so the warning site is reached with no logger to write to
            Assert.Equal("%MISSING%", result.env["VAR"]);
            Assert.Equal("run %UNKNOWN%", result.expandedArgs);
        }

        [Fact]
        public void ExpandAndAudit_NullLoggerOnRegexTimeout_SwallowsTimeoutWithoutLogging()
        {
            // Arrange
            var mockRegex = new Mock<IRegexWrapper>();
            mockRegex.Setup(r => r.MatchValues(It.IsAny<string>()))
                     .Throws(new RegexMatchTimeoutException());

            // Swap the static wrapper for the mock
            var original = ProcessHelper.EnvVarRegex;
            ProcessHelper.EnvVarRegex = mockRegex.Object;

            try
            {
                // Act
                // The timeout arm reports through logger?.Error; with a null logger it must stay silent
                var result = ProcessHelper.ExpandAndAudit(
                    new List<EnvironmentVariable> { new EnvironmentVariable { Name = "TRIGGER", Value = "trigger" } },
                    "trigger", null);

                // Assert
                Assert.Equal("trigger", result.env["TRIGGER"]);
                Assert.Equal("trigger", result.expandedArgs);
            }
            finally
            {
                // Restore original
                ProcessHelper.EnvVarRegex = original;
            }
        }
    }
}
