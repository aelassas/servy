using Moq;
using Servy.UI.Services;
using Xunit;

namespace Servy.UI.UnitTests.Services
{
    public class FileDialogServiceTests
    {
        private readonly Mock<IFileDialogService> _mockFileDialogService;

        public FileDialogServiceTests()
        {
            _mockFileDialogService = new Mock<IFileDialogService>();
        }

        [Fact]
        public void OpenExecutable_ReturnsFilePath()
        {
            // Arrange
            var expectedPath = @"C:\Program Files\MyApp\MyApp.exe";
            _mockFileDialogService.Setup(f => f.OpenExecutable()).Returns(expectedPath);

            // Act
            var actualPath = _mockFileDialogService.Object.OpenExecutable();

            // Assert
            Assert.Equal(expectedPath, actualPath);
        }

        [Fact]
        public void OpenFolder_ReturnsFolderPath()
        {
            // Arrange
            var expectedPath = @"C:\MyFolder";
            _mockFileDialogService.Setup(f => f.OpenFolder()).Returns(expectedPath);

            // Act
            var actualPath = _mockFileDialogService.Object.OpenFolder();

            // Assert
            Assert.Equal(expectedPath, actualPath);
        }

        [Fact]
        public void SaveFile_ReturnsFilePath()
        {
            // Arrange
            var dialogTitle = "Save Log";
            var expectedPath = @"C:\Logs\output.txt";
            _mockFileDialogService.Setup(f => f.SaveFile(dialogTitle)).Returns(expectedPath);

            // Act
            var actualPath = _mockFileDialogService.Object.SaveFile(dialogTitle);

            // Assert
            Assert.Equal(expectedPath, actualPath);
        }

        [Fact]
        public void SaveFile_Canceled_ReturnsNull()
        {
            // Arrange
            _mockFileDialogService.Setup(f => f.SaveFile(It.IsAny<string>())).Returns(string.Empty);

            // Act
            var actualPath = _mockFileDialogService.Object.SaveFile("Export Data");

            // Assert
            Assert.Empty(actualPath);
        }
    }
}
