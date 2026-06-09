using Servy.Core.Helpers;
using Servy.Core.Resources;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class HelperTests : IDisposable
    {
        private readonly string _testRoot;

        public HelperTests()
        {
            // Establish an isolated scratch space inside the user profile/temp directory
            _testRoot = Path.Combine(Path.GetTempPath(), "Servy_PathTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, true);
                }
            }
            catch
            {
                // Prevent teardown exceptions from hiding test results
            }
        }

        // Tests for IsValidPath
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("..\\somepath", false)]            // Explicit directory traversal at start
        [InlineData("C:\\valid\\path.txt", true)]      // Valid absolute path (Windows style)
        [InlineData("C:/valid/path.txt", true)]        // Valid absolute path (forward slash)
        [InlineData("relative\\path", false)]          // Relative path (not rooted)
        [InlineData("C:\\invalid|path", false)]        // Invalid character '|'
        [InlineData("C:\\valid\\..\\path", false)]     // Contains traversal segment ".."
        [InlineData("C:\\", true)]                     // Root path
        [InlineData("C:\\my..folder\\path", true)]     // Legitimate ".." inside a folder name
        [InlineData("C:\\folder\\file..txt", true)]    // Legitimate ".." inside a file name
        [InlineData("C:\\valid\\path\\..", false)]     // Traversal segment at the very end

        // COVERS: Filename-invalid characters in the file name segment
        [InlineData("C:\\logs\\app<bad>.log", false)]  // Less-than '<' in filename
        [InlineData("C:\\logs\\app>bad>.log", false)]  // Greater-than '>' in filename
        [InlineData("C:\\logs\\app*.log", false)]      // Wildcard '*' in filename
        [InlineData("C:\\logs\\app?.log", false)]      // Wildcard '?' in filename
        [InlineData("C:\\logs\\app\"bad\".log", false)] // Quote '"' in filename

        // COVERS: Filename-invalid characters in intermediate directory segments
        [InlineData("C:\\bad<dir>\\app.log", false)]   // Less-than '<' in directory segment
        [InlineData("C:\\bad|dir\\app.log", false)]    // Pipe '|' in directory segment
        [InlineData("C:\\bad?dir\\app.log", false)]    // Wildcard '?' in directory segment

        // COVERS: Misplaced colons (Win32 streams syntax validation)
        [InlineData("C:\\logs\\file:name.log", false)] // Colon inside a sub-directory or file segment
        [InlineData("C:relative\\path.log", false)]   // Drive-relative path (fails absolute constraint)
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

        [Theory]
        [InlineData(@"C:\Windows", true)]             // Standard drive-rooted absolute path
        [InlineData(@"D:\Data\config.xml", true)]     // Alternate drive-rooted
        [InlineData(@"\\Server\Share\Path", true)]    // UNC absolute path
        [InlineData(@"C:/Windows", true)]             // Forward-slash separator (normalized by .NET)
        [InlineData(@"\Windows\System32", false)]     // Rooted but relative to current drive
        [InlineData(@"\Path", false)]                 // Rooted but relative to current drive
        [InlineData(@"Relative\Path", false)]         // Fully relative
        [InlineData(@"..\Parent", false)]             // Parent relative
        [InlineData(@"\Servy\logs\app.log", false)]   // Was BUG: returned true
        [InlineData(@"/var/log/foo.log", false)]      // Was BUG: returned true
        [InlineData(@"C:", false)]                    // Rooted but relative to current directory on drive
        [InlineData("", false)]                       // Empty
        [InlineData(null, false)]                     // Null
        public void IsAbsolute_ShouldCorrectlyIdentifyPathTypes(string input, bool expected)
        {
            // Act
            bool result = Helper.IsAbsolute(input);

            // Assert
            Assert.Equal(expected, result);
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
        [InlineData("abc", "abc")]                       // Simple text, nothing to escape
        [InlineData(@"C:\Path", @"C:\Path")]             // Backslashes not before quotes - unchanged
        [InlineData(@"C:\Path\""File", @"C:\Path\\""File")] // Backslash immediately before quote - doubled
        [InlineData(@"NoQuotesHere\", @"NoQuotesHere\")] // Trailing backslash - unchanged
        [InlineData(@"\""", @"\\""")]                    // Single backslash + quote - doubled before quote
        [InlineData(@"\\\""", @"\\\\\\""")]              // Multiple backslashes before quote
        [InlineData(@"Mix\ed\\\""Case", @"Mix\ed\\\\\\""Case")] // Mixed case: normal + before quote
        [InlineData("abc\0def", @"abc\0def")]            // Contains null char -> replaced with literal "\0"
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
        [InlineData("v1.10", "1.10")]
        // --- NEW TEST CASES ---
        [InlineData("v1.2.3-rc.1", "1.2.3")]   // SemVer pre-release suffix
        [InlineData("v1.2.3+build.42", "1.2.3")] // SemVer build metadata
        [InlineData("8.3-stable", "8.3")]      // Common non-standard suffix
        [InlineData("v2026.5.25", "2026.5.25")] // Date-based tags
        [InlineData("2026.05.25", "2026.5.25")] // Leading zeros in segments
                                                // --- EDGE CASES ---
        [InlineData("1.2.3.4", "1.2.3.4")]     // 4-part versioning
        [InlineData("", null)]                 // New null contract
        [InlineData("   ", null)]              // Whitespace handling
        [InlineData("invalid", null)]          // New null contract
        [InlineData("1.x.0", null)]            // Invalid structure
        public void ParseVersion_ReturnsExpectedVersion(string version, string expectedVersionString)
        {
            // Arrange
            var expected = expectedVersionString != null ? Version.Parse(expectedVersionString) : null;

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
        [InlineData("  MyService")] // Both
        public void IsServiceNameValid_UntrimmedLeadingWhitespace_ReturnsTrimError(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_ServiceNameContainsLeadingWhitespace, error);
        }

        /// <summary>
        /// Covers Branch 2: serviceName != serviceName.Trim()
        /// </summary>
        [Theory]
        [InlineData("MyService ")] // Trailing space
        [InlineData("MyService  ")] // Both
        public void IsServiceNameValid_UntrimmedTrailingWhitespace_ReturnsTrimError(string input)
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

        /// <summary>
        /// Verifies that service names utilizing leading or trailing dots are explicitly rejected 
        /// to prevent filesystem and registry volatility.
        /// </summary>
        [Theory]
        [InlineData(".CON")]
        [InlineData(".CON.txt")]
        [InlineData("..PRN")]
        [InlineData(".MyService")]
        [InlineData("MyService.")]
        [InlineData(".")]
        public void IsServiceNameValid_LeadingOrTrailingDots_ReturnsFailure(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_InvalidServiceName, error);
        }

        /// <summary>
        /// Verifies that the validation scanner successfully evaluates every dot-separated segment,
        /// catching reserved names regardless of their location or extension layout in the string.
        /// </summary>
        [Theory]
        [InlineData("CON")]
        [InlineData("prn")]
        [InlineData("service.LPT1")]
        [InlineData("AUX.manager")]
        [InlineData("com3.internal.service")]
        public void IsServiceNameValid_ReservedDeviceNamesInSegments_ReturnsFailure(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_InvalidServiceName, error);
        }

        /// <summary>
        /// Edge-case safety check verifying that inputs consisting purely of multiple dots 
        /// are handled cleanly and fail closed.
        /// </summary>
        [Theory]
        [InlineData("...")]
        [InlineData(".....")]
        public void IsServiceNameValid_PureDotStrings_ReturnsFailure(string input)
        {
            // Act
            var (isValid, error) = Helper.IsServiceNameValid(input);

            // Assert
            Assert.False(isValid);
            Assert.Equal(Strings.Msg_InvalidServiceName, error);
        }

        /// <summary>
        /// Ensures valid, standard multi-segment service names that simply contain a reserved substring 
        /// embedded inside a longer segment are allowed (e.g. "CON" inside "Controller").
        /// </summary>
        [Theory]
        [InlineData("MyController.Service")]
        [InlineData("NullifierService")]
        [InlineData("Lpt99.Service")]
        public void IsServiceNameValid_EmbeddedSubstringMatch_ReturnsSuccess(string input)
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
                await Helper.WriteFileAtomicAsync(targetPath, async (Stream stream, CancellationToken cancellationToken) =>
                {
                    byte[] data = Encoding.UTF8.GetBytes("atomic-async-test-48");
                    await stream.WriteAsync(data, 0, data.Length, cancellationToken);
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
                await Helper.WriteFileAtomicAsync(targetPath, async (Stream stream, CancellationToken cancellationToken) =>
                {
                    byte[] data = Encoding.UTF8.GetBytes("nesting-test");
                    await stream.WriteAsync(data, 0, data.Length, cancellationToken);
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

        #region Native Win32 Junction Helpers

        // These minimal P/Invoke mechanisms ensure the unit tests run reliably in 
        // headless test runners without executing high-overhead "cmd.exe /c mklink" shells.

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

        #endregion

        #region HasAncestorReparsePoint tests

        [Fact]
        public void HasAncestorReparsePoint_WhenPathDoesNotExist_ReturnsFalse()
        {
            // Arrange: Generate a canonical path that is guaranteed not to exist on disk
            string nonExistentPath = Path.Combine(_testRoot, "SubFolder1", "SubFolder2", "config.json");

            // Act
            bool result = Helper.HasAncestorReparsePoint(nonExistentPath);

            // Assert
            // Branch Covered: The loop safely walks upward past non-existent directories 
            // until reaching the root, verifying no ReparsePoint exists anywhere in the chain.
            Assert.False(result);
        }

        [Fact]
        public void HasAncestorReparsePoint_WhenPathDoesNotExistButAncestorIsJunction_ReturnsTrue()
        {
            const int maxRetries = 5;
            int attempt = 0;

            while (true)
            {
                attempt++;

                // Isolate each attempt's file paths to avoid cross-attempt lock collisions
                string realDir = Path.Combine(_testRoot, $"RealDirectoryMissingLeaf_att{attempt}");
                string junctionTarget = Path.Combine(_testRoot, $"JunctionDirMissingLeaf_att{attempt}");

                try
                {
                    // Arrange
                    Directory.CreateDirectory(realDir);
                    Testing.Helper.CreateJunction(junctionTarget, realDir);

                    // The junction exists, but the target file and its immediate parent folder DO NOT exist
                    string targetFilePath = Path.Combine(junctionTarget, "NotYet", "config.json");

                    // Act
                    bool result = Helper.HasAncestorReparsePoint(targetFilePath);

                    // Assert
                    // Branch Covered: Walks past the non-existent 'NotYet' folder and successfully 
                    // detects the junction at the existing ancestor level.
                    Assert.True(result);

                    // If we reach this line, the test passed successfully. Break out of the retry loop.
                    break;
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    // Fall-through logic: swallow the transient error and allow the loop to try again.
                    // We can insert a micro-pause to give the Windows Object Manager time to release handles.
                    Thread.Sleep(50);
                }
                finally
                {
                    // Clean up the handles allocated during this distinct attempt run
                    try { DeleteDirectoryLink(junctionTarget); } catch { /* ignore */ }
                    try
                    {
                        if (Directory.Exists(realDir))
                        {
                            Directory.Delete(realDir, true);
                        }
                    }
                    catch { /* fail silent in finally to protect test runner teardown */ }
                }
            }
        }

        [Fact]
        public void HasAncestorReparsePoint_WhenStandardLocalPath_ReturnsFalse()
        {
            // Arrange: Construct a normal directory chain on disk
            string deepDir = Path.Combine(_testRoot, "Level1", "Level2", "Level3");
            Directory.CreateDirectory(deepDir);
            string targetFilePath = Path.Combine(deepDir, "service.xml");

            // Act
            bool result = Helper.HasAncestorReparsePoint(targetFilePath);

            // Assert
            // Branch Covered: Inside the loop, both LinkTarget and ReparsePoint flags are empty.
            // It walks all the way up to the root safely.
            Assert.False(result);
        }

        /// <summary>
        /// Safely removes a directory symbolic link or junction point without disturbing the target directory's contents.
        /// </summary>
        /// <param name="linkPath">The path of the directory link to remove.</param>
        private static void DeleteDirectoryLink(string linkPath)
        {
            if (!Directory.Exists(linkPath)) return;

            try
            {
                var attributes = File.GetAttributes(linkPath);

                // Check if the folder is actually a reparse point. 
                // If the test setup failed and accidentally created a standard directory,
                // we must use recursive deletion to clear it safely for the next retry loop.
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    // In modern .NET, passing recursive: false to Delete() on a reparse point
                    // safely unlinks it without deleting anything inside the targeted directory.
                    Directory.Delete(linkPath, recursive: false);
                }
                else
                {
                    Directory.Delete(linkPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanly unlink directory reparse point at '{linkPath}': {ex.Message}");
            }
        }

        [Fact]
        public void HasAncestorReparsePoint_WhenImmediateParentIsJunctionOrSymlink_ReturnsTrue()
        {
            string realDir = Path.Combine(_testRoot, "RealDirectory");
            string junctionTarget = Path.Combine(_testRoot, "JunctionDir");
            Directory.CreateDirectory(realDir);

            int maxRetries = 5;
            bool created = false;

            // Retry loop for the unstable NTFS link creation
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    Testing.Helper.CreateJunction(junctionTarget, realDir);
                    created = true;
                    break;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Thread.Sleep(500); // Backoff before retrying
                }
            }

            Assert.True(created, "Failed to create junction after multiple attempts.");

            try
            {
                string targetFilePath = Path.Combine(junctionTarget, "config.json");
                bool result = Helper.HasAncestorReparsePoint(targetFilePath);
                Assert.True(result);
            }
            finally
            {
                // Cleanup with retry
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        DeleteDirectoryLink(junctionTarget);
                        if (Directory.Exists(realDir)) Directory.Delete(realDir, true);
                        break;
                    }
                    catch (IOException) when (i < maxRetries - 1)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }

        [Fact]
        public void HasAncestorReparsePoint_WhenDeepAncestorIsJunction_ReturnsTrue()
        {
            // Arrange
            string realDir = Path.Combine(_testRoot, "ActualData");
            string junctionDir = Path.Combine(_testRoot, "HiddenJunction");
            string deepNestedPath = Path.Combine(junctionDir, "App_Data", "Logs");

            Directory.CreateDirectory(realDir);

            int maxRetries = 5;
            bool setupSuccess = false;

            // Retry loop for junction creation and nested structure setup
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    Testing.Helper.CreateJunction(junctionDir, realDir);
                    // Create nested structure inside the junctioned area
                    Directory.CreateDirectory(deepNestedPath);
                    setupSuccess = true;
                    break;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // Clean up potentially partially created structure before retry
                    try { if (Directory.Exists(junctionDir)) DeleteDirectoryLink(junctionDir); } catch { }
                    Thread.Sleep(500);
                }
            }

            Assert.True(setupSuccess, "Failed to create junction and nested directory structure after retries.");

            try
            {
                // Act
                string targetFilePath = Path.Combine(deepNestedPath, "output.log");
                bool result = Helper.HasAncestorReparsePoint(targetFilePath);

                // Assert
                Assert.True(result);
            }
            finally
            {
                // Teardown with retry logic
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        // Must delete nested content first for successful junction deletion
                        if (Directory.Exists(junctionDir))
                        {
                            // Using Directory.Delete(path, true) on a junction usually deletes the contents
                            // of the target folder. We use the specialized DeleteDirectoryLink instead.
                            DeleteDirectoryLink(junctionDir);
                        }
                        if (Directory.Exists(realDir)) Directory.Delete(realDir, true);
                        break;
                    }
                    catch (IOException) when (i < maxRetries - 1)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }

        #endregion
    }
}