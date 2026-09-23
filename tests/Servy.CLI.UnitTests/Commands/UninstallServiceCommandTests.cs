using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    [Collection(ElevationTestCollection.Name)]
    public class UninstallServiceCommandTests : ServiceCommandTestsBase<UninstallServiceCommand, UninstallServiceOptions>
    {
        protected override UninstallServiceCommand CreateCommandInstance()
        {
            return new UninstallServiceCommand(MockServiceManager.Object);
        }

        protected override UninstallServiceCommand CreateCommandInstanceWithManager(IServiceManager serviceManager) => new UninstallServiceCommand(serviceManager);

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
        /// Pins the deduplication #6408 landed: repository row deletion is owned by
        /// <see cref="IServiceManager.UninstallServiceAsync"/>, which deletes it on every success
        /// path it has. The command used to delete it a second time from a post-success callback,
        /// and the <c>IServiceRepository</c> dependency existed only to make that second call
        /// possible, so its absence from the constructor is what keeps the two copies from coming
        /// back.
        /// </summary>
        [Fact]
        public void Constructor_TakesServiceManagerOnly_NoRepositoryDependency()
        {
            // Arrange
            var constructor = Assert.Single(typeof(UninstallServiceCommand).GetConstructors());

            // Act
            var parameterTypes = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

            // Assert
            Assert.Equal(new[] { typeof(IServiceManager) }, parameterTypes);
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
            MockServiceManager.Verify(sm => sm.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Pins the routing #6405 introduced: <c>UninstallServiceCommand</c> passes
        /// <c>skipInstalledCheck: true</c>, so the SCM pre-flight is never consulted. The DB-orphan
        /// tolerance #6374 added, and the row deletion itself, both live inside
        /// <c>ServiceManager.UninstallServiceAsync</c> and cannot be expressed here, where
        /// <c>IServiceManager</c> is a mock that returns success either way.
        /// </summary>
        [Fact]
        public async Task Execute_SkipsScmPreFlightCheck_ReturnsSuccess()
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
        }

        /// <summary>
        /// Covers the post-success sync catch in <see cref="BaseCommand"/> that closed #3165: when the
        /// operation succeeds but the post-success callback throws, the command still reports success
        /// instead of failing the whole operation.
        /// <para>
        /// Until #6408 this was reached through <c>UninstallServiceCommand</c>, the only command that
        /// supplied an <c>onSuccess</c> callback. That callback was a duplicate of the row deletion
        /// <c>ServiceManager.UninstallServiceAsync</c> already performs and is gone, so no production
        /// command supplies one today. The arm is still part of <see cref="BaseCommand"/>'s contract,
        /// so it is exercised here through a command defined for that purpose rather than left
        /// uncovered.
        /// </para>
        /// </summary>
        [Fact]
        public async Task ExecuteServiceOperation_OnSuccessCallbackThrows_StillReturnsSuccess()
        {
            // Arrange
            const string serviceName = "TestService";
            var probe = new OnSuccessProbeCommand();
            SetupServiceManagerSuccess(MockServiceManager, serviceName);

            // Act
            var result = await probe.RunAsync(
                MockServiceManager.Object,
                serviceName,
                () => throw new InvalidOperationException("db locked"));

            // Assert
            // The operation succeeded, so the command reports success even though the post-success
            // callback threw and was swallowed by the catch under test.
            Assert.True(result.IsSuccess);
            Assert.Equal(OnSuccessProbeCommand.SuccessMessage, result.Message);

            // Verify the throwing callback really was invoked - without this the test would also pass
            // if BaseCommand stopped invoking onSuccess at all.
            Assert.Equal(1, probe.CallbackInvocations);
        }

        /// <summary>
        /// A command that exists only to reach <c>BaseCommand.ExecuteServiceOperationAsync</c>'s
        /// <c>onSuccess</c> arm, which has no production caller since #6408.
        /// </summary>
        private sealed class OnSuccessProbeCommand : BaseCommand
        {
            public const string SuccessMessage = "probe succeeded";

            public int CallbackInvocations { get; private set; }

            public Task<CommandResult> RunAsync(IServiceManager serviceManager, string serviceName, Action onSuccess)
            {
                return ExecuteServiceOperationAsync(
                    commandName: "probe",
                    action: "probe action",
                    suggestion: "probe suggestion",
                    serviceName: serviceName,
                    serviceManager: serviceManager,
                    operation: (token) => Task.FromResult(OperationResult.Success()),
                    successMessageFormatter: (name) => SuccessMessage,
                    onSuccess: (token) =>
                    {
                        CallbackInvocations++;
                        onSuccess();
                        return Task.CompletedTask;
                    },
                    skipInstalledCheck: true,
                    cancellationToken: CancellationToken.None);
            }
        }
    }
}
