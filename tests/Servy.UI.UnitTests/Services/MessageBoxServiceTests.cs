using Moq;
using Servy.UI.Services;
using Xunit;

namespace Servy.UI.UnitTests.Services
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
            _mockMessageBoxService.Object.ShowInfoAsync(message, caption);

            // Assert
            _mockMessageBoxService.Verify(m => m.ShowInfoAsync(message, caption), Times.Once);
        }

        [Fact]
        public void ShowWarning_CalledWithCorrectParameters()
        {
            // Arrange
            string message = "Warning message";
            string caption = "Warning";

            // Act
            _mockMessageBoxService.Object.ShowWarningAsync(message, caption);

            // Assert
            _mockMessageBoxService.Verify(m => m.ShowWarningAsync(message, caption), Times.Once);
        }

        [Fact]
        public void ShowError_CalledWithCorrectParameters()
        {
            // Arrange
            string message = "Error message";
            string caption = "Error";

            // Act
            _mockMessageBoxService.Object.ShowErrorAsync(message, caption);

            // Assert
            _mockMessageBoxService.Verify(m => m.ShowErrorAsync(message, caption), Times.Once);
        }
    }
}
