using Servy.CLI.Models;

namespace Servy.CLI.UnitTests.Models
{
    public class CommandResultTests
    {
        [Fact]
        public void Fail_ExitCodeZero_IsCoercedToOne()
        {
            // Arrange & Act
            var result = CommandResult.Fail("failed", 0);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public void Fail_NonZeroExitCode_IsPreserved()
        {
            // Arrange & Act
            var result = CommandResult.Fail("failed", 5);

            // Assert
            Assert.Equal(5, result.ExitCode);
        }
    }
}
