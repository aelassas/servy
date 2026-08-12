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

        protected override ServiceStatusCommand CreateCommandInstanceWithManager(IServiceManager serviceManager) => new ServiceStatusCommand(serviceManager);

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
            // Triggers CommandResult mapping fallback pathways natively.
            // Note: errorMsg parameter is unused because ServiceStatusCommand catches SCM exceptions and maps them to Msg_ServiceStatusAction.
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("SCM operational failure"));
        }

        protected override void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Throws<TException>();
        }

        [Fact]
        public override async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            const string serviceName = "RestrictedService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerException<UnauthorizedAccessException>(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_AdminPrivilegesRequired, "status"), result.Message);
        }

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
            Assert.False(result.IsSuccess);
            // Assert.Contains is used instead of Assert.Equal because BaseCommand.HandleException wraps
            // ExpectedGenericActionMessage within Msg_CommandFailedTemplate ("Failed to {0}: {1}") and appends suggestion text.
            Assert.Contains(ExpectedGenericActionMessage(serviceName), result.Message);
        }

        /// <summary>
        /// Validates that attempting to query status on an uninstalled service returns a failure result.
        /// Note: The SCM signals an unknown service name with an ArgumentException in GetServiceStatus, which the status
        /// command maps to its generic action message (Msg_ServiceStatusAction) rather than Msg_ServiceNotFound.
        /// </summary>
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
            Assert.False(result.IsSuccess);
            // Assert.Contains is used instead of Assert.Equal because BaseCommand.HandleException wraps
            // ExpectedGenericActionMessage within Msg_CommandFailedTemplate ("Failed to {0}: {1}") and appends suggestion text.
            Assert.Contains(ExpectedGenericActionMessage(serviceName), result.Message);
        }
    }
}
