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

        #region ValidatePathSecurity Tests

        [Fact]
        public void ValidatePathSecurity_InvalidPathChars_ReturnsFail()
        {
            // Arrange
            string invalidPath = new string(Path.GetInvalidPathChars());

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(invalidPath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(content);
        }

        [Theory]
        [InlineData(@"\\server\share\config.json")]
        [InlineData(@"\\127.0.0.1\c$\config.json")]
        public void ValidatePathSecurity_UncPath_ReturnsFail(string uncPath)
        {
            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(uncPath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(content);
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
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(content);

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
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(protectedDir, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Null(content);
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
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Path.GetExtension(fileName).ToLowerInvariant(), result.ErrorMessage);
            Assert.Null(content);
        }

        [Fact]
        public void ValidatePathSecurity_ValidLocalJsonFile_PassesAllGuardsAndReturnsSuccess()
        {
            // Arrange
            string filePath = Path.Combine(_tempDirectory, "secure_config.json");
            File.WriteAllText(filePath, "{ \"setting\": 1 }");

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.ValidPath);
            Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(content);
            Assert.NotEmpty(content);
        }

        [Fact]
        public void ValidatePathSecurity_FileDoesNotExistForHandleResolution_ReturnsFail()
        {
            // Arrange
            // Bypasses initial string checks (like UNC and Extensions) but fails at the physical 
            // FileStream handle resolution step because it doesn't exist on disk.
            string filePath = Path.Combine(_tempDirectory, "phantom_config.xml");

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(content);
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
            var result = ImportGuard.ValidatePathSecurityAndSize(symlinkPath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(content);
            // Validates that the Reparse Point Guard catches file-level symbolic links
        }

        #endregion
    }
}