using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    [Collection(ElevationTestCollection.Name)]
    public class StartServiceCommandTests : ServiceCommandTestsBase<StartServiceCommand, StartServiceOptions>
    {
        protected override StartServiceCommand CreateCommandInstance() => new StartServiceCommand(MockServiceManager.Object);

        protected override StartServiceCommand CreateCommandInstanceWithManager(IServiceManager serviceManager) => new StartServiceCommand(serviceManager);

        protected override StartServiceOptions CreateValidOptions(string serviceName) => new StartServiceOptions { ServiceName = serviceName };

        protected override StartServiceOptions CreateEmptyOptions(string serviceName) => new StartServiceOptions { ServiceName = serviceName };

        protected override string ExpectedSuccessMessage(string serviceName) => string.Format(Strings.Msg_StartSuccess, serviceName);

        protected override string ExpectedGenericActionMessage(string serviceName) => string.Format(Strings.Msg_StartServiceAction, serviceName);

        protected override string ExpectedSuggestion => Strings.Msg_StartServiceSuggestion;

        protected override string ExpectedCommandName => "start";

        protected override void SetupServiceManagerSuccess(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            mockManager.Setup(sm => sm.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            mockManager.Setup(sm => sm.StartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
        }

        protected override void SetupServiceManagerFailure(Mock<IServiceManager> mockManager, string serviceName, string errorMsg)
        {
            mockManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            mockManager.Setup(sm => sm.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            mockManager.Setup(sm => sm.StartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure(errorMsg));
        }

        protected override void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            mockManager.Setup(sm => sm.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            mockManager.Setup(sm => sm.StartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>())).Throws<TException>();
        }

        [Fact]
        public Task Execute_ServiceIsDisabled_ReturnsServiceDisabledError() => AssertDisabledStartupTypeIsRefusedAsync();

        [Fact]
        public async Task Execute_ValidOptions_AsksTheManagerToLogTheSuccessfulStart()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            const string serviceName = "TestService";
            SetupServiceManagerSuccess(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, CreateValidOptions(serviceName), cts.Token);

            // Assert
            Assert.True(result.IsSuccess);
            MockServiceManager.Verify(
                sm => sm.StartServiceAsync(serviceName, true, It.IsAny<CancellationToken>()),
                Times.Once);

            // Exact token, not It.IsAny: a hand-over that passes default matches nothing. The installed check
            // (Start, Stop and Restart; Uninstall skips it) and the disabled pre-check (Start and Restart) are
            // BaseCommand's, so they are pinned here once.
            var token = cts.Token;
            MockServiceManager.Verify(sm => sm.IsServiceInstalled(serviceName, token), Times.Once);
            MockServiceManager.Verify(sm => sm.GetServiceStartupType(serviceName, token), Times.Once);
            MockServiceManager.Verify(sm => sm.StartServiceAsync(serviceName, true, token), Times.Once);

            cts.Dispose();
        }
    }
}
