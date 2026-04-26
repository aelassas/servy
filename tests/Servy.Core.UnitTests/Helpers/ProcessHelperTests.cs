using Servy.Core.Helpers;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ProcessHelperTests
    {
        private readonly ProcessHelper _processHelper;

        public ProcessHelperTests()
        {
            _processHelper = new ProcessHelper();
        }

        [Theory]
        [InlineData(0, "0%")]        // zero case
        [InlineData(0.03, "0%")]     // zero case
        [InlineData(1.0, "1.0%")]    // integer case
        [InlineData(1.04, "1.0%")]   // two decimals
        [InlineData(1.05, "1.1%")]   // two decimals
        [InlineData(1.06, "1.1%")]   // two decimals
        [InlineData(1.1, "1.1%")]    // one decimal -> force two decimals
        [InlineData(1.23, "1.2%")]   // two decimals
        [InlineData(1.34, "1.3%")]   // rounding down
        [InlineData(1.35, "1.4%")]   // rounding up
        [InlineData(1.36, "1.4%")]   // rounding up
        public void FormatCPUUsage_ReturnsExpected(double input, string expected)
        {
            var result = _processHelper.FormatCpuUsage(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(512, "512.0 B")]                // < KB
        [InlineData(2048, "2.0 KB")]                // exact KB
        [InlineData(3072, "3.0 KB")]                // KB range
        [InlineData(1048576, "1.0 MB")]             // exact MB
        [InlineData((1.5 * 1024 * 1024), "1.5 MB")] // MB range
        [InlineData(1073741824, "1.0 GB")]          // exact GB
        [InlineData((2.23 * 1024 * 1024 * 1024), "2.2 GB")] // GB range
        [InlineData((2.25 * 1024 * 1024 * 1024), "2.3 GB")] // GB range
        [InlineData(1099511627776, "1.0 TB")]       // exact TB
        [InlineData((3.75 * 1024 * 1024 * 1024 * 1024), "3.8 TB")] // TB range
        public void FormatRAMUsage_ReturnsExpected(long input, string expected)
        {
            var result = _processHelper.FormatRamUsage(input);
            Assert.Equal(expected, result);
        }

        // -------------------------
        // ResolvePath tests
        // -------------------------

        [Fact]
        public void ResolvePath_NullInput_ReturnsNull()
        {
            var result = _processHelper.ResolvePath(null);
            Assert.Null(result);
        }

        [Fact]
        public void ResolvePath_EmptyInput_ReturnsEmpty()
        {
            var result = _processHelper.ResolvePath(string.Empty);
            Assert.Null(result);
        }

        [Fact]
        public void ResolvePath_AbsolutePath_NoEnvVars_ReturnsNormalizedPath()
        {
            var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var input = tempDir + Path.DirectorySeparatorChar;

            var result = _processHelper.ResolvePath(input);

            Assert.Equal(Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar), result?.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void ResolvePath_AbsolutePath_WithEnvVar_ExpandsSuccessfully()
        {
            var input = "%TEMP%";

            var result = _processHelper.ResolvePath(input);

            Assert.True(Path.IsPathRooted(result));
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), result.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void ResolvePath_UnexpandedEnvVar_ThrowsInvalidOperationException()
        {
            var input = @"C:\%THIS_VAR_SHOULD_NOT_EXIST%\file.txt";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _processHelper.ResolvePath(input));

            Assert.Contains("could not be expanded", ex.Message);
        }

        [Fact]
        public void ResolvePath_RelativePath_ThrowsInvalidOperationException()
        {
            var input = @"relative\path\file.txt";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _processHelper.ResolvePath(input));

            Assert.Contains("relative", ex.Message);
        }

        [Fact]
        public void ResolvePath_NormalizesDotDotSegments()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "a", "b");
            var input = Path.Combine(baseDir, @"..\..\test");

            var result = _processHelper.ResolvePath(input);

            Assert.Equal(
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), "test")),
                result);
        }

        // -------------------------
        // ValidatePath tests
        // -------------------------

        [Fact]
        public void ValidatePath_NullInput_ReturnsFalse()
        {
            Assert.False(_processHelper.ValidatePath(null));
        }

        [Fact]
        public void ValidatePath_WhitespaceInput_ReturnsFalse()
        {
            Assert.False(_processHelper.ValidatePath("   "));
        }

        [Fact]
        public void ValidatePath_ExistingFile_ReturnsTrue()
        {
            var file = Path.GetTempFileName();

            try
            {
                Assert.True(_processHelper.ValidatePath(file, isFile: true));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void ValidatePath_NonExistingFile_ReturnsFalse()
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");

            Assert.False(_processHelper.ValidatePath(file, isFile: true));
        }

        [Fact]
        public void ValidatePath_ExistingDirectory_ReturnsTrue()
        {
            var dir = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

            try
            {
                Assert.True(_processHelper.ValidatePath(dir.FullName, isFile: false));
            }
            finally
            {
                dir.Delete();
            }
        }

        [Fact]
        public void ValidatePath_NonExistingDirectory_ReturnsFalse()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Assert.False(_processHelper.ValidatePath(dir, isFile: false));
        }

        [Fact]
        public void ValidatePath_UnexpandedEnvVar_ReturnsFalse()
        {
            var input = @"C:\%THIS_VAR_SHOULD_NOT_EXIST%\file.txt";

            Assert.False(_processHelper.ValidatePath(input));
        }

        [Fact]
        public void ValidatePath_RelativePath_ReturnsFalse()
        {
            var input = @"relative\path\file.txt";

            Assert.False(_processHelper.ValidatePath(input));
        }

        [Fact]
        public void ValidatePath_EnvVar_File_ReturnsTrue()
        {
            var tempFile = Path.GetTempFileName();

            try
            {
                // Convert absolute temp file path into one using %TEMP%
                var fileName = Path.GetFileName(tempFile);
                var envPath = Path.Combine("%TEMP%", fileName);

                Assert.True(_processHelper.ValidatePath(envPath, isFile: true));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ValidatePath_EnvVar_Directory_ReturnsTrue()
        {
            var dir = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

            try
            {
                var dirName = new DirectoryInfo(dir.FullName).Name;
                var envPath = Path.Combine("%TEMP%", dirName);

                Assert.True(_processHelper.ValidatePath(envPath, isFile: false));
            }
            finally
            {
                dir.Delete();
            }
        }

        [Theory]
        // 1. Basic cases
        [InlineData(null, "\"\"")]
        [InlineData("", "\"\"")]
        [InlineData("   ", "\"\"")]
        [InlineData("hello", "\"hello\"")]
        [InlineData("hello world", "\"hello world\"")]

        // 2. Backslash cases (not followed by quotes)
        [InlineData(@"C:\Program Files\Servy", "\"C:\\Program Files\\Servy\"")]
        [InlineData(@"C:\Path\With\Trailing\", "\"C:\\Path\\With\\Trailing\\\\\"")]

        // 3. The Quote-Escaping Gauntlet (The 2n+1 Rule)
        [InlineData("quote\"", "\"quote\\\"\"")]                  // " -> \"
        [InlineData(@"foo\""bar", "\"foo\\\\\\\"bar\"")]          // \" -> \\\"
        [InlineData(@"foo\\""bar", "\"foo\\\\\\\\\\\"bar\"")]     // \\" -> \\\\\"

        // 4. The Trailing Backslash Guard (The 2n Rule)
        [InlineData(@"C:\temp\", "\"C:\\temp\\\\\"")]            // \ -> \\
        [InlineData(@"C:\temp\\", "\"C:\\temp\\\\\\\\\"")]        // \\ -> \\\\

        // 5. Complex Realistic Scenarios
        [InlineData(@"/D ""config=C:\temp\""", "\"/D \\\"config=C:\\temp\\\\\\\"\"")]
        public void EscapeProcessArgument_ShouldMatchWin32ParsingRules(string input, string expected)
        {
            // Act
            var result = _processHelper.EscapeProcessArgument(input);

            // Assert
            Assert.Equal(expected, result);
        }

    }
}