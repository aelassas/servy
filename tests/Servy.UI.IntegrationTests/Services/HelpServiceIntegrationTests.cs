using Moq;
using Moq.Protected;
using Servy.UI.Services;
using System.Net;
using System.Reflection;

namespace Servy.UI.IntegrationTests.Services
{
    public class HelpServiceIntegrationTests
    {
        private readonly Mock<IMessageBoxService> _mockMessageBox;
        private readonly HelpService _service;
        private const string Caption = "Help Test";

        public HelpServiceIntegrationTests()
        {
            _mockMessageBox = new Mock<IMessageBoxService>();
            _service = new HelpService(_mockMessageBox.Object);
            UiHeadless.IsEnabled = true;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullMessageBoxService_ThrowsArgumentNullException()
        {
            // Branch: messageBoxService ?? throw new ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => new HelpService(null!));
        }

        #endregion

        #region OpenDocumentation Tests

        [Fact]
        public async Task OpenDocumentation_InHeadlessMode_GracefullyDropsExecutionWithoutError()
        {
            // Note: Process.Start(psi) is hard to mock directly without a wrapper.
            // In a CI environment, this branch typically "hands off" or succeeds silently.
            await _service.OpenDocumentationAsync(Caption);

            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region OpenAboutDialog Tests

        [Fact]
        public async Task OpenAboutDialog_InvokesMessageBox()
        {
            // Arrange
            const string aboutText = "Servy v1.0";

            // Act
            await _service.OpenAboutDialogAsync(aboutText, Caption);

            // Assert
            _mockMessageBox.Verify(m => m.ShowInfoAsync(aboutText, Caption), Times.Once);
        }

        #endregion

        #region CheckUpdates Tests

        [Fact]
        public async Task CheckUpdates_NoTagNameInJson_ShowsNoUpdates()
        {
            // Arrange
            // Branch: if (string.IsNullOrEmpty(tagName))
            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{ 'name': 'Draft Release', 'tag_name': '' }")
                });

            // Inject our mock handler directly into the existing static HttpClient instance
            InjectMockHandlerIntoStaticClient(mockHandler.Object);

            // Act
            await _service.CheckUpdatesAsync(Caption);

            // Assert
            _mockMessageBox.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), Caption), Times.Once);
        }

        [Fact]
        public async Task CheckUpdates_VersionIsOlder_ShowsNoUpdates()
        {
            // Arrange
            // Branch: else { await _messageBoxService.ShowInfoAsync(Strings.Msg_NoUpdatesAvailable...) }
            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{ 'tag_name': 'v1.0.0' }")
                });

            // Inject our mock handler directly into the existing static HttpClient instance
            InjectMockHandlerIntoStaticClient(mockHandler.Object);

            // Act
            await _service.CheckUpdatesAsync(Caption);

            // Assert
            _mockMessageBox.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), Caption), Times.Once);
        }

        #endregion

        #region Private Mock Injection Framework

        /// <summary>
        /// Bypasses runtime initonly restrictions by modifying the private execution handler 
        /// instance deep inside the existing static HttpClient instance.
        /// </summary>
        private void InjectMockHandlerIntoStaticClient(HttpMessageHandler mockHandler)
        {
            // 1. Extract the active static HttpClient instance from HelpService
            var httpClientField = typeof(HelpService).GetField("_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
            var clientInstance = httpClientField?.GetValue(null) as HttpClient;

            if (clientInstance == null) return;

            // 2. HttpClient inherits '_handler' from HttpMessageInvoker in modern .NET runtimes
            var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic);

            // Fallback for older .NET Framework layouts if encountered
            if (handlerField == null)
            {
                handlerField = typeof(HttpClient).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            // 3. Force-write the mock handler into the instance field context cleanly
            handlerField?.SetValue(clientInstance, mockHandler);
        }

        #endregion

        #region NormalizeVersion Private Method Reflection Tests

        [Fact]
        public void NormalizeVersion_PartialVersionsWithNegativeFields_PadsMissingPartsToZero()
        {
            // Arrange
            var method = typeof(HelpService).GetMethod("NormalizeVersion", BindingFlags.Static | BindingFlags.NonPublic);

            // System.Version elements constructed with 2 parts assign -1 automatically to Build and Revision fields
            var incompleteVersion = new Version(4, 2);

            // Act
            var result = (Version)method!.Invoke(null, new object[] { incompleteVersion })!;

            // Assert
            Assert.Equal(4, result.Major);
            Assert.Equal(2, result.Minor);
            Assert.Equal(0, result.Build);
            Assert.Equal(0, result.Revision);
        }

        #endregion
    }
}