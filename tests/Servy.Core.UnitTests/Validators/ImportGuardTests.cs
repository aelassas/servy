using Servy.Core.Validators;

namespace Servy.Core.UnitTests.Validators
{
    public class ImportGuardTests : IDisposable
    {
        private readonly string _tempDirectory;

        public ImportGuardTests()
        {
            // Set up an isolated temporary directory for file-based tests
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            // Clean up all temporary files after tests complete
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        #region ValidatePathAndSize Tests

        [Fact]
        public void ValidatePathAndSize_NullOrEmptyPath_ReturnsInvalidPathError()
        {
            // Act
            var result = ImportGuard.ValidatePathAndSize(string.Empty, 5, "Size Limit: {0}");

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(result.ValidPath);
            Assert.NotNull(result.ErrorMessage);
            // Validates that it hit the ArgumentException inside Path.GetFullPath
        }

        [Fact]
        public void ValidatePathAndSize_FileDoesNotExist_ReturnsFileNotFoundError()
        {
            // Arrange
            string nonExistentPath = Path.Combine(_tempDirectory, "does_not_exist.json");

            // Act
            var result = ImportGuard.ValidatePathAndSize(nonExistentPath, 5, "Size Limit: {0}");

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(result.ValidPath);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ValidatePathAndSize_FileExceedsMaxSize_ReturnsSizeLimitError()
        {
            // Arrange
            string filePath = Path.Combine(_tempDirectory, "large_config.json");
            File.WriteAllBytes(filePath, new byte[1024 * 1024 * 2]); // Create 2MB file

            // Assume maxFileSizeMb is 1
            int maxMb = 1;
            string format = "File {0} is too large.";

            // Act
            var result = ImportGuard.ValidatePathAndSize(filePath, maxMb, format);

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(result.ValidPath);
            Assert.Equal(string.Format(format, filePath), result.ErrorMessage);
        }

        [Fact]
        public void ValidatePathAndSize_ValidFile_ReturnsSuccessWithResolvedPath()
        {
            // Arrange
            string filePath = Path.Combine(_tempDirectory, "valid_config.json");
            File.WriteAllText(filePath, "{}"); // Valid small file

            // Act
            var result = ImportGuard.ValidatePathAndSize(filePath, 5, "Limit: {0}");

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.ValidPath);
            Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
            Assert.Null(result.ErrorMessage);
        }

        #endregion

        #region ValidatePathSecurity Tests

        [Fact]
        public void ValidatePathSecurity_InvalidPathChars_ReturnsFail()
        {
            // Arrange
            string invalidPath = new string(Path.GetInvalidPathChars());

            // Act
            var result = ImportGuard.ValidatePathSecurity(invalidPath);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
        }

        [Theory]
        [InlineData(@"\\server\share\config.json")]
        [InlineData(@"\\127.0.0.1\c$\config.json")]
        public void ValidatePathSecurity_UncPath_ReturnsFail(string uncPath)
        {
            // Act
            var result = ImportGuard.ValidatePathSecurity(uncPath);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            // Verifies the Infiltration Guard (UNC Path Block) triggers
        }

        [Theory]
        [InlineData("CON.json")]
        [InlineData("PRN.xml")]
        [InlineData("COM1.json")]
        [InlineData("LPT¹.xml")]
        public void ValidatePathSecurity_ReservedDeviceName_ReturnsFail(string fileName)
        {
            string filePath = fileName;

            // Act
            var result = ImportGuard.ValidatePathSecurity(filePath);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);

            // ROBUSTNESS: .NET Framework 4.8 and .NET 10.0 canonicalize DOS device names differently.
            // - .NET 10.0: Path.GetFullPath("COM1.json") -> "D:\a\...\COM1.json". Hits the DOS Guard.
            // - .NET 4.8: Path.GetFullPath("COM1.json") -> "\\.\COM1". Hits the UNC Guard early because it starts with "\\".
            // Both are secure and block the input. We validate that at least one of these layers caught the payload.
            bool hitDosGuard = result.ErrorMessage.IndexOf(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase) >= 0;
            bool hitUncGuard = result.ErrorMessage.IndexOf("UNC paths", StringComparison.OrdinalIgnoreCase) >= 0;

            Assert.True(hitDosGuard || hitUncGuard,
                $"Expected DOS device payload to be intercepted by either the DOS guard or the UNC guard. Actual error: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidatePathSecurity_ProtectedFolder_ReturnsFail()
        {
            // Arrange
            // Attempting to import a config from the protected Windows directory
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string filePath = Path.Combine(protectedDir, "win_config.json");

            // Act
            var result = ImportGuard.ValidatePathSecurity(filePath);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(protectedDir, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("config.txt")]
        [InlineData("config.exe")]
        [InlineData("config.yaml")]
        public void ValidatePathSecurity_InvalidExtension_ReturnsFail(string fileName)
        {
            // Arrange
            string filePath = Path.Combine(_tempDirectory, fileName);

            // Act
            var result = ImportGuard.ValidatePathSecurity(filePath);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Path.GetExtension(fileName).ToLowerInvariant(), result.ErrorMessage);
        }

        [Fact]
        public void ValidatePathSecurity_ValidLocalJsonFile_PassesAllGuardsAndReturnsSuccess()
        {
            // Arrange
            string filePath = Path.Combine(_tempDirectory, "secure_config.json");
            File.WriteAllText(filePath, "{ \"setting\": 1 }");

            // Act
            var result = ImportGuard.ValidatePathSecurity(filePath);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.ValidPath);
            Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void ValidatePathSecurity_FileDoesNotExistForHandleResolution_ReturnsFail()
        {
            // Arrange
            // Bypasses initial string checks (like UNC and Extensions) but fails at the physical 
            // FileStream handle resolution step because it doesn't exist on disk.
            string filePath = Path.Combine(_tempDirectory, "phantom_config.xml");

            // Act
            var result = ImportGuard.ValidatePathSecurity(filePath);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            // Expecting the FileNotFoundException caught by the broad try/catch around the Handle Resolution
        }

        [Fact]
        public void ValidatePathSecurity_Symlink_ReturnsFail()
        {
            // Arrange
            string targetPath = Path.Combine(_tempDirectory, "real_config.json");
            string symlinkPath = Path.Combine(_tempDirectory, "symlink_config.json");

            File.WriteAllText(targetPath, "{}");

            try
            {
                // Note: File.CreateSymbolicLink requires .NET 6+.
                // On Windows, this requires Developer Mode or Administrator privileges.
                File.CreateSymbolicLink(symlinkPath, targetPath);
            }
            catch (IOException)
            {
                // Skip test gracefully if the CI runner lacks symlink creation privileges
                return;
            }
            catch (UnauthorizedAccessException)
            {
                // Skip test gracefully if the CI runner lacks symlink creation privileges
                return;
            }

            // Act
            var result = ImportGuard.ValidatePathSecurity(symlinkPath);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            // Validates that the Reparse Point Guard catches file-level symbolic links
        }

        #endregion
    }
}