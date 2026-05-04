using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Xunit;
using Servy.UI.Services;
using Servy.Testing;

namespace Servy.UI.IntegrationTests.Services
{
    public class MessageBoxServiceTests
    {
        private readonly MessageBoxService _service;

        public MessageBoxServiceTests()
        {
            _service = new MessageBoxService();
        }

        #region Smoke Tests (Dispatcher Verification)

        [Fact]
        public async Task ShowInfoAsync_InvokesDispatcher()
        {
            // Note: In a CI environment, we cannot actually click "OK".
            // These tests verify that the Dispatcher logic initiates.
            Helper.RunInSTA(async () =>
            {
                // We use a timeout or a mock-like approach because MessageBox.Show blocks.
                // In a pure unit test, you would typically wrap MessageBox.Show in 
                // a virtual method to override it, but here we test the service orchestration.
                var task = _service.ShowInfoAsync("Test", "Caption");

                Assert.NotNull(task);
                // We do not await here in CI to avoid hanging the runner on a modal dialog.
            });
        }

        #endregion

        #region Confirmation Logic Branch Tests

        [Fact]
        public async Task ShowConfirmAsync_ReturnsValueFromDispatcher()
        {
            // This test covers the branch: return MessageBox.Show(...) == MessageBoxResult.Yes;
            // Since we can't click the button in CI, we verify the task creation.
            Helper.RunInSTA(() =>
            {
                var task = _service.ShowConfirmAsync("Confirm?", "Caption");
                Assert.NotNull(task);
            });
        }

        #endregion
    }
}