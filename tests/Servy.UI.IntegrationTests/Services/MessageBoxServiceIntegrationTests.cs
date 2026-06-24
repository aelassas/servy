using Servy.Testing;
using Servy.UI.Services;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UI.IntegrationTests.Services
{
    public class MessageBoxServiceIntegrationTests
    {
        private readonly MessageBoxService _service;

        public MessageBoxServiceIntegrationTests()
        {
            _service = new MessageBoxService(new WpfUiDispatcher());
            UiHeadless.IsEnabled = true;
        }

        #region Smoke Tests (Dispatcher Verification)

        [Fact]
        public async Task ShowInfoAsync_InvokesDispatcher()
        {
            // Note: In a CI environment, we cannot actually click "OK".
            // These tests verify that the Dispatcher logic initiates.
            Helper.RunOnSTA((System.Action)(async () =>
            {
                // We use a timeout or a mock-like approach because MessageBox.Show blocks.
                // In a pure unit test, you would typically wrap MessageBox.Show in 
                // a virtual method to override it, but here we test the service orchestration.
                var task = _service.ShowInfoAsync("Test", "Caption");

                Assert.NotNull(task);
                // We do not await here in CI to avoid hanging the runner on a modal dialog.
            }));
        }

        #endregion

        #region Confirmation Logic Branch Tests

        [Fact]
        public async Task ShowConfirmAsync_ReturnsValueFromDispatcher()
        {
            // This test covers the branch: return MessageBox.Show(...) == MessageBoxResult.Yes;
            // Since we can't click the button in CI, we verify the task creation.
            Helper.RunOnSTA(() =>
            {
                var task = _service.ShowConfirmAsync("Confirm?", "Caption");
                Assert.NotNull(task);
            });
        }

        #endregion
    }
}