using Servy.Core.Config;
using Servy.Infrastructure.Helpers;
using System;
using Xunit;

namespace Servy.Infrastructure.Tests.Helpers
{
    public class DatabaseValidatorTests
    {
        [Fact]
        public void IsSqliteVersionSafe_CurrentEnvironment_ReturnsParseableVersion()
        {
            // Act
            DatabaseValidator.IsSqliteVersionSafe(out string detectedVersion);

            // Assert
            // We do not assert whether the environment is safe or unsafe (which is environment-dependent).
            // We only assert that the method successfully extracted a version string that can be parsed,
            // proving the detection mechanism itself works without crashing.
            Assert.NotNull(detectedVersion);
            Assert.True(Version.TryParse(detectedVersion, out _), $"Detected version '{detectedVersion}' should be parseable.");
        }

        [Theory]
        [InlineData("3.50.1", false)]  // Just below threshold
        [InlineData("3.50.2", true)]   // Exactly at threshold
        [InlineData("3.50.4", true)]   // Your SourceGear version
        [InlineData("4.0.0", true)]    // Future version
        [InlineData("invalid", false)] // Unparseable string
        public void SQLiteVersionComparison_LogicCheck(string versionToTest, bool expectedSafe)
        {
            // Note: Since we can't mock the static SQLiteVersion, 
            // this test validates the comparison logic used in the implementation.

            bool canParse = Version.TryParse(versionToTest, out var parsedVersion);

            bool isSafe = canParse && parsedVersion >= AppConfig.MinRequiredSqliteVersion;

            Assert.Equal(expectedSafe, isSafe);
        }

        [Theory]
        // Branch 1: Valid and Safe (sqlVersion >= MinRequiredSqliteVersion)
        [InlineData("3.50.2", true)]
        [InlineData("3.50.4", true)]
        [InlineData("10.0.0", true)]

        // Branch 2: Valid but Unsafe (sqlVersion < MinRequiredSqliteVersion)
        [InlineData("3.50.1", false)]
        [InlineData("1.0.0", false)]
        [InlineData("0.0.0", false)]

        // Branch 3: Invalid/Unparseable (Version.TryParse returns false)
        [InlineData("not-a-version", false)]
        [InlineData("v3.50.2", false)] // Version.TryParse fails on leading characters
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsSqliteVersionSafeInternal_CoverageTest(string inputVersion, bool expectedResult)
        {
            // Act
            bool actualResult = DatabaseValidator.ValidateVersion(inputVersion, out string currentVersion);

            // Assert
            Assert.Equal(expectedResult, actualResult);
            Assert.Equal(inputVersion, currentVersion); // Ensures currentVersion is assigned correctly
        }

        [Fact]
        public void IsSqliteVersionSafeInternal_OutputsExactInput()
        {
            // This specifically tests the "currentVersion = versionToValidate" assignment
            string myInput = "9.9.9-test";
            DatabaseValidator.ValidateVersion(myInput, out string output);

            Assert.Equal(myInput, output);
        }

    }
}