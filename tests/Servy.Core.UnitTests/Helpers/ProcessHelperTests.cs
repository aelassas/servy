using Servy.Core.Helpers;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ProcessHelperTests : IDisposable
    {
        private readonly ProcessHelper _processHelper;
        private readonly string _testRootName;
        private readonly string _testRoot;

        public ProcessHelperTests()
        {
            _processHelper = new ProcessHelper();

            // Every filesystem artifact this class creates lives under one GUID-suffixed root, as in
            // HelperTests and RotatingStreamWriterTests, so nothing is shared with another process
            // and a single guarded Dispose owns all cleanup.
            _testRootName = "ProcessHelperTests_" + Guid.NewGuid().ToString("N");
            _testRoot = Path.Combine(Path.GetTempPath(), _testRootName);
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, recursive: true);
                }
            }
            catch
            {
                // A cleanup failure must never replace the assertion failure that is already propagating.
            }
        }

        /// <summary>
        /// Creates an empty file inside the sandbox and returns its full path.
        /// </summary>
        private string CreateTempFile()
        {
            var file = Path.Combine(_testRoot, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(file, string.Empty);

            return file;
        }

        /// <summary>
        /// Creates a GUID-named directory inside the sandbox.
        /// </summary>
        private DirectoryInfo CreateTempDirectory()
        {
            return CreateTempDirectory(Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Creates a directory with the given name inside the sandbox.
        /// </summary>
        private DirectoryInfo CreateTempDirectory(string name)
        {
            return Directory.CreateDirectory(Path.Combine(_testRoot, name));
        }

        #region FormatCpuUsage Tests

        [Theory]
        [InlineData(0, "0.0%")]      // zero case
        [InlineData(0.03, "0.0%")]   // small non-zero rounds down to 0.0%
        [InlineData(1.0, "1.0%")]    // integer case
        [InlineData(1.04, "1.0%")]   // two decimals
        [InlineData(1.05, "1.1%")]   // two decimals
        [InlineData(1.06, "1.1%")]   // two decimals
        [InlineData(1.1, "1.1%")]    // already one decimal, formatted as-is
        [InlineData(1.23, "1.2%")]   // two decimals
        [InlineData(1.34, "1.3%")]   // rounding down
        [InlineData(1.35, "1.4%")]   // rounding up
        [InlineData(1.36, "1.4%")]   // rounding up
        public void FormatCpuUsage_ReturnsExpected(double input, string expected)
        {
            // Arrange & Act
            var result = _processHelper.FormatCpuUsage(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region FormatRamUsage Tests

        [Theory]
        [InlineData(0L, "0.0 B")]                                   // zero case, mirrors FormatCpuUsage
        [InlineData(512L, "512.0 B")]                               // < KB
        [InlineData(1023L, "1023.0 B")]                             // top of B branch, must NOT promote to KB
        [InlineData(1024L, "1.0 KB")]                               // exact KB, first promotion step
        [InlineData(2048L, "2.0 KB")]                               // exact KB
        [InlineData(3072L, "3.0 KB")]                               // KB range
        [InlineData(1048575L, "1.0 MB")]                            // 1 MB - 1, must promote rather than print 1024.0 KB
        [InlineData(1048576L, "1.0 MB")]                            // exact MB
        [InlineData((long)(1.5 * 1024 * 1024), "1.5 MB")]           // MB range
        [InlineData(1073741823L, "1.0 GB")]                         // 1 GB - 1, must promote rather than print 1024.0 MB
        [InlineData(1073741824L, "1.0 GB")]                         // exact GB
        [InlineData((long)(2.23 * 1024 * 1024 * 1024), "2.2 GB")]   // GB range, non-exact fraction
        [InlineData((long)(2.25 * 1024 * 1024 * 1024), "2.3 GB")]   // GB range
        [InlineData(1099511627775L, "1.0 TB")]                      // 1 TB - 1, must promote rather than print 1024.0 GB
        [InlineData(1099511627776L, "1.0 TB")]                      // exact TB
        [InlineData((long)(3.75 * 1024 * 1024 * 1024 * 1024), "3.8 TB")] // TB range
        public void FormatRamUsage_ReturnsExpected(long input, string expected)
        {
            // Arrange & Act
            var result = _processHelper.FormatRamUsage(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region ResolvePath Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void ResolvePath_NullOrWhitespace_ReturnsNull(string input)
        {
            // Arrange & Act
            var result = _processHelper.ResolvePath(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ResolvePath_AbsolutePath_NoEnvVars_ReturnsNormalizedPath()
        {
            // Arrange
            var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var input = tempDir + Path.DirectorySeparatorChar;

            // Act
            var result = _processHelper.ResolvePath(input);

            // Assert
            Assert.Equal(Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar), result?.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void ResolvePath_AbsolutePath_WithEnvVar_ExpandsSuccessfully()
        {
            // Arrange
            var input = "%TEMP%";

            // Act
            var result = _processHelper.ResolvePath(input);

            // Assert
            Assert.True(Path.IsPathRooted(result));
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), result.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void ResolvePath_RelativePath_ThrowsInvalidOperationException()
        {
            // Arrange
            var input = @"relative\path\file.txt";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _processHelper.ResolvePath(input));

            Assert.Contains("Only absolute paths are allowed", ex.Message);
        }

        [Fact]
        public void ResolvePath_NormalizesDotDotSegments()
        {
            // Arrange
            var baseDir = Path.Combine(Path.GetTempPath(), "a", "b");
            var input = Path.Combine(baseDir, @"..\..\test");

            // Act
            var result = _processHelper.ResolvePath(input);

            // Assert
            Assert.Equal(
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), "test")),
                result);
        }

        [Fact]
        public void ResolvePath_ValidExistingAbsolutePath_ResolvesAndNormalizes()
        {
            // Arrange
            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string leaf = Path.GetFileName(baseDir);
            string rawPath = Path.Combine(baseDir, "..", leaf, "app.log");
            string expected = Path.Combine(baseDir, "app.log");

            // Act
            string result = _processHelper.ResolvePath(rawPath);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ResolvePath_NonExistentPathWithLiteralPercentSegments_ResolvesSuccessfully()
        {
            // Arrange: Tests Issue #2082
            // A future log destination that contains literal '%' bounds and does not exist yet.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string futurePath = Path.Combine(baseDir, "runs_%batch_id%", "stdout.log");

            // Act
            string result = _processHelper.ResolvePath(futurePath);

            // Assert
            // The method must not throw an exception on non-existent targets,
            // allowing the application to create directories dynamically later.
            string expected = Path.GetFullPath(futurePath);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ResolvePath_ValidSystemVariable_ExpandsCorrectly()
        {
            // Arrange
            string systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            string input = "%SystemRoot%\\System32\\cmd.exe";

            // Act
            string result = _processHelper.ResolvePath(input);

            // Assert
            Assert.Equal(Path.Combine(systemRoot, "System32\\cmd.exe"), result, ignoreCase: true);
        }

        #endregion

        #region ValidatePath Tests

        [Fact]
        public void ValidatePath_NullInput_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.False(_processHelper.ValidatePath(null));
        }

        [Fact]
        public void ValidatePath_WhitespaceInput_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.False(_processHelper.ValidatePath("    "));
        }

        [Fact]
        public void ValidatePath_ExistingFile_ReturnsTrue()
        {
            // Arrange
            var file = CreateTempFile();

            // Act & Assert
            Assert.True(_processHelper.ValidatePath(file, isFile: true));
        }

        [Fact]
        public void ValidatePath_NonExistingFile_ReturnsFalse()
        {
            // Arrange
            var file = Path.Combine(_testRoot, Guid.NewGuid() + ".txt");

            // Act & Assert
            Assert.False(_processHelper.ValidatePath(file, isFile: true));
        }

        [Fact]
        public void ValidatePath_ExistingDirectory_ReturnsTrue()
        {
            // Arrange
            var dir = CreateTempDirectory();

            // Act & Assert
            Assert.True(_processHelper.ValidatePath(dir.FullName, isFile: false));
        }

        [Fact]
        public void ValidatePath_NonExistingDirectory_ReturnsFalse()
        {
            // Arrange
            var dir = Path.Combine(_testRoot, Guid.NewGuid().ToString());

            // Act & Assert
            Assert.False(_processHelper.ValidatePath(dir, isFile: false));
        }

        [Fact]
        public void ValidatePath_UnexpandedEnvVar_IsTreatedAsLiteralSegment()
        {
            // Arrange: the name only has to look like an unexpanded variable, so the GUID inside the
            // token keeps that property while making the directory this test's own.
            var dir = CreateTempDirectory("%THIS_VAR_SHOULD_NOT_EXIST_" + Guid.NewGuid().ToString("N") + "%");

            // Act & Assert
            // The literal '%...%' directory exists, so validation succeeds:
            // the segment is a path component, not a failed expansion.
            Assert.True(_processHelper.ValidatePath(dir.FullName, isFile: false));
        }

        [Fact]
        public void ValidatePath_RelativePath_ReturnsFalse()
        {
            // Arrange
            var input = @"relative\path\file.txt";

            // Act & Assert
            Assert.False(_processHelper.ValidatePath(input));
        }

        [Fact]
        public void ValidatePath_EnvVar_File_ReturnsTrue()
        {
            // Arrange
            var tempFile = CreateTempFile();

            // Convert absolute temp file path into one using %TEMP%
            var fileName = Path.GetFileName(tempFile);
            var envPath = Path.Combine("%TEMP%", _testRootName, fileName);

            // Act & Assert
            Assert.True(_processHelper.ValidatePath(envPath, isFile: true));
        }

        [Fact]
        public void ValidatePath_EnvVar_Directory_ReturnsTrue()
        {
            // Arrange
            var dir = CreateTempDirectory();

            var dirName = new DirectoryInfo(dir.FullName).Name;
            var envPath = Path.Combine("%TEMP%", _testRootName, dirName);

            // Act & Assert
            Assert.True(_processHelper.ValidatePath(envPath, isFile: false));
        }

        [Fact]
        public void ValidatePath_ExistingDirectory_AsFile_ReturnsFalse()
        {
            // Arrange: Establish a folder target to probe as an illegal file type reference
            var dir = CreateTempDirectory();

            // Act & Assert
            Assert.False(_processHelper.ValidatePath(dir.FullName, isFile: true));
        }

        [Fact]
        public void ValidatePath_ExistingFile_AsDirectory_ReturnsFalse()
        {
            // Arrange: Establish an active file token to probe as an illegal folder container reference
            var file = CreateTempFile();

            // Act & Assert
            Assert.False(_processHelper.ValidatePath(file, isFile: false));
        }

        #endregion
    }
}
