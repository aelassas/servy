using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Services;
using System.ServiceProcess;

namespace Servy.CLI.UnitTests.Commands
{
    public class ServiceStatusCommandTests : ServiceCommandTestsBase<ServiceStatusCommand, ServiceStatusOptions>
    {
        protected override ServiceStatusCommand CreateCommandInstance() => new ServiceStatusCommand(MockServiceManager.Object);

        protected override ServiceStatusCommand CreateCommandInstanceWithManager(IServiceManager? serviceManager) => new ServiceStatusCommand(serviceManager!);

        protected override ServiceStatusOptions CreateValidOptions(string serviceName) => new ServiceStatusOptions { ServiceName = serviceName };

        protected override ServiceStatusOptions CreateEmptyOptions(string? serviceName) => new ServiceStatusOptions { ServiceName = serviceName };

        protected override string ExpectedSuccessMessage(string serviceName) => string.Format(Strings.Msg_ServiceStatusResult, serviceName, ServiceControllerStatus.Running);

        protected override string ExpectedGenericActionMessage(string serviceName) => string.Format(Strings.Msg_ServiceStatusAction, serviceName);

        protected override string ExpectedCommandName => "status";

        protected override async Task<CommandResult> ExecuteCommandAsync(ServiceStatusCommand command, ServiceStatusOptions options)
        {
            // Override mapping explicitly to bypass async conversion paths for sync execution.
            return await Task.FromResult(command.Execute(options, TestContext.Current.CancellationToken));
        }

        protected override void SetupServiceManagerSuccess(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);
        }

        protected override void SetupServiceManagerFailure(Mock<IServiceManager> mockManager, string serviceName, string errorMsg)
        {
            // Triggers CommandResult mapping fallback pathways natively.
            // Note: errorMsg parameter is unused because ServiceStatusCommand catches SCM exceptions and maps them via Msg_ServiceStatusAction.
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("SCM operational failure"));
        }

        protected override void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>())).Throws<TException>();
        }

        [Fact]
        public override async Task Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerFailure(MockServiceManager, serviceName, string.Empty);

            var action = ExpectedGenericActionMessage(serviceName);
            var expectedMessage = string.Format(Strings.Msg_CommandFailedTemplate, action, "SCM operational failure") +
                $"{Environment.NewLine}{string.Format(Strings.Msg_SuggestionTemplate, Strings.Msg_ServiceStatusSuggestion)}";

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(expectedMessage, result.Message);
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

            var ex = new ArgumentException("Service does not exist");

            // SCM throws ArgumentException when looking up a non-existent service name status
            MockServiceManager
                .Setup(sm => sm.GetServiceStatus(serviceName, It.IsAny<CancellationToken>()))
                .Throws(ex);

            var action = ExpectedGenericActionMessage(serviceName);
            var expectedMessage = string.Format(Strings.Msg_CommandFailedTemplate, action, ex.Message) +
                $"{Environment.NewLine}{string.Format(Strings.Msg_SuggestionTemplate, Strings.Msg_ServiceStatusSuggestion)}";

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(expectedMessage, result.Message);
        }

        /// <summary>
        /// Pins the expected status token vocabulary against all native ServiceControllerStatus enum member names
        /// plus the NotInstalled fallback literal, validating that the command options HelpText documentation stays synchronized.
        /// </summary>
        [Fact]
        public void ServiceStatusVocabulary_MatchesServiceControllerStatusAndNotInstalled()
        {
            // Arrange
            var expectedTokens = Enum.GetNames(typeof(ServiceControllerStatus))
                .Concat(new[] { nameof(ServiceStatus.NotInstalled) })
                .ToList();

            var verbAttr = typeof(ServiceStatusOptions)
                .GetCustomAttributes(typeof(CommandLine.VerbAttribute), false)
                .FirstOrDefault() as CommandLine.VerbAttribute;

            // Act
            var verbHelpText = verbAttr?.HelpText ?? string.Empty;

            // Assert
            Assert.NotNull(verbAttr);
            foreach (var token in expectedTokens)
            {
                Assert.Contains(token, verbHelpText);
            }
        }
    }
}
