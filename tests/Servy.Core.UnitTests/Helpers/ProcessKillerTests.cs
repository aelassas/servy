using Servy.Core.Helpers;
using Servy.Testing;
using System.Diagnostics;

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
        public void KillProcessTreeAndParents_InvalidInput_ReturnsFalse(string? name)
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents(name!);

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
        [InlineData("app.exe", "app")]
        [InlineData("APP.EXE", "APP")]
        [InlineData("App.Exe", "App")]
        [InlineData("app", "app")]
        public void StripExe_ShouldNormalizeExtensionsCaseInsensitively(string input, string expected)
        {
            // Arrange & Act
            // Invoke the private 'StripExe' static method directly using the TestReflection engine. 
            // This bypasses live process-table dependencies and targets the text manipulation algorithm directly.
            string? actual = TestReflection.InvokeNonPublicStatic(typeof(ProcessKiller), "StripExe", input) as string;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void KillProcessesUsingFile_InvalidInput_ReturnsExpected(string? path)
            => Assert.True(_processKiller.KillProcessesUsingFile(path!));

        [Fact]
        public void KillProcessesUsingFile_MissingFile_ReturnsTrue()
        {
            // Arrange
            string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

            // Act
            var result = _processKiller.KillProcessesUsingFile(fakePath);

            // Assert
            // The method returns true and logs an error if the target file is missing
            Assert.True(result);
        }

        #endregion

    }
}