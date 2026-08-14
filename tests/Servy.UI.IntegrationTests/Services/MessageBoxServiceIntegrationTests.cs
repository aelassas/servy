using Servy.Testing;
using Servy.UI.Services;

namespace Servy.UI.IntegrationTests.Services
{
    [Collection("UiSta")]
    public class MessageBoxServiceIntegrationTests
    {
        private readonly MessageBoxService _service;

        public MessageBoxServiceIntegrationTests()
        {
            // Arrange
            _service = new MessageBoxService(new WpfUiDispatcher());
        }

        #region Smoke Tests (Dispatcher Verification)

        [Fact]
        public async Task ShowInfoAsync_InvokesDispatcher()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                string message = "Test";
                string caption = "Caption";

                // Act
                var task = _service.ShowInfoAsync(message, caption);

                // Assert
                Assert.NotNull(task);
                await task;
            });
        }

        #endregion

        #region Confirmation Logic Branch Tests

        [Fact]
        public async Task ShowConfirmAsync_ReturnsValueFromDispatcher()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                string message = "Confirm?";
                string caption = "Caption";

                // Act
                bool result = await _service.ShowConfirmAsync(message, caption);

                // Assert
                Assert.True(result);
            });
        }

        #endregion
    }
}
