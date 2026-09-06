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

        #region Branch: Headless Short-Circuit

        // UiHeadlessFixture, reached through [Collection("UiSta")], sets UiHeadless.IsEnabled for the
        // whole collection, and ShowCoreAsync returns on exactly that condition before it touches the
        // dispatcher. These two tests therefore pin the headless contract - the console line and the
        // auto-confirm - rather than any dispatch, which is what their names used to claim.

        [Fact]
        public async Task ShowInfoAsync_InHeadlessMode_WritesHeadlessLineAndCompletes()
        {
            // Arrange
            string message = "Test";
            string caption = "Caption";

            // Act
            var captured = await ConsoleCapture.RunAsync(() => _service.ShowInfoAsync(message, caption));

            // Assert
            Assert.Contains($"[HEADLESS INFO] {caption}: {message}", captured.StdOut);
            Assert.DoesNotContain("Auto-answering", captured.StdOut);
        }

        #endregion

        #region Confirmation Logic Branch Tests

        [Fact]
        public async Task ShowConfirmAsync_InHeadlessMode_AutoAnswersYes()
        {
            // Arrange
            string message = "Confirm?";
            string caption = "Caption";

            // Act
            var captured = await ConsoleCapture.RunAsync(() => _service.ShowConfirmAsync(message, caption));

            // Assert
            Assert.True(captured.Result);
            Assert.Contains($"[HEADLESS CONFIRM] {caption}: {message} -> Auto-answering 'Yes'.", captured.StdOut);
        }

        #endregion
    }
}
