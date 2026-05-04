using Moq;
using Servy.UI.Helpers;
using Servy.UI.Services;

namespace Servy.UI.UnitTests.Helpers
{
    public class ImportGuardTests
    {
        private readonly Mock<IMessageBoxService> _mockMessageBox;
        private const string Caption = "Import Test";
        private const string SizeLimitFormat = "File {0} is too big.";

        public ImportGuardTests()
        {
            _mockMessageBox = new Mock<IMessageBoxService>();
        }

        #region ValidateFileSizeAsync Tests

        [Fact]
        public async Task ValidateFileSizeAsync_EmptyPath_ReturnsFalse()
        {
            // Branch: try { Path.GetFullPath } catch (Exception)
            // An empty string triggers an ArgumentException in Path.GetFullPath 
            // across all .NET versions.
            var invalidPath = "";

            var result = await ImportGuard.ValidateFileSizeAsync(
                invalidPath,
                _mockMessageBox.Object,
                Caption,
                1,
                SizeLimitFormat);

            Assert.False(result);

            // Verify message box was NOT shown because we returned false in the catch block
            _mockMessageBox.Verify(m => m.ShowErrorAsync(
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ValidateFileSizeAsync_FileNotFound_ReturnsFalseAndShowsError()
        {
            // Branch: if (!fileInfo.Exists)
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var result = await ImportGuard.ValidateFileSizeAsync(
                nonExistentPath,
                _mockMessageBox.Object,
                Caption,
                1,
                SizeLimitFormat);

            Assert.False(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), Caption), Times.Once);
        }

        [Fact]
        public async Task ValidateFileSizeAsync_FileSizeExceedsLimit_ReturnsFalseAndShowsError()
        {
            // Branch: if (fileInfo.Length > (long)maxFileSizeMb * 1024 * 1024)
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create a 2MB file
                using (var fs = new FileStream(tempFile, FileMode.Create))
                {
                    fs.SetLength(2 * 1024 * 1024);
                }

                // Set limit to 1MB
                var result = await ImportGuard.ValidateFileSizeAsync(
                    tempFile,
                    _mockMessageBox.Object,
                    Caption,
                    1,
                    SizeLimitFormat);

                Assert.False(result);
                _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), Caption), Times.Once);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ValidateFileSizeAsync_ValidFile_ReturnsTrue()
        {
            // Branch: Final Happy Path
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create a small 1KB file
                File.WriteAllText(tempFile, "Valid content");

                var result = await ImportGuard.ValidateFileSizeAsync(
                    tempFile,
                    _mockMessageBox.Object,
                    Caption,
                    1,
                    SizeLimitFormat);

                Assert.True(result);
                _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        #endregion
    }
}