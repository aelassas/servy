using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class UninstallServiceCommandTests : ServiceCommandTestsBase<UninstallServiceCommand, UninstallServiceOptions>
    {
        private Mock<IServiceRepository> _mockRepository = new Mock<IServiceRepository>();

        protected override UninstallServiceCommand CreateCommandInstance()
        {
            return new UninstallServiceCommand(MockServiceManager.Object, _mockRepository.Object);
        }

        protected override UninstallServiceCommand CreateCommandInstanceWithManager(IServiceManager serviceManager) => new UninstallServiceCommand(serviceManager, _mockRepository.Object);

        protected override UninstallServiceOptions CreateValidOptions(string serviceName) => new UninstallServiceOptions { ServiceName = serviceName };

        protected override UninstallServiceOptions CreateEmptyOptions(string serviceName) => new UninstallServiceOptions { ServiceName = serviceName };

        protected override string ExpectedSuccessMessage(string serviceName) => string.Format(Strings.Msg_UninstallSuccess, serviceName);

        protected override string ExpectedGenericActionMessage(string serviceName) => string.Format(Strings.Msg_UninstallServiceAction, serviceName);

        protected override void SetupServiceManagerSuccess(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            mockManager.Setup(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
        }

        protected override void SetupServiceManagerFailure(Mock<IServiceManager> mockManager, string serviceName, string errorMsg)
        {
            mockManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            mockManager.Setup(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure(errorMsg));
        }

        protected override void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            mockManager.Setup(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>())).Throws<TException>();
        }

        [Fact]
        public override async Task Execute_ServiceNotInstalled_ReturnsServiceNotFoundError()
        {
            // Arrange
            const string serviceName = "MissingService";
            var options = CreateValidOptions(serviceName);
            MockServiceManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(false);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ServiceNotFound, result.Message);
        }
    }
}