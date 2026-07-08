using Moq;
using Servy.Core.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Unit tests for the ProcessKiller utility (input validation and not-found logic).
    /// Integration tests that spawn real processes live in ProcessKillerIntegrationTests.
    /// </summary>
    public class ProcessKillerTests
    {
        private readonly IProcessKiller _processKiller;

        public ProcessKillerTests()
        {
            _processKiller = new ProcessKiller();
        }

        #region Unit Tests (Validation & Logic)

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void KillProcessTreeAndParents_InvalidInput_ReturnsFalse(string name)
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents(name);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void KillProcessTreeAndParents_ProcessNotFound_ReturnsTrue()
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents("NonExistentProcess_Unique_999");

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("app.exe")]
        [InlineData("APP.EXE")]
        [InlineData("App.Exe")]
        public void KillProcessTreeAndParents_ShouldHandleExeExtensionCaseInsensitively(string input)
        {
            // Act
            // This verifies the string manipulation logic doesn't throw when suffix is present
            var result = _processKiller.KillProcessTreeAndParents(input);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void KillProcessesUsingFile_InvalidInput_ReturnsExpected(string path)
            => Assert.True(_processKiller.KillProcessesUsingFile(path));

        [Fact]
        public void KillProcessesUsingFile_MissingFile_ReturnsTrue()
        {
            // Arrange
            string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

            var mockProcessHelper = new Mock<IProcessHelper>();

            // Act
            var result = _processKiller.KillProcessesUsingFile(fakePath);

            // Assert
            // The method returns true and logs an error if the target file is missing
            Assert.True(result);
        }

        #endregion

    }
}