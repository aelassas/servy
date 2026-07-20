using Servy.Infrastructure.Helpers;
using System;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Helpers
{
    public class DatabaseValidatorTests
    {
        [Fact]
        public void IsSqliteVersionSafe_CurrentEnvironment_ReturnsParseableVersion()
        {
            // Arrange & Act
            DatabaseValidator.IsSqliteVersionSafe(out string detectedVersion);

            // Assert
            // We do not assert whether the environment is safe or unsafe (which is environment-dependent).
            // We only assert that the method successfully extracted a version string that can be parsed,
            // proving the detection mechanism itself works without crashing.
            Assert.NotNull(detectedVersion);
            Assert.True(Version.TryParse(detectedVersion, out _), $"Detected version '{detectedVersion}' should be parseable.");
        }

        [Theory]
        // Branch 1: Valid and Safe (sqlVersion >= MinRequiredSqliteVersion)
        [InlineData("3.50.2", true)]
        [InlineData("3.50.4", true)]
        [InlineData("4.0.0", true)]  // Folded from old logic check to maintain future version variant coverage
        [InlineData("10.0.0", true)]

        // Branch 2: Valid but Unsafe (sqlVersion < MinRequiredSqliteVersion)
        [InlineData("3.50.1", false)]
        [InlineData("1.0.0", false)]
        [InlineData("0.0.0", false)]

        // Branch 3: Invalid/Unparseable (Version.TryParse returns false)
        [InlineData("not-a-version", false)]
        [InlineData("invalid", false)]      // Retained value map from redundant check block
        [InlineData("v3.50.2", false)] // Version.TryParse fails on leading characters
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ValidateVersion_CoverageTest(string inputVersion, bool expectedResult)
        {
            // Arrange - Handled by xUnit data attributes

            // Act
            bool actualResult = DatabaseValidator.ValidateVersion(inputVersion);

            // Assert
            Assert.Equal(expectedResult, actualResult);
        }
    }
}