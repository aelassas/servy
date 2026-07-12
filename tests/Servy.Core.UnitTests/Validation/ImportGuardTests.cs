using Servy.Core.Validation;
using Servy.Testing;
using System;
using System.IO;
using Xunit;

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
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string content);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.ValidPath);
            Assert.Equal(filePath, result.ValidPath.ResolvedPath);
            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public void ValidatePathSecurityAndSize_InvalidFile_DelegatesSuccessfullyAndReturnsFailure()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "invalid_delegate.txt");
            File.WriteAllText(filePath, "invalid extension context");

            // Act
            var result = ImportGuard.ValidatePathSecurityAndSize(filePath, out string content);

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(content);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(".txt", result.ErrorMessage);
        }
    }
}