using Moq;
using Servy.CLI.Models;
using Servy.Core.Services;

namespace Servy.CLI.UnitTests.Commands
{
    /// <summary>
    /// Centralized base test harness to standardize skeleton verifications for service management CLI operations.
    /// Optimized for modern runtime features.
    /// </summary>
    /// <typeparam name="TCommand">The explicit runtime type of the command service wrapper under validation.</typeparam>
    /// <typeparam name="TOptions">The user configuration options model matching the signature parameters of the target execution action.</typeparam>
    public abstract class ServiceCommandTestsBase<TCommand, TOptions>
        where TOptions : class, new()
    {
        /// <summary>
        /// Gets the mocked service manager instance used to orchestrate SCM simulation behaviors.
        /// </summary>
        protected readonly Mock<IServiceManager> MockServiceManager;

        /// <summary>
        /// Gets the concrete command instance context layer under test.
        /// </summary>
        protected readonly TCommand Command;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommandTestsBase{TCommand, TOptions}"/> class.
        /// Configures the core isolation layers and invokes the internal component factory hook context.
        /// </summary>
        protected ServiceCommandTestsBase()
        {
            MockServiceManager = new Mock<IServiceManager>();
            Command = CreateCommandInstance();
        }

        #region Extensibility Template Hooks

        /// <summary>
        /// When overridden in a derived class, instantiates the exact command system element intended for unit analysis.
        /// </summary>
        /// <returns>A fully configured, target execution layer instance of <typeparamref name="TCommand"/>.</returns>
        protected abstract TCommand CreateCommandInstance();

        /// <summary>
        /// When overridden in a derived class, builds a valid parameter dataset populated with a service name indicator.
        /// </summary>
        /// <param name="serviceName">The unique identifying target name context applied to the DTO instance properties.</param>
        /// <returns>A valid runtime input instance parameter of type <typeparamref name="TOptions"/>.</returns>
        protected abstract TOptions CreateValidOptions(string serviceName);

        /// <summary>
        /// When overridden in a derived class, initializes an unpopulated options payload designed to test short-circuiting validation blocks.
        /// </summary>
        /// <returns>An structurally empty configuration instance of type <typeparamref name="TOptions"/>.</returns>
        protected abstract TOptions CreateEmptyOptions();

        /// <summary>
        /// When overridden in a derived class, returns the exact success notification message string expected from the pipeline output.
        /// </summary>
        /// <param name="serviceName">The name of the target configuration entity evaluated during the transaction lifecycle.</param>
        /// <returns>The fully formatted exact success validation match text string.</returns>
        protected abstract string ExpectedSuccessMessage(string serviceName);

        /// <summary>
        /// When overridden in a derived class, provides the expected fallback error segment mapped inside the generic exception handler template logic.
        /// </summary>
        /// <param name="serviceName">The identifier of the component target under context analysis.</param>
        /// <returns>The descriptive structural operation action string fragment.</returns>
        protected abstract string ExpectedGenericActionMessage(string serviceName);

        /// <summary>
        /// When overridden in a derived class, targets mock interface expressions to evaluate a successful operational runtime workflow path.
        /// </summary>
        /// <param name="mockManager">The mock object infrastructure managing the core service interaction behavior.</param>
        /// <param name="serviceName">The exact tracking identity argument value targeted by the mock assertion loop.</param>
        protected abstract void SetupServiceManagerSuccess(Mock<IServiceManager> mockManager, string serviceName);

        /// <summary>
        /// When overridden in a derived class, targets mock interface expressions to evaluate an explicit logic failure workflow condition.
        /// </summary>
        /// <param name="mockManager">The mock object infrastructure managing the core service interaction behavior.</param>
        /// <param name="serviceName">The exact tracking identity argument value targeted by the mock assertion loop.</param>
        /// <param name="errorMsg">The descriptive failure message payload configured to return from the mock layer.</param>
        protected abstract void SetupServiceManagerFailure(Mock<IServiceManager> mockManager, string serviceName, string errorMsg);

        /// <summary>
        /// When overridden in a derived class, intercepts standard SCM queries to inject terminal framework exception instances directly.
        /// </summary>
        /// <typeparam name="TException">The specific runtime <see cref="Exception"/> subtype context targeted to drop out of the mock matrix.</typeparam>
        /// <param name="mockManager">The mock object infrastructure managing the core service interaction behavior.</param>
        /// <param name="serviceName">The exact tracking identity argument value targeted by the mock assertion loop.</param>
        protected abstract void SetupServiceManagerException<TException>(Mock<IServiceManager> mockManager, string serviceName) where TException : Exception, new();

        /// <summary>
        /// Routes the command processing context to modern runtime execution lanes dynamically via structural duck-typing.
        /// Automatically checks for asynchronous signatures before falling back to synchronous handlers.
        /// </summary>
        /// <param name="command">The specific system target layer component executing the required user operation.</param>
        /// <param name="options">The primary properties parameter criteria passing configuration keys to the processor context.</param>
        /// <returns>An asynchronous task returning a definitive runtime verification <see cref="CommandResult"/> wrapper state.</returns>
        protected virtual async Task<CommandResult> ExecuteCommandAsync(TCommand command, TOptions options)
        {
            // Dynamic routing checks for asynchronous or synchronous execution paradigms natively 
            // without requiring a specific shared IAsyncCommand interface to exist.
            dynamic cmd = command!;

            try
            {
                var result = cmd.ExecuteAsync(options, TestContext.Current.CancellationToken);
                return await result;
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // Fallback route if the destination target class maps purely onto a synchronous execution block.
                return cmd.Execute(options, TestContext.Current.CancellationToken);
            }
        }

        #endregion

        #region Base Core Test Suite Skeleton

        /// <summary>
        /// Validates that a perfectly matching inputs and valid setup returns a successful result with the proper text payload.
        /// </summary>
        /// <returns>An asynchronous task tracking the verification flow execution state.</returns>
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
            Assert.True(result.Success);
            Assert.Equal(ExpectedSuccessMessage(serviceName), result.Message);
        }

        /// <summary>
        /// Validates that passing empty service identifiers directly terminates processing with a structured failure error response string.
        /// </summary>
        /// <returns>An asynchronous task tracking the verification flow execution state.</returns>
        [Fact]
        public virtual async Task Execute_EmptyServiceName_ReturnsFailure()
        {
            // Arrange
            var options = CreateEmptyOptions();

            // Act
            var result = await ExecuteCommandAsync(Command, options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Service name is required.", result.Message);
        }

        /// <summary>
        /// Validates that operational failures reported explicitly by the service manager layer are translated into a failure response cleanly.
        /// </summary>
        /// <returns>An asynchronous task tracking the verification flow execution state.</returns>
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
            Assert.False(result.Success);
            Assert.Equal(expectedFailureText, result.Message);
        }

        /// <summary>
        /// Validates that intercepting security constraints and lack of privileges formats a predictable, informative administrator elevation notification.
        /// </summary>
        /// <returns>An asynchronous task tracking the verification flow execution state.</returns>
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
            Assert.False(result.Success);
            Assert.Contains("Access Denied", result.Message);
        }

        /// <summary>
        /// Validates that unexpected runtime environment exceptions are gracefully caught and map cleanly back into safe localized generic diagnostic outputs.
        /// </summary>
        /// <returns>An asynchronous task tracking the verification flow execution state.</returns>
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
            Assert.False(result.Success);
            Assert.Contains(ExpectedGenericActionMessage(serviceName), result.Message);
        }

        #endregion
    }
}