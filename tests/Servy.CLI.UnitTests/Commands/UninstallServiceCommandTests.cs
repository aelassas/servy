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
        public override async Task Execute_ServiceNotInstalled_ReturnsServiceNotFoundError()
        {
            // Arrange
            const string serviceName = "MissingService";
            var options = CreateValidOptions(serviceName);
            MockServiceManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(false);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Strings.Msg_ServiceNotFound, result.Message);
        }

        [Fact]
        public async Task Execute_ServiceNotInstalledInScmButExistsInRepository_DeletesDbRecordAndReturnsSuccess()
        {
            // Arrange
            const string serviceName = "OrphanedDbService";
            var options = CreateValidOptions(serviceName);
            var orphanedService = new ServiceDto { Name = serviceName };

            // 1. SCM reports that the service is NOT installed
            MockServiceManager
                .Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>()))
                .Returns(false);

            // 2. Repository finds the leftover record in servy.db
            _mockRepository
                .Setup(repo => repo.GetByNameAsync(serviceName, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orphanedService);

            // 3. Setup deletion on repository
            _mockRepository
                .Setup(repo => repo.DeleteAsync(serviceName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(1));

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            // Verify that the command reports success
            Assert.True(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_UninstallServiceNotInstalled, serviceName), result.Message);

            // Verify that the database cleanup was actually invoked
            _mockRepository.Verify(
                repo => repo.DeleteAsync(serviceName, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify that SCM uninstall operation was never attempted since it wasn't registered in SCM
            MockServiceManager.Verify(
                sm => sm.UninstallServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}