using Servy.Core.Config;
using Servy.Core.Resources;
using Servy.Core.Validation;
using Servy.Testing;

namespace Servy.Core.UnitTests.Validation
{
    public class ImportGuardTests : TempDirectoryTestBase
    {
        [Fact]
        public void ValidatePathSecurityAndSize_ValidFile_DelegatesSuccessfullyAndLoadsContent()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "import_delegate.json");
            string expectedContent = "{\"servy\": true}";
            File.WriteAllText(filePath, expectedContent);

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.None, result.FailureKind);
            Assert.NotNull(result.ValidPath);
            // ResolvedPath is the kernel-resolved target (GetFinalPathNameByHandle), not the caller's string:
            // 8.3 components such as C:\Users\RUNNER~1 are expanded, so assert same file rather than same text.
            Assert.Equal(Path.GetFileName(filePath), Path.GetFileName(result.ValidPath!.ResolvedPath));
            Assert.True(File.Exists(result.ValidPath.ResolvedPath));
            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public void ValidatePathSecurityAndSize_InvalidFile_DelegatesSuccessfullyAndReturnsFailure()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "invalid_delegate.txt");
            File.WriteAllText(filePath, "invalid extension context");

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.Null(content);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(string.Format(Strings.Msg_SecurityInvalidFileType, ".txt"), result.ErrorMessage);
        }

        [Fact]
        public void ValidatePathSecurityAndSize_FileExceedsSizeLimit_ReturnsFailureWithoutContent()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "huge.json");
            using (var fs = new FileStream(filePath, FileMode.CreateNew))
            {
                // SetLength extends the file without writing any data (the tail reads as zeros),
                // so this costs 10 MiB of allocated disk but no write IO.
                fs.SetLength(AppConfig.MaxConfigFileSizeBytes + 1);
            }

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.Null(content);

            // The production message is formatted with securityCheck.ValidPath.ResolvedPath - the
            // kernel-resolved target (GetFinalPathNameByHandle) - not the caller's string. 8.3
            // components such as C:\Users\RUNNER~1 are expanded there, so comparing against
            // filePath fails on any runner whose temp path is shortened (the #5757 class).
            // Resolve through the same gate the production code uses.
            var pathCheck = PathSecurityGuard.ValidatePath(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, out var validatedStream);
            string resolvedPath;
            using (validatedStream)
            {
                Assert.True(pathCheck.IsValid);
                resolvedPath = pathCheck.ValidPath.ResolvedPath;
            }

            string expectedMessage = string.Format(Strings.Msg_ConfigSizeLimitReached, resolvedPath, AppConfig.MaxConfigFileSizeMB);
            Assert.Equal(expectedMessage, result.ErrorMessage);
        }
    }
}
