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
        // Test Seam: Enables unit tests to bypass non-mockable static OS environment checks deterministically.
        private static bool _bypassElevationCheck = false;

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
        /// Executes a synchronous command action with common error handling.
        /// Wraps the action with common error handling <see cref="OperationCanceledException"/> is translated to a clean cancellation result, 
        /// and all other exceptions are routed through <see cref="HandleException"/> (which additionally special-cases <see cref="UnauthorizedAccessException"/>).
        /// </summary>
        /// <param name="commandName">Command name  (e.g., "install", "start").</param>
        /// <param name="action">A description of what is being attempted (e.g., "install service 'MyService'").</param>
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
        /// Executes an asynchronous command action with common error handling.
        /// Wraps the action with common error handling <see cref="OperationCanceledException"/> is translated to a clean cancellation result, 
        /// and all other exceptions are routed through <see cref="HandleException"/> (which additionally special-cases <see cref="UnauthorizedAccessException"/>).
        /// </summary>
        /// <param name="commandName">Command name  (e.g., "install", "start").</param>
        /// <param name="action">A description of what is being attempted (e.g., "start service 'MyService'").</param>
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
                // Return a clean failure result for user-initiated cancellations.
                // This avoids logging a full stack trace for an expected event.
                return CommandResult.Fail(string.Format(Strings.Msg_CommandCancelled, commandName));
            }
            catch (Exception ex)
            {
                return HandleException(ex, commandName, action, suggestion);
            }
        }

        /// <summary>
        /// Centralizes shared service management pre-flight validation, installation assertions, and operation logging pipelines.
        /// </summary>
        /// <param name="commandName">The diagnostic name of the command executing the pipeline context used for logging scopes.</param>
        /// <param name="action">The specific architectural intent verb used to construct detailed context error strings.</param>
        /// <param name="suggestion">The informative remediation recommendation message provided to the end-user upon structural failures.</param>
        /// <param name="serviceName">The system identity name of the target Windows service to evaluate.</param>
        /// <param name="serviceManager">The operational management abstraction framework instance used to check installation topologies.</param>
        /// <param name="operation">The core delegate wrapping the actual asynchronous SCM manipulation step.</param>
        /// <param name="successMessageFormatter">A string manipulation delegate used to generate uniform success notification logs.</param>
        /// <param name="preCheck">An optional delegate invocation layer that performs specialized pre-flight verification assertions.</param>
        /// <param name="onSuccess">An optional asynchronous callback, awaited after the operation succeeds, used to synchronize repository state (e.g. DB upsert). Failures are caught and logged as a warning without failing the command.</param>
        /// <param name="cancellationToken">A cancellation token tracking task state abandonment flags across runtime pools.</param>
        /// <returns>An asynchronous task returning a definitive <see cref="CommandResult"/> representing pipeline execution outcomes.</returns>
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
                // Pre-flight elevation check wrapped inside a protective test seam hook
                if (!_bypassElevationCheck)
                {
                    SecurityHelper.EnsureAdministrator();
                }

                if (string.IsNullOrWhiteSpace(serviceName))
                    return CommandResult.Fail(Strings.Msg_ServiceNameRequired);

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
                            // Transform terminal execution fault into an informative best-effort warning descriptor.
                            // Prevents database write-locks or directory IO bottlenecks from masking a successful SCM operation.
                            Logger.Warn($"{commandName}: Service operation completed successfully, but post-success repository synchronization failed for '{serviceName}': {ex.Message}");
                        }
                    }

                    var successMsg = successMessageFormatter(serviceName);
                    Logger.Info(successMsg);
                    return CommandResult.Ok(successMsg);
                }
                else
                {
                    Logger.Error(res.ErrorMessage);
                    return res.ToFailure();
                }
            });
        }

        /// <summary>
        /// Centralizes exception logging and CommandResult formatting for both synchronous and asynchronous command executions.
        /// </summary>
        /// <param name="ex">The intercepted <see cref="Exception"/> instance requiring diagnostics processing and logging.</param>
        /// <param name="commandName">The execution name of the calling command used to populate localized remediation placeholders.</param>
        /// <param name="action">The core operational verb mapping to the attempted system action being protected.</param>
        /// <param name="suggestion">An optional actionable remediation fallback suggestion provided to the user upon command failure.</param>
        /// <returns>A formatted <see cref="CommandResult"/> encapsulating the failure context and localized error templates.</returns>
        private CommandResult HandleException(Exception ex, string commandName, string action, string suggestion)
        {
            if (ex is UnauthorizedAccessException)
            {
                Logger.Error($"Failed to {action} (Unauthorized)", ex);

                // This uses the specific resource string: 
                // "Access Denied. Please restart the shell as Administrator to run the '{0}' command."
                var errorMessage = string.Format(Strings.Msg_AdminPrivilegesRequired, commandName);

                // We can skip the 'fallbackSuggestion' since the message is already so clear.
                return CommandResult.Fail(errorMessage);
            }
            else
            {
                Logger.Error($"Failed to {action}", ex);

                // ROBUSTNESS: Utilize localized templates rather than hardcoded English string fragments.
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