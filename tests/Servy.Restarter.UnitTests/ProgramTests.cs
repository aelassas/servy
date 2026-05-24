using System;
using System.IO;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    // Forces sequential execution since Environment.ExitCode and ConfigurationManager are global static states
    [Collection("Servy.Restarter.UnitTests.ProgramTests")]
    public class ProgramTests : IDisposable
    {
        public ProgramTests()
        {
            // Reset global exit code before each test execution block
            Environment.ExitCode = 0;
        }

        #region Guard Conditions Branch Coverage

        [Fact]
        public void Main_MissingArguments_SetsExitCodeTo1AndExitsEarly()
        {
            // Arrange
            string[] args = new string[0]; // Triggers: if (args.Length == 0)

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Main_EmptyOrWhitespaceServiceName_SetsExitCodeTo1AndExitsEarly(string invalidName)
        {
            // Arrange
            string[] args = new string[] { invalidName }; // Triggers: if (string.IsNullOrWhiteSpace(serviceName))

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
        }

        #endregion

        #region Operational Pipeline & Validation Branches

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We pass an unmanaged service identifier string. Since the memory database is fresh and empty,
            // serviceRepository.GetByName(...) will return null, exercising the managed validation check.
            string[] args = new string[] { "UnmanagedNet48Service" };

            // Act
            Program.Main(args);

            // Assert
            // Program logs the validation failure and registers failure exit code 1
            Assert.Equal(1, Environment.ExitCode);
        }

        #endregion

        public void Dispose()
        {
            // Clean up temporary local workspace state file markers if generated
            try
            {
                if (File.Exists("test_restarter.key")) File.Delete("test_restarter.key");
                if (File.Exists("test_restarter.iv")) File.Delete("test_restarter.iv");
            }
            catch
            {
                // Suppress lock warnings on ephemeral files cleanup
            }
        }
    }
}