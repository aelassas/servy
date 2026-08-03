using Servy.Core.Config;
using Servy.Infrastructure.Helpers;

namespace Servy.Infrastructure.UnitTests.Helpers
{
    public class DatabaseValidatorTests
    {
        [Fact]
        public void IsSqliteVersionSafe_CurrentEnvironment_ReturnsParseableVersion()
        {
            // Arrange & Act
            bool isSafe = DatabaseValidator.IsSqliteVersionSafe(out string? detectedVersion);

            // Assert
            // We do not assert whether the environment is safe or unsafe (which is environment-dependent).
            // We only assert that the method successfully extracted a version string that can be parsed,
            // proving the detection mechanism itself works without crashing.
            Assert.NotNull(detectedVersion);
            Assert.True(Version.TryParse(detectedVersion, out _), $"Detected version '{detectedVersion}' should be parseable.");

            // Verify that the boolean verdict strictly agrees with direct delegation to ValidateVersion
            Assert.Equal(DatabaseValidator.ValidateVersion(detectedVersion), isSafe);
        }

        public static TheoryData<string?, bool> VersionCases()
        {
            var min = AppConfig.MinRequiredSqliteVersion;
            var justBelow = new Version(min.Major, min.Minor, Math.Max(0, min.Build - 1));
            var newerPatch = new Version(min.Major, min.Minor, min.Build + 2);

            return new TheoryData<string?, bool>
            {
                // Branch 1: Valid and Safe (sqlVersion >= MinRequiredSqliteVersion)
                { min.ToString(), true },        // Exact boundary: comparison must be >=
                { newerPatch.ToString(), true }, // Newer patch version
                { "4.0.0", true },               // Major-version bump must still compare as newer
                { "10.0.0", true },              // Multi-digit major must not compare lexically

                // Branch 2: Valid but Unsafe (sqlVersion < MinRequiredSqliteVersion)
                { justBelow.ToString(), false }, // One patch level below must be rejected
                { "1.0.0", false },
                { "0.0.0", false },

                // Branch 3: Invalid/Unparseable (Version.TryParse returns false)
                { "not-a-version", false },
                { "invalid", false },
                { "v3.50.2", false },            // Version.TryParse fails on leading characters
                { "", false },
                { null, false }
            };
        }

        [Theory]
        [MemberData(nameof(VersionCases))]
        public void ValidateVersion_ParsesAndComparesAgainstMinimum(string? inputVersion, bool expectedSafe)
        {
            // Act
            bool actualResult = DatabaseValidator.ValidateVersion(inputVersion);

            // Assert
            Assert.Equal(expectedSafe, actualResult);
        }
    }
}