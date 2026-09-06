using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Services;

namespace Servy.CLI.UnitTests.Commands
{
    [Collection("SequentialElevationTests")]
    public class UninstallServiceCommandTests : ServiceCommandTestsBase<UninstallServiceCommand, UninstallServiceOptions>
    {
        private Mock<IServiceRepository> _mockRepository = new Mock<IServiceRepository>();

        protected override UninstallServiceCommand CreateCommandInstance()
        {
            return new UninstallServiceCommand(MockServiceManager.Object, _mockRepository.Object);
        }

        protected override UninstallServiceCommand CreateCommandInstanceWithManager(IServiceManager? serviceManager) => new UninstallServiceCommand(serviceManager!, _mockRepository.Object);

        protected override UninstallServiceOptions CreateValidOptions(string serviceName) => new UninstallServiceOptions { ServiceName = serviceName };

        protected override UninstallServiceOptions CreateEmptyOptions(string? serviceName) => new UninstallServiceOptions { ServiceName = serviceName };

        protected override string ExpectedSuccessMessage(string serviceName) => string.Format(Strings.Msg_UninstallSuccess, serviceName);

        protected override string ExpectedGenericActionMessage(string serviceName) => string.Format(Strings.Msg_UninstallServiceAction, serviceName);

        protected override string ExpectedCommandName => "uninstall";

        protected override void SetupServiceManagerSuccess(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
        }

        protected override void SetupServiceManagerFailure(Mock<IServiceManager> mockManager, string serviceName, string errorMsg)
        {
            mockManager.Setup(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure(errorMsg));
        }

        protected override void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>())).Throws<TException>();
        }

        /// <summary>
        /// Uninstall passes <c>skipInstalledCheck: true</c>, so the not-installed outcome comes from
        /// <see cref="IServiceManager.UninstallServiceAsync"/> rather than from the pre-flight check.
        /// </summary>
        protected override void SetupServiceNotInstalled(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager
                    .Setup(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(OperationResult.Failure(Core.Resources.Strings.Msg_ServiceNotFound));
        }

        [Fact]
        public override async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerSuccess(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ExpectedSuccessMessage(serviceName), result.Message);
            _mockRepository.Verify(r => r.DeleteAsync(serviceName, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Execute_ServiceNotInstalledInScmButExistsInRepository_DeletesDbRecordAndReturnsSuccess()
        {
            // Arrange
            const string serviceName = "OrphanedDbService";
            var options = CreateValidOptions(serviceName);

            // Uninstall skips the IsServiceInstalled pre-flight (skipInstalledCheck: true), so only
            // UninstallServiceAsync is mocked, returning Success (simulating ServiceManager's internal orphan cleanup #6374).
            SetupServiceManagerSuccess(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            // Verify that the command reports success
            Assert.True(result.IsSuccess);
            Assert.Equal(ExpectedSuccessMessage(serviceName), result.Message);

            // Verify that the CLI delegated directly to ServiceManager.UninstallServiceAsync
            MockServiceManager.Verify(
                sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify repository cleanup post-uninstall callback was executed by the command wrapper
            _mockRepository.Verify(
                repo => repo.DeleteAsync(serviceName, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
