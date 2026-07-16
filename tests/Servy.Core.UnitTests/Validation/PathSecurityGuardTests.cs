using Servy.Core.Resources;
using Servy.Core.Validation;
using Servy.Testing;

namespace Servy.Core.UnitTests.Validation
{
    public class PathSecurityGuardTests : TempDirectoryTestBase
    {
        #region Common Security Rules

        [Fact]
        public void ValidatePath_InvalidPathChars_ReturnsFail()
        {
            // Arrange
            string invalidPath = new string(Path.GetInvalidPathChars());

            // Act
            var result = PathSecurityGuard.ValidatePath(invalidPath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Strings.Msg_InvalidPath, result.ErrorMessage);
            Assert.Null(stream);
        }

        [Theory]
        [InlineData(@"\\server\share\config.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData(@"\\127.0.0.1\c$\config.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData(@"\\server\share\export.json", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        public void ValidatePath_UncPath_ReturnsFail(string uncPath, FileMode mode, FileAccess access, FileShare share)
        {
            // Act
            var result = PathSecurityGuard.ValidatePath(uncPath, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(stream);
            Assert.True(
                result.ErrorMessage.IndexOf("UNC paths", StringComparison.OrdinalIgnoreCase) >= 0 ||
                result.ErrorMessage.IndexOf("UNC destination", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected UNC guard rejection. Actual: {result.ErrorMessage}");
        }

        [Theory]
        [InlineData("CON.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData("PRN.xml", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        [InlineData("COM1.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData("LPT1.xml", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        public void ValidatePath_ReservedDeviceName_ReturnsFail(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            // Act
            var result = PathSecurityGuard.ValidatePath(fileName, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(stream);

            bool hitDosGuard = result.ErrorMessage.IndexOf(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase) >= 0;
            bool hitUncGuard = result.ErrorMessage.IndexOf("UNC paths", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               result.ErrorMessage.IndexOf("UNC destination", StringComparison.OrdinalIgnoreCase) >= 0;

            Assert.True(hitDosGuard || hitUncGuard,
                $"Expected DOS device payload to be intercepted by either the DOS guard or the UNC guard. Actual error: {result.ErrorMessage}");
        }

        [Theory]
        [InlineData(FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        public void ValidatePath_ProtectedFolder_ReturnsFail(FileMode mode, FileAccess access, FileShare share)
        {
            // Arrange
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string filePath = Path.Combine(protectedDir, "sys_config.json");

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(protectedDir, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Null(stream);
        }

        [Theory]
        [InlineData("config.txt", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData("export.exe", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        [InlineData("config.yaml", FileMode.Open, FileAccess.Read, FileShare.Read)]
        public void ValidatePath_InvalidExtension_ReturnsFail(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Path.GetExtension(fileName).ToLowerInvariant(), result.ErrorMessage);
            Assert.Null(stream);
        }

        [Fact]
        public void ValidatePath_Symlink_ReturnsFail()
        {
            // Arrange
            string targetPath = Path.Combine(TempDirectory, "real_target.json");
            string symlinkPath = Path.Combine(TempDirectory, "symlink_target.json");

            File.WriteAllText(targetPath, "{}");

            try
            {
                File.CreateSymbolicLink(symlinkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Skip test gracefully if the runner platform environment lacks symlink creation tokens
                return;
            }

            // Act
            var result = PathSecurityGuard.ValidatePath(symlinkPath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(stream);
        }

        #endregion

        #region Operational Mode Differences

        [Theory]
        [InlineData("missing_import.json")]
        [InlineData("missing_import.xml")]
        public void ValidatePath_ImportMode_FileDoesNotExist_ReturnsFail(string fileName)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            Assert.NotNull(result.ErrorMessage);
            Assert.False(result.IsValid);
            Assert.Null(stream);
        }

        [Theory]
        [InlineData("new_export.json")]
        [InlineData("new_export.xml")]
        public void ValidatePath_ExportMode_FileDoesNotExist_CreatesHandleAndSucceeds(string fileName)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, out var stream);

            // Assert
            try
            {
                Assert.True(result.IsValid);
                Assert.NotNull(stream);
                Assert.True(stream.CanWrite);
            }
            finally
            {
                stream?.Dispose();
            }
        }

        [Theory]
        [InlineData("valid_engine_config.json", "{}")]
        [InlineData("valid_engine_config.xml", "<root/>")]
        public void ValidatePath_ValidLocalAllowedFile_PassesAllGuardsAndExposesStream(string fileName, string fileContent)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);
            File.WriteAllText(filePath, fileContent);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            try
            {
                Assert.True(result.IsValid);
                Assert.NotNull(result.ValidPath);
                Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
                using (var reader = new StreamReader(stream!))
                {
                    Assert.Equal(fileContent, reader.ReadToEnd());
                }
            }
            finally
            {
                stream?.Dispose();
            }
        }

        #endregion
    }
}