using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    [Collection(ElevationTestCollection.Name)]
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

        /// <summary>
        /// Validates that the constructor throws an <see cref="ArgumentNullException"/> when the required
        /// <see cref="IServiceRepository"/> dependency is missing. The inherited
        /// <c>Constructor_NullServiceManager_ThrowsArgumentNullException</c> always passes a real repository
        /// mock, so the second guard needs its own direct constructor call.
        /// </summary>
        [Fact]
        public void Constructor_NullServiceRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>("serviceRepository",
                () => new UninstallServiceCommand(MockServiceManager.Object, null));
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

        /// <summary>
        /// Pins the routing #6405 introduced: <c>UninstallServiceCommand</c> passes
        /// <c>skipInstalledCheck: true</c>, so the SCM pre-flight is never consulted, and the
        /// repository row is still deleted by the post-success callback. The DB-orphan tolerance
        /// #6374 added lives inside <c>ServiceManager.UninstallServiceAsync</c> and cannot be
        /// expressed here, where <c>IServiceManager</c> is a mock that returns success either way.
        /// </summary>
        [Fact]
        public async Task Execute_SkipsScmPreFlightCheck_DeletesDbRecordAndReturnsSuccess()
        {
            // Arrange
            const string serviceName = "UninstalledService";
            var options = CreateValidOptions(serviceName);

            // The default arrangement is all this test needs: no IsServiceInstalled stub can change
            // what the command does, because the command never asks. SetupServiceManagerSuccess
            // stubs UninstallServiceAsync only, and the pre-flight assertion below is what makes
            // this test behaviourally different from Execute_ValidOptions_ReturnsSuccess.
            SetupServiceManagerSuccess(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            // Verify that the command reports success
            Assert.True(result.IsSuccess);
            Assert.Equal(ExpectedSuccessMessage(serviceName), result.Message);

            // Verify the #6405 routing this test exists for: the SCM pre-flight is skipped, so the
            // command never asks whether the service is installed. Without this the test is
            // behaviourally identical to Execute_ValidOptions_ReturnsSuccess.
            MockServiceManager.Verify(
                sm => sm.IsServiceInstalled(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

            // Verify that the CLI delegated directly to ServiceManager.UninstallServiceAsync
            MockServiceManager.Verify(
                sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify repository cleanup post-uninstall callback was executed by the command wrapper
            _mockRepository.Verify(
                repo => repo.DeleteAsync(serviceName, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Covers the post-success sync catch in <see cref="BaseCommand"/> that closed #3165: when the SCM
        /// uninstall succeeds but the repository cleanup throws, the command still reports success instead
        /// of failing the whole uninstall and stranding a repository row the user can no longer remove.
        /// No other command supplies an <c>onSuccess</c> callback, so this is the only place that arm of
        /// <c>ExecuteServiceOperationAsync</c> can be reached.
        /// </summary>
        [Fact]
        public async Task Execute_RepositoryDeleteAsyncThrows_StillReturnsSuccess()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerSuccess(MockServiceManager, serviceName);
            _mockRepository
                .Setup(repo => repo.DeleteAsync(serviceName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db locked"));

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            // The SCM uninstall succeeded, so the command reports success even though the post-success
            // repository cleanup threw and was swallowed by the catch under test.
            Assert.True(result.IsSuccess);
            Assert.Equal(ExpectedSuccessMessage(serviceName), result.Message);

            // Verify the throwing callback really was invoked - without this the test would also pass if
            // the command stopped calling DeleteAsync at all.
            _mockRepository.Verify(
                repo => repo.DeleteAsync(serviceName, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
