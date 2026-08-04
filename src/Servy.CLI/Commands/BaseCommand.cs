using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Base class for CLI commands providing centralized exception handling for command execution.
    /// </summary>
    public abstract class BaseCommand
    {
        /// <summary>
        /// Test Seam: Enables unit tests to bypass non-mockable static OS environment checks deterministically.
        /// </summary>
        internal static bool BypassElevationCheck = false;

        /// <summary>
        /// Creates a pre-check delegate that verifies if a specific service is in a 'Disabled' state before proceeding with a command.
        /// </summary>
        /// <param name="serviceManager">The <see cref="IServiceManager"/> instance used to query current service startup configuration.</param>
        /// <param name="serviceName">The unique name of the service to inspect.</param>
        /// <returns>
        /// A <see cref="Func{CancellationToken, CommandResult}"/> that, when executed, returns a failed <see cref="CommandResult"/> 
        /// if the service is disabled; otherwise, returns <c>null</c> to signal the check passed.
        /// </returns>
        protected Func<CancellationToken, CommandResult> NotDisabledPreCheck(IServiceManager serviceManager, string serviceName) =>
            token =>
            {
                var startupType = serviceManager.GetServiceStartupType(serviceName, cancellationToken: token);
                return startupType == ServiceStartType.Disabled ? CommandResult.Fail(Strings.Msg_ServiceDisabledError) : null;
            };

        /// <summary>
        /// Executes a synchronous command action with common error handling: <see cref="OperationCanceledException"/> 
        /// is translated to a clean cancellation result, and all other exceptions are routed through <see cref="HandleException"/>.
        /// </summary>
        /// <param name="commandName">The name of the command executing (e.g., "install", "start"), used for logging scopes.</param>
        /// <param name="action">Human-readable description of the attempted operation, used in error messages.</param>
        /// <param name="suggestion">Actionable advice for the user if the command fails.</param>
        /// <param name="task">The synchronous command logic to execute.</param>
        /// <returns>A <see cref="CommandResult"/> representing success or failure of the command.</returns>
        protected CommandResult ExecuteWithHandling(string commandName, string action, string suggestion, Func<CommandResult> task)
        {
            try
            {
                return task();
            }
            catch (OperationCanceledException)
            {
                return CommandResult.Fail(string.Format(Strings.Msg_CommandCancelled, commandName));
            }
            catch (Exception ex)
            {
                return HandleException(ex, commandName, action, suggestion);
            }
        }

        /// <summary>
        /// Executes an asynchronous command action with common error handling: <see cref="OperationCanceledException"/> 
        /// is translated to a clean cancellation result, and all other exceptions are routed through <see cref="HandleException"/>.
        /// </summary>
        /// <param name="commandName">The name of the command executing (e.g., "install", "start"), used for logging scopes.</param>
        /// <param name="action">Human-readable description of the attempted operation, used in error messages.</param>
        /// <param name="suggestion">Actionable advice for the user if the command fails.</param>
        /// <param name="task">The asynchronous command logic to execute.</param>
        /// <returns>A <see cref="Task{CommandResult}"/> representing success or failure of the command.</returns>
        protected async Task<CommandResult> ExecuteWithHandlingAsync(string commandName, string action, string suggestion, Func<Task<CommandResult>> task)
        {
            try
            {
                return await task();
            }
            catch (OperationCanceledException)
            {
                return CommandResult.Fail(string.Format(Strings.Msg_CommandCancelled, commandName));
            }
            catch (Exception ex)
            {
                return HandleException(ex, commandName, action, suggestion);
            }
        }

        /// <summary>
        /// Centralizes shared service management pre-flight validation, installation assertions, and operation logging.
        /// </summary>
        /// <param name="commandName">The name of the command executing, used for logging scopes.</param>
        /// <param name="action">Human-readable description of the attempted operation, used in error messages.</param>
        /// <param name="suggestion">Remediation suggestion provided to the user on failure.</param>
        /// <param name="serviceName">The name of the target Windows service.</param>
        /// <param name="serviceManager">The service manager instance used to manage Windows services.</param>
        /// <param name="operation">The delegate wrapping the actual asynchronous service operation.</param>
        /// <param name="successMessageFormatter">Function used to format the success message string.</param>
        /// <param name="preCheck">Optional delegate performing pre-flight checks before the main operation runs.</param>
        /// <param name="onSuccess">Optional asynchronous callback executed after a successful operation to synchronize repository state (e.g. DB upsert). Failures are caught and logged as a warning without failing the command.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task returning a <see cref="CommandResult"/> representing the operation outcome.</returns>
        protected async Task<CommandResult> ExecuteServiceOperationAsync(
            string commandName,
            string action,
            string suggestion,
            string serviceName,
            IServiceManager serviceManager,
            Func<CancellationToken, Task<OperationResult>> operation,
            Func<string, string> successMessageFormatter,
            Func<CancellationToken, CommandResult> preCheck = null,
            Func<CancellationToken, Task> onSuccess = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithHandlingAsync(commandName, action, suggestion, async () =>
            {
                if (string.IsNullOrWhiteSpace(serviceName))
                    return CommandResult.Fail(Strings.Msg_ServiceNameRequired);

                if (!BypassElevationCheck)
                {
                    SecurityHelper.EnsureAdministrator();
                }

                var exists = serviceManager.IsServiceInstalled(serviceName, cancellationToken: cancellationToken);
                if (!exists)
                {
                    return CommandResult.Fail(Strings.Msg_ServiceNotFound);
                }

                if (preCheck != null)
                {
                    var checkResult = preCheck(cancellationToken);
                    if (checkResult != null) return checkResult;
                }

                var res = await operation(cancellationToken);
                if (res.IsSuccess)
                {
                    if (onSuccess != null)
                    {
                        try
                        {
                            await onSuccess(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"{commandName}: Service operation completed successfully, but post-success repository synchronization failed for '{serviceName}': {ex.Message}");
                        }
                    }

                    var successMsg = successMessageFormatter(serviceName);
                    Logger.Info(successMsg);
                    return CommandResult.Ok(successMsg);
                }
                else
                {
                    var reason = string.IsNullOrWhiteSpace(res.ErrorMessage) ? Strings.Msg_UnknownError : res.ErrorMessage;
                    Logger.Error($"{commandName}: failed to {action} for '{serviceName}': {reason}");
                    return res.ToFailure();
                }
            });
        }

        /// <summary>
        /// Centralizes exception logging and CommandResult formatting for both synchronous and asynchronous command executions.
        /// </summary>
        /// <param name="ex">The thrown exception to process.</param>
        /// <param name="commandName">The name of the command executing, used for logging scopes and formatting messages.</param>
        /// <param name="action">Human-readable description of the attempted operation, used in error messages.</param>
        /// <param name="suggestion">Actionable remediation suggestion provided to the user upon command failure.</param>
        /// <returns>A <see cref="CommandResult"/> detailing the failure.</returns>
        private CommandResult HandleException(Exception ex, string commandName, string action, string suggestion)
        {
            if (ex is UnauthorizedAccessException)
            {
                Logger.Error($"Failed to {action} (Unauthorized)", ex);

                var errorMessage = string.Format(Strings.Msg_AdminPrivilegesRequired, commandName);
                return CommandResult.Fail(errorMessage);
            }
            else
            {
                Logger.Error($"Failed to {action}", ex);

                var errorMessage = string.Format(Strings.Msg_CommandFailedTemplate, action, ex.Message);

                if (!string.IsNullOrEmpty(suggestion))
                {
                    var localizedSuggestion = string.Format(Strings.Msg_SuggestionTemplate, suggestion);
                    errorMessage += $"{Environment.NewLine}{localizedSuggestion}";
                }

                return CommandResult.Fail(errorMessage);
            }
        }
    }
}