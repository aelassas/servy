using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Core.RegexWrapper;
using Servy.Service.Helpers;
using System.Text.RegularExpressions;

namespace Servy.Service.UnitTests.Helpers
{
    [CollectionDefinition("SequentialProcessHelperTests", DisableParallelization = true)]
    public class SequentialProcessHelperTests
    {
        // Enforces strict sequential isolation across the execution suite
    }

    [Collection("SequentialEnvTests")]
    public class ProcessHelperTests
    {
        private readonly Mock<IServyLogger> _mockLogger;

        public ProcessHelperTests()
        {
            _mockLogger = new Mock<IServyLogger>();
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
            _mockLogger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
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
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("%MISSING%")), It.IsAny<Exception>()), Times.Once);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("%UNKNOWN%")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ExpandAndAudit_HandlesEmptyInputGracefully()
        {
            // Act - should not throw or log
            ProcessHelper.ExpandAndAudit(new List<EnvironmentVariable>(), "", _mockLogger.Object);

            // Assert
            _mockLogger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void ExpandAndAudit_HandlesRegexTimeout_LogsError()
        {
            // Arrange
            var mockRegex = new Mock<IRegexWrapper>();
            mockRegex.Setup(r => r.Matches(It.IsAny<string>()))
                     .Throws(new RegexMatchTimeoutException());

            // Swap the static wrapper for the mock
            var original = ProcessHelper.EnvVarRegex;
            ProcessHelper.EnvVarRegex = mockRegex.Object;

            try
            {
                // Act
                // Pass a non-empty string to trigger the try block
                ProcessHelper.ExpandAndAudit(new List<EnvironmentVariable>(), "trigger", _mockLogger.Object);

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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
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
    }
}