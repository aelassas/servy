using Servy.Core.Validation;

namespace Servy.Core.UnitTests.Validation
{
    public class ImportGuardTests : IDisposable
    {
        private readonly string _tempDirectory;

        public ImportGuardTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [Fact]
        public void ValidatePathSecurityAndSize_ValidFile_DelegatesSuccessfullyAndLoadsContent()
        {
            // Arrange
            string filePath = Path.Combine(_tempDirectory, "import_delegate.json");
            string expectedContent = "{\"servy\": true}";
            File.WriteAllText(filePath, expectedContent);

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.ValidPath);
            Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public void ValidatePathSecurityAndSize_InvalidFile_DelegatesSuccessfullyAndReturnsFailure()
        {
            // Arrange
            string filePath = Path.Combine(_tempDirectory, "invalid_delegate.txt");
            File.WriteAllText(filePath, "invalid extension context");

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string? content);

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(content);
            Assert.NotNull(result.ErrorMessage);
        }
    }
}