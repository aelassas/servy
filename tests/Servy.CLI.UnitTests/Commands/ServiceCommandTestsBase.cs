using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.Core.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    /// <summary>
    /// Shared base for CLI service command tests: builds the IServiceManager mock, creates the command under test,
    /// and runs the standard test suite shared across service CLI operations.
    /// </summary>
    /// <typeparam name="TCommand">The command type under test.</typeparam>
    /// <typeparam name="TOptions">The CLI options type accepted by the command.</typeparam>
    public abstract class ServiceCommandTestsBase<TCommand, TOptions>
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
        /// Sets up the mock service manager and instantiates the command under test.
        /// </summary>
        protected ServiceCommandTestsBase()
        {
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
        protected abstract TCommand CreateCommandInstanceWithManager(IServiceManager serviceManager);

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
        protected abstract TOptions CreateEmptyOptions(string serviceName);

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
        /// Executes the command under test with the provided options.
        /// Checks for an asynchronous <c>ExecuteAsync</c> method via dynamic dispatch, falling back to synchronous <c>Execute</c>.
        /// </summary>
        /// <param name="command">The command instance to execute.</param>
        /// <param name="options">The options instance to pass to the command.</param>
        /// <returns>The resulting <see cref="CommandResult"/>.</returns>
        protected virtual async Task<CommandResult> ExecuteCommandAsync(TCommand command, TOptions options)
        {
            dynamic cmd = command;

            try
            {
                BaseCommand.BypassElevationCheck = true;
                var result = cmd.ExecuteAsync(options, CancellationToken.None);
                return await result;
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                return cmd.Execute(options, CancellationToken.None);
            }
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
            IServiceManager nullManager = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>("serviceManager", () => CreateCommandInstanceWithManager(nullManager));
            Assert.Equal("serviceManager", ex.ParamName);
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
        public virtual async Task Execute_EmptyServiceName_ReturnsFailure(string invalidServiceName)
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
        /// Validates that an <see cref="UnauthorizedAccessException"/> thrown by the service manager is caught and returns an "Access Denied" failure result.
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
            Assert.False(result.IsSuccess);
            Assert.Contains("Access Denied", result.Message);
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
        /// Validates that attempting to execute an operation on an uninstalled service returns a "service not found" failure result.
        /// </summary>
        [Fact]
        public virtual async Task Execute_ServiceNotInstalled_ReturnsServiceNotFoundError()
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

        #endregion
    }
}