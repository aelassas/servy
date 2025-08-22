using Servy.Core.Helpers;

namespace Servy.Core.UnitTests.Helpers
{
    public class HelperTests
    {
        // Tests for IsValidPath
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("..\\somepath", false)]           // directory traversal
        [InlineData("C:\\valid\\path.txt", true)]     // valid absolute path (Windows style)
        [InlineData("C:/valid/path.txt", true)]       // valid absolute path (slash)
        [InlineData("relative\\path", false)]         // relative path (not rooted)
        [InlineData("C:\\invalid|path", false)]       // invalid char '|'
        [InlineData("C:\\valid\\..\\path", false)]    // contains ..
        [InlineData("/usr/bin/bash", true)]           // absolute path (Unix style)
        [InlineData("C:\\", true)]                    // root path
        public void IsValidPath_VariousInputs_ReturnsExpected(string path, bool expected)
        {
            var result = Helper.IsValidPath(path);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidPath_TooLongPath_ThrowsAndReturnsFalse()
        {
            // Arrange
            var longFolder = new string('a', short.MaxValue);
            var path = "C:\\" + longFolder;

            // Act
            var result = Helper.IsValidPath(path);

            // Assert
            Assert.False(result);
        }

        // Tests for CreateParentDirectory
        [Fact]
        public void CreateParentDirectory_NullOrWhitespace_ReturnsFalse()
        {
            Assert.False(Helper.CreateParentDirectory(null));
            Assert.False(Helper.CreateParentDirectory(""));
            Assert.False(Helper.CreateParentDirectory("    "));
            Assert.False(Helper.CreateParentDirectory("C:\\"));
        }

        [Fact]
        public void CreateParentDirectory_PathHasNoParentDirectory_ReturnsFalse()
        {
            Assert.False(Helper.CreateParentDirectory("C:\\"));
        }

        [Theory]
        [InlineData("file.txt")]               // no directory part, returns false
        [InlineData("C:\\file.txt")]           // directory is "C:\"
        [InlineData("C:\\folder\\file.txt")]   // directory is "C:\folder"
        [InlineData("C:/folder/file.txt")]     // with forward slashes
        public void CreateParentDirectory_DirectoryExistsOrCreated_ReturnsTrue(string filePath)
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var testFilePath = Path.Combine(tempDir, filePath);

            try
            {
                // Act
                var result = Helper.CreateParentDirectory(testFilePath);

                // Assert
                Assert.True(result);

                var parentDir = Path.GetDirectoryName(testFilePath);
                Assert.True(Directory.Exists(parentDir));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void CreateParentDirectory_InvalidPath_ReturnsFalse()
        {
            // Give an invalid path that will throw
            var invalidPath = "?:\\invalid\\path\\file.txt";
            var result = Helper.CreateParentDirectory(invalidPath);
            Assert.False(result);
        }

        [Theory]
        [InlineData(null, "\"\"")]
        [InlineData("", "\"\"")]
        [InlineData("abc", "\"abc\"")]
        [InlineData("\"abc\"", "\"abc\"")]
        [InlineData("\"abc\\\"", "\"abc\"")]
        [InlineData("abc\\", "\"abc\"")]
        [InlineData("\"abc\\\\\"", "\"abc\"")]
        public void Quote_Input_ReturnsExpected(string input, string expected)
        {
            // Act
            var result = Helper.Quote(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("v1.2.3", 1.2)]
        [InlineData("1.2.3", 1.2)]
        [InlineData("V3.4.5", 3.4)]
        [InlineData("2.0", 2.0)]
        [InlineData("v1", 0)]
        [InlineData("invalid", 0)]
        [InlineData("1.x.0", 0)]
        public void ParseVersion_ReturnsExpectedDouble(string version, double expected)
        {
            var result = Helper.ParseVersion(version);
            Assert.Equal(expected, result);
        }
    }
}