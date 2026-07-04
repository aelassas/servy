using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.Services;
using System.Threading;

namespace Servy.CLI.UnitTests.Commands
{
    public class UninstallServiceCommandTests : ServiceCommandTestsBase<UninstallServiceCommand, UninstallServiceOptions>
    {
        private Mock<IServiceRepository> _mockRepository;

        protected override UninstallServiceCommand CreateCommandInstance()
        {
            _mockRepository = new Mock<IServiceRepository>();
            return new UninstallServiceCommand(MockServiceManager.Object, _mockRepository.Object);
        }

        protected override UninstallServiceOptions CreateValidOptions(string serviceName) => new UninstallServiceOptions { ServiceName = serviceName };

        protected override UninstallServiceOptions CreateEmptyOptions() => new UninstallServiceOptions { ServiceName = "" };

        protected override string ExpectedSuccessMessage(string serviceName) => $"Service '{serviceName}' uninstalled successfully.";

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
    }
}