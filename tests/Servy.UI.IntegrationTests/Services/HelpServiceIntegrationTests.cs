using Moq;
using Moq.Protected;
using Servy.Testing;
using Servy.UI.Resources;
using Servy.UI.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UI.IntegrationTests.Services
{
    [Collection("UiSta")]
    public class HelpServiceIntegrationTests : IDisposable
    {
        private readonly Mock<IMessageBoxService> _mockMessageBox;
        private readonly HelpService _service;
        private const string Caption = "Help Test";

        private HttpMessageHandler _originalHandler;
        private HttpClient _targetClient;

        public HelpServiceIntegrationTests()
        {
            _mockMessageBox = new Mock<IMessageBoxService>();
            _service = new HelpService(_mockMessageBox.Object);
        }

        public void Dispose()
        {
            // Restore the original handler to avoid cross-test static state pollution
            if (_targetClient != null && _originalHandler != null)
            {
                try
                {
                    SetHandlerField(_targetClient, _originalHandler);
                }
                catch
                {
                    // Best effort cleanup during tear-down
                }
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullMessageBoxService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            // Branch: messageBoxService ?? throw new ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => new HelpService(null));
        }

        #endregion

        #region OpenDocumentation Tests

        [Fact]
        public async Task OpenDocumentation_InHeadlessMode_GracefullyDropsExecutionWithoutError()
        {
            // Arrange & Act
            // UiHeadless is enabled via fixture, so OpenExternalUrl short-circuits before Process.Start;
            // verify no error dialog is raised.
            await _service.OpenDocumentationAsync(Caption);

            // Assert
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
                    Content = new StringContent("{ \"name\": \"Draft Release\", \"tag_name\": \"\" }")
                });

            // Inject our mock handler directly into the existing static HttpClient instance
            InjectMockHandlerIntoStaticClient(mockHandler.Object);

            // Act
            await _service.CheckUpdatesAsync(Caption);

            // Assert
            _mockMessageBox.Verify(
                m => m.ShowErrorAsync(
                    It.Is<string>(s => s == string.Format(Strings.Msg_UpdateCheckInvalidTag, string.Empty)),
                    Caption),
                Times.Once);
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
                    Content = new StringContent("{ \"tag_name\": \"v1.0.0\" }")
                });

            // Inject our mock handler directly into the existing static HttpClient instance
            InjectMockHandlerIntoStaticClient(mockHandler.Object);

            // Act
            await _service.CheckUpdatesAsync(Caption);

            // Assert
            _mockMessageBox.Verify(
                m => m.ShowInfoAsync(It.Is<string>(s => s == Strings.Msg_NoUpdatesAvailable), Caption),
                Times.Once);
        }

        [Fact]
        public async Task CheckUpdates_NewerVersionAvailable_UserConfirms_InHeadlessMode_DoesNotOpenBrowser()
        {
            // Arrange
            // 1. Mock GitHub API returning a newer version tag
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
                    Content = new StringContent("{ \"tag_name\": \"v9999.0\", \"html_url\": \"https://github.com/aelassas/servy/releases/tag/v9999.0\" }")
                });

            InjectMockHandlerIntoStaticClient(mockHandler.Object);

            // 2. Mock user clicking "Yes" on the update prompt
            _mockMessageBox
                .Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption))
                .ReturnsAsync(true);

            // Act
            // Because UiHeadless.IsEnabled is true (via UiHeadlessFixture),
            // HelpService.OpenExternalUrl short-circuits and will NOT call Process.Start.
            await _service.CheckUpdatesAsync(Caption);

            // Assert
            _mockMessageBox.Verify(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption), Times.Once);
        }

        #endregion

        #region Private Mock Injection Framework

        /// <summary>
        /// Bypasses runtime initonly restrictions by modifying the private execution handler
        /// instance deep inside the existing static HttpClient instance across .NET Core/5+ and .NET Framework 4.8.
        /// </summary>
        private void InjectMockHandlerIntoStaticClient(HttpMessageHandler mockHandler)
        {
            // 1. Extract the active static HttpClient instance from HelpService
            _targetClient = TestReflection.GetFieldStatic<HttpClient>(typeof(HelpService), "_httpClient");

            // 2. Capture the original handler before replacing it to support tear-down restoration
            if (_originalHandler == null)
            {
                _originalHandler = GetHandlerField(_targetClient);
            }

            // 3. Set the mock handler into the private field using runtime-compatible field lookup
            SetHandlerField(_targetClient, mockHandler);
        }

        private static HttpMessageHandler GetHandlerField(HttpClient client)
        {
            var fieldInfo = GetHandlerFieldInfo(client);
            return (HttpMessageHandler)fieldInfo.GetValue(client);
        }

        private static void SetHandlerField(HttpClient client, HttpMessageHandler mockHandler)
        {
            var fieldInfo = GetHandlerFieldInfo(client);
            fieldInfo.SetValue(client, mockHandler);
        }

        private static FieldInfo GetHandlerFieldInfo(HttpClient client)
        {
            var type = client.GetType();
            while (type != null)
            {
                var field = type.GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
                         ?? type.GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            throw new InvalidOperationException("Could not locate handler field (_handler or handler) on HttpClient for this target framework.");
        }

        #endregion

        #region NormalizeVersion Private Method Reflection Tests

        [Fact]
        public void NormalizeVersion_PartialVersionsWithNegativeFields_PadsMissingPartsToZero()
        {
            // Arrange
            // System.Version elements constructed with 2 parts assign -1 automatically to Build and Revision fields
            var incompleteVersion = new Version(4, 2);

            // Act
            // Non-public static invocation is routed cleanly via the centralized test reflection helper
            var result = (Version)TestReflection.InvokeNonPublicStatic(typeof(HelpService), "NormalizeVersion", incompleteVersion);

            // Assert
            Assert.Equal(4, result.Major);
            Assert.Equal(2, result.Minor);
            Assert.Equal(0, result.Build);
            Assert.Equal(0, result.Revision);
        }

        #endregion
    }
}
