using Servy.Core.Helpers;
using Servy.Core.Resources;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
        [InlineData("..\\somepath", false)]           // Explicit directory traversal at start
        [InlineData("C:\\valid\\path.txt", true)]     // Valid absolute path (Windows style)
        [InlineData("C:/valid/path.txt", true)]       // Valid absolute path (forward slash)
        [InlineData("relative\\path", false)]         // Relative path (not rooted)
        [InlineData("C:\\invalid|path", false)]       // Invalid character '|'
        [InlineData("C:\\valid\\..\\path", false)]    // Contains traversal segment ".."
        [InlineData("C:\\", true)]                    // Root path
        [InlineData("C:\\my..folder\\path", true)]    // Legitimate ".." inside a folder name
        [InlineData("C:\\folder\\file..txt", true)]   // Legitimate ".." inside a file name
        [InlineData("C:\\valid\\path\\..", false)]    // Traversal segment at the very end
        public void IsValidPath_VariousInputs_ReturnsExpected(string path, bool expected)
        {
            // Act
            var result = Helper.IsValidPath(path);

            // Assert
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
        [InlineData("v1.2.3", "1.2.3")]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("V3.4.5", "3.4.5")]
        [InlineData("2.0", "2.0")]
        [InlineData("v1.10", "1.10")] // Proves 1.10 > 1.9 logic is fixed
        [InlineData("v1", "0.0")]     // Fallback case
        [InlineData("invalid", "0.0")]
        [InlineData("1.x.0", "0.0")]
        public void ParseVersion_ReturnsExpectedVersion(string version, string expectedVersionString)
        {
            // Arrange
            var expected = Version.Parse(expectedVersionString);

            // Act
            var result = Helper.ParseVersion(version);

            // Assert
            // System.Version implements Equals() and correctly compares Major, Minor, Build, etc.
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

        /// <summary>
        /// Covers Branch 1: string.IsNullOrWhiteSpace(serviceName)
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsServiceNameValid_NullOrWhitespace_ReturnsValidationError(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_ValidationError, error);
        }

        /// <summary>
        /// Covers Branch 2: serviceName != serviceName.Trim()
        /// </summary>
        [Theory]
        [InlineData(" MyService")] // Leading space
        [InlineData("MyService ")] // Trailing space
        [InlineData(" MyService ")] // Both
        public void IsServiceNameValid_UntrimmedWhitespace_ReturnsTrimError(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_ServiceNameContainsTrailingWhitespace, error);
        }

        /// <summary>
        /// Covers Branch 3a: serviceName.IndexOfAny(InvalidServiceChars) >= 0
        /// </summary>
        [Theory]
        [InlineData(@"My\Service")]
        [InlineData("My/Service")]
        [InlineData("My:Service")]
        [InlineData("My*Service")]
        [InlineData("My?Service")]
        [InlineData("My\"Service")]
        [InlineData("My<Service")]
        [InlineData("My>Service")]
        [InlineData("My|Service")]
        public void IsServiceNameValid_ForbiddenCharacters_ReturnsInvalidCharError(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_InvalidServiceName, error);
        }

        /// <summary>
        /// Covers Branch 3b: serviceName.Any(c => char.IsControl(c))
        /// </summary>
        [Theory]
        [InlineData("My\nService")] // Newline
        [InlineData("My\tService")] // Tab
        [InlineData("MyService\u0000")] // Null character
        public void IsServiceNameValid_ControlCharacters_ReturnsInvalidCharError(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_InvalidServiceName, error);
        }

        /// <summary>
        /// Covers the Success Path: No branches taken
        /// </summary>
        [Theory]
        [InlineData("MyService")]
        [InlineData("Servy-Agent")]
        [InlineData("Wexflow_Service")]
        [InlineData("Service.123")]
        public void IsServiceNameValid_ValidInput_ReturnsSuccess(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.True(isValid);
            Assert.Equal(string.Empty, error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizePath_NullOrWhiteSpace_ReturnsNull(string input)
        {
            // Act
            var result = Helper.NormalizePath(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void NormalizePath_RelativePath_ReturnsFullPath()
        {
            // Arrange
            var input = "logs";
            var expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "logs");

            // Act
            var result = Helper.NormalizePath(input);

            // Assert
            // We use Path.GetFullPath in the assertion to match OS-specific drive letters/formatting
            Assert.Equal(Path.GetFullPath(expected), result);
        }

        [Theory]
        [InlineData(@"C:\Windows\")]
        [InlineData(@"C:\Windows\\")]
        public void NormalizePath_TrailingSeparators_TrimsThem(string input)
        {
            // Arrange
            var expected = @"C:\Windows";

            // Act
            var result = Helper.NormalizePath(input);

            // Assert
            Assert.Equal(expected, result);
            Assert.False(result.EndsWith(Path.DirectorySeparatorChar.ToString()));
        }

        [Fact]
        public void NormalizePath_ParentDirectoryDots_ResolvesToAbsolute()
        {
            // Arrange
            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var input = Path.Combine(baseDir, "subdir", "..");

            // Act
            var result = Helper.NormalizePath(input);

            // Assert
            Assert.Equal(baseDir, result);
        }

        [Fact]
        public void NormalizePath_ForwardSlashes_ConvertsToBackslashesOnWindows()
        {
            // Arrange
            // Path.GetFullPath handles cross-platform slash normalization
            var input = "C:/Windows/System32/";
            var expected = @"C:\Windows\System32";

            // Act
            var result = Helper.NormalizePath(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #region WriteFileAtomic (Synchronous) Tests

        [Fact]
        public void WriteFileAtomic_Success_WritesFileAndCleansUpTemp()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string targetPath = Path.Combine(tempDir, "target.txt");
            string tempPath = targetPath + ".tmp"; // Logic uses .tmp suffix

            try
            {
                // Act: WriteFileAtomic ensures parent directory creation
                Helper.WriteFileAtomic(targetPath, (Stream stream) =>
                {
                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                    {
                        writer.Write("atomic-sync-test-48");
                        // fs.Flush is called internally by WriteFileAtomic after the action
                    }
                });

                // Assert
                Assert.True(File.Exists(targetPath));
                Assert.Equal("atomic-sync-test-48", File.ReadAllText(targetPath));
                Assert.False(File.Exists(tempPath), "Temporary file should be cleaned up by the finally block");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void WriteFileAtomic_OnException_CleansUpTempAndDoesNotCreateTarget()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string targetPath = Path.Combine(tempDir, "target.txt");
            string tempPath = targetPath + ".tmp";

            try
            {
                // Act & Assert
                Assert.Throws<InvalidOperationException>(() =>
                {
                    Helper.WriteFileAtomic(targetPath, (Stream stream) =>
                    {
                        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                        {
                            writer.Write("partial-data");
                            throw new InvalidOperationException("Simulated failure");
                        }
                    });
                });

                // CleanupTempFile is called in finally to ensure .tmp is removed
                Assert.False(File.Exists(targetPath), "Target should not exist if move was never reached.");
                Assert.False(File.Exists(tempPath), "Temp file should be cleaned up even on failure");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void WriteFileAtomic_OverwritesReadOnlyFile()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            string targetPath = Path.Combine(tempDir, "target.txt");

            try
            {
                File.WriteAllText(targetPath, "initial-content");
                // Set read-only to test PrepareDestinationForMove logic
                File.SetAttributes(targetPath, FileAttributes.ReadOnly);

                // Act
                // Act
                Helper.WriteFileAtomic(targetPath, (Stream stream) =>
                {
                    // The 4th parameter is 'leaveOpen'. Set it to true.
                    // We must also specify a buffer size (default is 1024).
                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                    {
                        writer.Write("new-content");
                    }
                    // StreamWriter is disposed here, but 'stream' remains open for Helper.WriteFileAtomic to Flush()
                });

                // Assert: Attributes should be normalized before move
                Assert.True(File.Exists(targetPath));
                Assert.Equal("new-content", File.ReadAllText(targetPath));

                FileAttributes attributes = File.GetAttributes(targetPath);
                Assert.False((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            }
            finally
            {
                if (File.Exists(targetPath)) File.SetAttributes(targetPath, FileAttributes.Normal);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region WriteFileAtomicAsync Tests

        [Fact]
        public async Task WriteFileAtomicAsync_Success_WritesFile()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string targetPath = Path.Combine(tempDir, "target.txt");

            try
            {
                // Act: Uses WriteFileAtomicCore internally
                await Helper.WriteFileAtomicAsync(targetPath, async (Stream stream) =>
                {
                    byte[] data = Encoding.UTF8.GetBytes("atomic-async-test-48");
                    await stream.WriteAsync(data, 0, data.Length);
                });

                // Assert
                Assert.True(File.Exists(targetPath));

                // .NET 4.8 workaround for lack of File.ReadAllTextAsync
                string result;
                using (StreamReader reader = new StreamReader(targetPath))
                {
                    result = await reader.ReadToEndAsync();
                }
                Assert.Equal("atomic-async-test-48", result);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task WriteFileAtomicAsync_CreatesNestedDirectories()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string nestedPath = Path.Combine(tempDir, "deeply", "nested", "path");
            string targetPath = Path.Combine(nestedPath, "test.log");

            try
            {
                // Act: Core logic calls Directory.CreateDirectory
                await Helper.WriteFileAtomicAsync(targetPath, async (Stream stream) =>
                {
                    byte[] data = Encoding.UTF8.GetBytes("nesting-test");
                    await stream.WriteAsync(data, 0, data.Length);
                });

                // Assert
                Assert.True(Directory.Exists(nestedPath));
                Assert.True(File.Exists(targetPath));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        #endregion

    }
}
