using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Core.RegexWrapper;
using Servy.Service.Helpers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace Servy.Service.UnitTests.Helpers
{
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
        public void LogUnexpandedPlaceholders_HandlesEmptyInputGracefully()
        {
            // Act - should not throw or log
            ProcessHelper.ExpandAndAudit(new List<EnvironmentVariable>(), "", _mockLogger.Object);

            // Assert
            _mockLogger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void LogUnexpandedPlaceholders_HandlesRegexTimeout_LogsError()
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
        public void ExpandAndAudit_HandlesNullOrEmptyContextPrefix(string prefix)
        {
            // Act
            ProcessHelper.ExpandAndAudit(new List<EnvironmentVariable>(), "cmd %VAR%", _mockLogger.Object, prefix);

            // Assert
            // Verify it handles the prefix logic without throwing
            _mockLogger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);
        }
    }
}