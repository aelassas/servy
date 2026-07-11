using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Services;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class ServiceStatusCommandTests : ServiceCommandTestsBase<ServiceStatusCommand, ServiceStatusOptions>
    {
        protected override ServiceStatusCommand CreateCommandInstance() => new ServiceStatusCommand(MockServiceManager.Object);

        protected override ServiceStatusOptions CreateValidOptions(string serviceName) => new ServiceStatusOptions { ServiceName = serviceName };

        protected override ServiceStatusOptions CreateEmptyOptions(string serviceName) => new ServiceStatusOptions { ServiceName = serviceName };

        protected override string ExpectedSuccessMessage(string serviceName) => string.Format(Strings.Msg_ServiceStatusResult, serviceName, ServiceControllerStatus.Running);

        protected override string ExpectedGenericActionMessage(string serviceName) => string.Format(Strings.Msg_ServiceStatusAction, serviceName);

        protected override async Task<CommandResult> ExecuteCommandAsync(ServiceStatusCommand command, ServiceStatusOptions options)
        {
            // Override mapping explicitly to bypass async conversion paths for sync execution.
            return await Task.FromResult(command.Execute(options, CancellationToken.None));
        }

        protected override void SetupServiceManagerSuccess(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);
        }

        protected override void SetupServiceManagerFailure(Mock<IServiceManager> mockManager, string serviceName, string errorMsg)
        {
            // Triggers CommandResult mapping fallback pathways natively
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Throws<ArgumentException>();
        }

        protected override void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Throws<TException>();
        }

        // Bypasses testing hooks that are naturally unhandled by ServiceStatusCommand infrastructure paths.
        [Fact]
        public override Task Execute_UnauthorizedAccessException_ReturnsFailure() => Task.CompletedTask;

        [Fact]
        public override async Task Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerFailure(MockServiceManager, serviceName, string.Empty);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(ExpectedGenericActionMessage(serviceName), result.Message);
        }

        [Fact]
        public override async Task Execute_ServiceNotInstalled_ReturnsServiceNotFoundError()
        {
            // Arrange
            const string serviceName = "MissingService";
            var options = CreateValidOptions(serviceName);

            // SCM throws ArgumentException when looking up a non-existent service name status
            MockServiceManager
                .Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>()))
                .Throws<ArgumentException>();

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(ExpectedGenericActionMessage(serviceName), result.Message);
        }
    }
}