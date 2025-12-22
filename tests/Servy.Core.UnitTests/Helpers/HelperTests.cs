using Servy.Core.Helpers;
using System;
using System.IO;
using System.Reflection;
using Xunit;

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
        [InlineData(null, "\"\"")]                  // null input
        [InlineData("", "\"\"")]                    // empty string
        [InlineData("   ", "\"\"")]                 // whitespace only
        [InlineData("abc", "\"abc\"")]              // simple string, no escaping
        [InlineData(@"ab\""c", @"""ab\\\""c""")]    // simple string, escaping
        [InlineData(@"C:\Path", @"""C:\Path""")]    // normal backslashes
        [InlineData(@"C:\Path\", @"""C:\Path\\""")] // trailing backslash (doubles before closing quote)
        [InlineData(@"C:\Path""File", @"""C:\Path\""File""")] // quote in the middle
        [InlineData(@"\\""", @"""\\\\\""""")] // backslash directly before a quote
        [InlineData(@"Mixed\Path""End\", @"""Mixed\Path\""End\\""")] // mix of both
        [InlineData("abc\0def", @"""abc\0def""")] // contains a null character -> replaced with literal "\0"
        public void Quote_ShouldEscapeCorrectly(string input, string expected)
        {
            // Act
            var result = Helper.Quote(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, "")]                  // null input
        [InlineData("", "")]                    // empty string
        [InlineData("   ", "")]                 // whitespace only
        [InlineData("abc", "abc")]              // simple string, no escaping
        [InlineData(@"ab\""c", @"ab\\\""c")]    // simple string, escaping
        [InlineData(@"C:\Path", @"C:\Path")]    // normal backslashes
        [InlineData(@"C:\Path\", @"C:\Path\\")] // trailing backslash (doubles before closing quote)
        [InlineData(@"C:\Path""File", @"C:\Path\""File")] // quote in the middle
        [InlineData(@"\\""", @"\\\\\""")] // backslash directly before a quote
        [InlineData(@"Mixed\Path""End\", @"Mixed\Path\""End\\")] // mix of both
        [InlineData("abc\0def", @"abc\0def")] // contains a null character -> replaced with literal "\0"
        public void EscapeArgs_ShouldEscapeCorrectly(string input, string expected)
        {
            // Act
            var result = Helper.EscapeArgs(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, "")]                           // Null input
        [InlineData("", "")]                             // Empty string
        [InlineData("   ", "")]                          // Whitespace only
        [InlineData("abc", "abc")]                       // Simple text, nothing to escape
        [InlineData(@"C:\Path", @"C:\Path")]             // Backslashes not before quotes - unchanged
        [InlineData(@"C:\Path\""File", @"C:\Path\\""File")] // Backslash immediately before quote - doubled
        [InlineData(@"NoQuotesHere\", @"NoQuotesHere\")] // Trailing backslash - unchanged
        [InlineData(@"\""", @"\\""")]                    // Single backslash + quote - doubled before quote
        [InlineData(@"\\\""", @"\\\\\\""")]              // Multiple backslashes before quote
        [InlineData(@"Mix\ed\\\""Case", @"Mix\ed\\\\\\""Case")] // Mixed case: normal + before quote
        [InlineData("abc\0def", @"abc\0def")]           // Contains null char -> replaced with literal "\0"
        public void EscapeBackslashes_ShouldEscapeCorrectly(string input, string expected)
        {
            // Act
            var result = Helper.EscapeBackslashes(input);

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

        [Theory]
        [InlineData(null, "Unknown")]
        [InlineData("", "Unknown")]
        [InlineData("   ", "Unknown")]
        [InlineData(".NETFramework,Version=v4.8", ".NET Framework 4.8")]
        [InlineData(".NETFramework,Version=unknown", ".NET Framework unknown")]
        [InlineData("SomeOtherFramework", "SomeOtherFramework")]
        public void ParseFrameworkName_CoversAllBranches(string input, string expected)
        {
            string result = Helper.ParseFrameworkName(input);
            Assert.Equal(expected, result);
        }

    }

}