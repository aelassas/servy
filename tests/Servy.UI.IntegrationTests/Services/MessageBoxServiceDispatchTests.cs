using Moq;
using Servy.UI.Services;

namespace Servy.UI.IntegrationTests.Services
{
    /// <summary>
    /// Unit-level tests for <see cref="MessageBoxService"/> that drive the two branches
    /// <see cref="MessageBoxServiceIntegrationTests"/> cannot reach: the constructor null-guard,
    /// and the non-headless dispatch call. They use a mocked <see cref="IUiDispatcher"/> and never
    /// touch a real WPF dispatcher, but they live in this project because it is the only test
    /// project that references Servy.UI, and in this collection because
    /// <see cref="UiHeadlessFixture"/> owns the process-global <see cref="UiHeadless.IsEnabled"/>
    /// flag that the dispatch test has to flip.
    /// </summary>
    [Collection(UiStaCollection.Name)]
    public class MessageBoxServiceDispatchTests
    {
        #region Constructor Guard

        [Fact]
        public void Constructor_NullDispatcher_ThrowsArgumentNullException()
        {
            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new MessageBoxService(null!));

            // Assert
            Assert.Equal("dispatcher", ex.ParamName);
        }

        #endregion

        #region Branch: Non-Headless Dispatch

        [Fact]
        public async Task ShowInfoAsync_WhenNotHeadless_MarshalsThroughDispatcher()
        {
            // Arrange
            var mockDispatcher = new Mock<IUiDispatcher>();
            mockDispatcher
                .Setup(d => d.InvokeAsync(It.IsAny<Func<bool>>()))
                .ReturnsAsync(true);
            var service = new MessageBoxService(mockDispatcher.Object);

            // UiHeadlessFixture enables headless mode for the whole collection, which is the
            // short-circuit that hides this branch. Restore it in the finally so the sibling
            // tests in this collection still see the value the fixture set.
            bool wasHeadless = UiHeadless.IsEnabled;
            UiHeadless.IsEnabled = false;

            try
            {
                // Act
                await service.ShowInfoAsync("message", "caption");
            }
            finally
            {
                UiHeadless.IsEnabled = wasHeadless;
            }

            // Assert
            mockDispatcher.Verify(d => d.InvokeAsync(It.IsAny<Func<bool>>()), Times.Once);
        }

        #endregion
    }
}
