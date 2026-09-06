using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.Core.Resources;
using Servy.Core.Services;
using CliStrings = Servy.CLI.Resources.Strings;

namespace Servy.CLI.UnitTests.Commands
{
    /// <summary>
    /// Shared base for CLI service command tests: builds the IServiceManager mock, creates the command under test,
    /// and runs the standard test suite shared across service CLI operations.
    /// </summary>
    /// <typeparam name="TCommand">The command type under test.</typeparam>
    /// <typeparam name="TOptions">The CLI options type accepted by the command.</typeparam>
    public abstract class ServiceCommandTestsBase<TCommand, TOptions> : IDisposable
        where TOptions : class, new()
    {
        /// <summary>
        /// Gets the mock service manager used to simulate Windows Service Control Manager (SCM) behavior.
        /// </summary>
        protected readonly Mock<IServiceManager> MockServiceManager;

        /// <summary>
        /// Gets the command instance under test.
        /// </summary>
        protected readonly TCommand Command;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommandTestsBase{TCommand, TOptions}"/> class.
        /// Sets up elevation bypass, mock service manager, and instantiates the command under test.
        /// </summary>
        protected ServiceCommandTestsBase()
        {
            BaseCommand.BypassElevationCheck = true;
            MockServiceManager = new Mock<IServiceManager>();
            Command = CreateCommandInstance();
        }

        #region Extensibility Template Hooks

        /// <summary>
        /// When overridden in a derived class, instantiates the command under test using the default mock manager.
        /// </summary>
        /// <returns>An instance of <typeparamref name="TCommand"/>.</returns>
        protected abstract TCommand CreateCommandInstance();

        /// <summary>
        /// When overridden in a derived class, instantiates the command under test with a specific service manager instance (used to test constructor guards).
        /// </summary>
        /// <param name="serviceManager">The service manager instance to pass to the constructor (may be null).</param>
        /// <returns>An instance of <typeparamref name="TCommand"/>.</returns>
        protected abstract TCommand CreateCommandInstanceWithManager(IServiceManager? serviceManager);

        /// <summary>
        /// When overridden in a derived class, creates a valid options instance containing the specified service name.
        /// </summary>
        /// <param name="serviceName">The service name to set on the options instance.</param>
        /// <returns>A valid options instance of type <typeparamref name="TOptions"/>.</returns>
        protected abstract TOptions CreateValidOptions(string serviceName);

        /// <summary>
        /// When overridden in a derived class, creates an options instance containing the given (invalid) service name to test input validation guards.
        /// </summary>
        /// <param name="serviceName">The malformed service name to test (null, empty, or whitespace-only).</param>
        /// <returns>An options instance of type <typeparamref name="TOptions"/>.</returns>
        protected abstract TOptions CreateEmptyOptions(string? serviceName);

        /// <summary>
        /// When overridden in a derived class, returns the expected success message string for the command.
        /// </summary>
        /// <param name="serviceName">The target service name.</param>
        /// <returns>The expected success message.</returns>
        protected abstract string ExpectedSuccessMessage(string serviceName);

        /// <summary>
        /// When overridden in a derived class, returns the expected error message fragment included in generic exception responses.
        /// </summary>
        /// <param name="serviceName">The target service name.</param>
        /// <returns>The expected error message fragment.</returns>
        protected abstract string ExpectedGenericActionMessage(string serviceName);

        /// <summary>
        /// When overridden in a derived class, returns the command name the command under test passes to
        /// <c>BaseCommand.ExecuteWithHandling</c>/<c>ExecuteWithHandlingAsync</c> (e.g. "start", "stop"),
        /// which is the value formatted into <see cref="CliStrings.Msg_CommandCancelled"/>.
        /// </summary>
        protected abstract string ExpectedCommandName { get; }

        /// <summary>
        /// When overridden in a derived class, returns the expected error message when the target service is not installed.
        /// Defaults to <see cref="Strings.Msg_ServiceNotFound"/>.
        /// </summary>
        /// <param name="serviceName">The target service name.</param>
        /// <returns>The expected service not found error message.</returns>
        protected virtual string ExpectedServiceNotFoundMessage(string serviceName)
        {
            return Strings.Msg_ServiceNotFound;
        }

        /// <summary>
        /// When overridden in a derived class, configures the mock service manager to return success for the specified service name.
        /// </summary>
        /// <param name="mockManager">The mock service manager.</param>
        /// <param name="serviceName">The target service name.</param>
        protected abstract void SetupServiceManagerSuccess(Mock<IServiceManager> mockManager, string serviceName);

        /// <summary>
        /// When overridden in a derived class, configures the mock service manager to return a failure result with the given error message.
        /// </summary>
        /// <param name="mockManager">The mock service manager.</param>
        /// <param name="serviceName">The target service name.</param>
        /// <param name="errorMsg">The error message payload to return from the mock.</param>
        protected abstract void SetupServiceManagerFailure(Mock<IServiceManager> mockManager, string serviceName, string errorMsg);

        /// <summary>
        /// When overridden in a derived class, configures the mock service manager to throw an exception of type <typeparamref name="TException"/>.
        /// </summary>
        /// <typeparam name="TException">The exception type to throw from the mock.</typeparam>
        /// <param name="mockManager">The mock service manager.</param>
        /// <param name="serviceName">The target service name.</param>
        protected abstract void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName) where TException : Exception, new();

        /// <summary>
        /// When overridden in a derived class, configures the mock service manager to simulate an uninstalled or missing service.
        /// Defaults to setting up <see cref="IServiceManager.IsServiceInstalled"/> to return <c>false</c>.
        /// </summary>
        /// <param name="mockManager">The mock service manager.</param>
        /// <param name="serviceName">The target service name.</param>
        protected virtual void SetupServiceNotInstalled(Mock<IServiceManager> mockManager, string serviceName)
        {
            mockManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(false);
        }

        /// <summary>
        /// Executes the command under test with the provided options.
        /// Invokes the asynchronous <c>ExecuteAsync</c> entry point via dynamic dispatch, since the commands do not share an interface.
        /// A command exposing a synchronous <c>Execute</c> instead overrides this method (see <c>ServiceStatusCommandTests</c>).
        /// </summary>
        /// <param name="command">The command instance to execute.</param>
        /// <param name="options">The options instance to pass to the command.</param>
        /// <returns>The resulting <see cref="CommandResult"/>.</returns>
        protected virtual async Task<CommandResult> ExecuteCommandAsync(TCommand command, TOptions options)
        {
            dynamic cmd = command!;

            return await cmd.ExecuteAsync(options, TestContext.Current.CancellationToken);
        }

        #endregion

        #region Base Core Test Suite Skeleton

        /// <summary>
        /// Validates that the constructor throws an <see cref="ArgumentNullException"/> when the required <see cref="IServiceManager"/> dependency is missing.
        /// </summary>
        [Fact]
        public virtual void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceManager? nullManager = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("serviceManager", () => CreateCommandInstanceWithManager(nullManager));
        }

        /// <summary>
        /// Validates that passing valid options and setting up a successful mock manager call returns a successful result with the expected success message.
        /// </summary>
        [Fact]
        public virtual async Task Execute_ValidOptions_ReturnsSuccess()
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
        }

        /// <summary>
        /// Validates that passing null, empty, or whitespace service names returns a validation failure result indicating that a service name is required.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public virtual async Task Execute_EmptyServiceName_ReturnsFailure(string? invalidServiceName)
        {
            // Arrange
            var options = CreateEmptyOptions(invalidServiceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Strings.Msg_ServiceNameRequired, result.Message);
        }

        /// <summary>
        /// Validates that operational failures returned by the service manager are propagated as failure command results.
        /// </summary>
        [Fact]
        public virtual async Task Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            var expectedFailureText = $"Failed to perform operation on {serviceName}.";
            SetupServiceManagerFailure(MockServiceManager, serviceName, expectedFailureText);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(expectedFailureText, result.Message);
        }

        /// <summary>
        /// Validates that an <see cref="UnauthorizedAccessException"/> thrown by the service manager is caught and returns the admin-privileges failure result for this command's verb.
        /// </summary>
        [Fact]
        public virtual async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerException<UnauthorizedAccessException>(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            // Assert the resource rather than the first two English words of its value: a
            // reworded or localised Msg_AdminPrivilegesRequired must not fail this test, and
            // the substring form pinned nothing about the {0} verb argument.
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(CliStrings.Msg_AdminPrivilegesRequired, ExpectedCommandName), result.Message);
        }

        /// <summary>
        /// Validates that unexpected runtime exceptions thrown by the service manager are caught and translated into generic error messages.
        /// </summary>
        [Fact]
        public virtual async Task Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerException<Exception>(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(ExpectedGenericActionMessage(serviceName), result.Message);
        }

        /// <summary>
        /// Validates that a cancelled operation is reported as a clean cancellation result carrying the command name,
        /// rather than being routed through the generic exception handler.
        /// </summary>
        [Fact]
        public virtual async Task Execute_OperationCanceled_ReturnsCancelledResult()
        {
            // Arrange
            const string serviceName = "TestService";
            var options = CreateValidOptions(serviceName);
            SetupServiceManagerException<OperationCanceledException>(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(CliStrings.Msg_CommandCancelled, ExpectedCommandName), result.Message);
        }

        /// <summary>
        /// Validates that attempting to execute an operation on an uninstalled service returns a "service not found" failure result.
        /// </summary>
        [Fact]
        public virtual async Task Execute_ServiceNotInstalled_ReturnsServiceNotFoundError()
        {
            // Arrange
            const string serviceName = "MissingService";
            var options = CreateValidOptions(serviceName);
            SetupServiceNotInstalled(MockServiceManager, serviceName);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ExpectedServiceNotFoundMessage(serviceName), result.Message);
        }

        /// <summary>
        /// Runs the Disabled-startup-type refusal scenario: an installed service whose startup type is
        /// Disabled must be refused with <see cref="CliStrings.Msg_ServiceDisabledError"/>.
        /// Only the commands that perform that pre-check call it, which is why it is a helper here
        /// rather than a [Fact] every derived class would inherit.
        /// </summary>
        protected async Task AssertDisabledStartupTypeIsRefusedAsync()
        {
            // Arrange
            const string serviceName = "DisabledService";
            var options = CreateValidOptions(serviceName);
            MockServiceManager.Setup(sm => sm.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            MockServiceManager.Setup(sm => sm.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Disabled);

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(CliStrings.Msg_ServiceDisabledError, result.Message);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Resets process-wide static state altered during test execution.
        /// </summary>
        public virtual void Dispose()
        {
            BaseCommand.BypassElevationCheck = false;
        }

        #endregion
    }
}
