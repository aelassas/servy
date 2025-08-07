using Moq;
using Xunit;
using Servy.Services;

namespace Servy.UnitTests.Services
{
    public class MessageBoxServiceTests
    {
        private readonly Mock<IMessageBoxService> _mockMessageBoxService;

        public MessageBoxServiceTests()
        {
            _mockMessageBoxService = new Mock<IMessageBoxService>();
        }

        [Fact]
        public void ShowInfo_CalledWithCorrectParameters()
        {
            // Arrange
            string message = "Info message";
            string caption = "Information";

            // Act
            _mockMessageBoxService.Object.ShowInfo(message, caption);

            // Assert
            _mockMessageBoxService.Verify(m => m.ShowInfo(message, caption), Times.Once);
        }

        [Fact]
        public void ShowWarning_CalledWithCorrectParameters()
        {
            // Arrange
            string message = "Warning message";
            string caption = "Warning";

            // Act
            _mockMessageBoxService.Object.ShowWarning(message, caption);

            // Assert
            _mockMessageBoxService.Verify(m => m.ShowWarning(message, caption), Times.Once);
        }

        [Fact]
        public void ShowError_CalledWithCorrectParameters()
        {
            // Arrange
            string message = "Error message";
            string caption = "Error";

            // Act
            _mockMessageBoxService.Object.ShowError(message, caption);

            // Assert
            _mockMessageBoxService.Verify(m => m.ShowError(message, caption), Times.Once);
        }
    }
}
