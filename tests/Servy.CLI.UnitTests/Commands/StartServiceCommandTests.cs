using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Services;
using System.Threading;

namespace Servy.CLI.UnitTests.Commands
{
    public class StartServiceCommandTests : ServiceCommandTestsBase<StartServiceCommand, StartServiceOptions>
    {
        protected override StartServiceCommand CreateCommandInstance() => new StartServiceCommand(MockServiceManager.Object);

        protected override StartServiceOptions CreateValidOptions(string serviceName) => new StartServiceOptions { ServiceName = serviceName };

        protected override StartServiceOptions CreateEmptyOptions() => new StartServiceOptions { ServiceName = "" };

        protected override string ExpectedSuccessMessage(string serviceName) => string.Format(Strings.Msg_StartSuccess, serviceName);

        protected override string ExpectedGenericActionMessage(string serviceName) => string.Format(Strings.Msg_StartServiceAction, serviceName);

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
    }
}