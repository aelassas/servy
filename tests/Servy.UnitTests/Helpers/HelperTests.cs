using Servy.Core.Helpers;
using System.IO;
using Xunit;

namespace Servy.UnitTests.Helpers
{
    public class HelperTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("C:\\ValidPath", true)]
        [InlineData("C:/ValidPath", true)]
        [InlineData("..\\Invalid", false)]
        [InlineData("Invalid|Path", false)]
        [InlineData("Relative\\Path", false)]
        public void IsValidPath_ReturnsExpectedResult(string path, bool expected)
        {
            var result = Helper.IsValidPath(path);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CreateParentDirectory_ReturnsFalse_ForNullOrWhitespace()
        {
            Assert.False(Helper.CreateParentDirectory(null));
            Assert.False(Helper.CreateParentDirectory(""));
            Assert.False(Helper.CreateParentDirectory("  "));
        }

        [Fact]
        public void CreateParentDirectory_CreatesDirectory_IfNotExists()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ServyTest", "SubDir");
            var filePath = Path.Combine(tempDir, "file.txt");

            // Ensure cleanup
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);

            var result = Helper.CreateParentDirectory(filePath);

            Assert.True(result);
            Assert.True(Directory.Exists(tempDir));

            // Cleanup
            Directory.Delete(Path.Combine(Path.GetTempPath(), "ServyTest"), true);
        }

        [Theory]
        [InlineData(null, "\"\"")]
        [InlineData("", "\"\"")]
        [InlineData("test", "\"test\"")]
        [InlineData("\"test\"", "\"test\"")]
        [InlineData("\"test\\", "\"test\"")]
        [InlineData("te\"st", "\"te\"\"st\"")]
        public void Quote_ReturnsExpected(string input, string expected)
        {
            var result = Helper.Quote(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("v1.2.3", 1.2)]
        [InlineData("1.2.3", 1.2)]
        [InlineData("V3.4.5", 3.4)]
        [InlineData("2.0", 2.0)]
        [InlineData("v1", 0)]
        [InlineData("invalid", 0)]
        public void ParseVersion_ReturnsExpectedDouble(string version, double expected)
        {
            var result = Helper.ParseVersion(version);
            Assert.Equal(expected, result);
        }
    }
}
