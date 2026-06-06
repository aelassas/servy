using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.Core.Common;
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
        /// Executes a synchronous command action with common error handling.
        /// Catches <see cref="UnauthorizedAccessException"/> and <see cref="Exception"/>, returning an appropriate contextual failure <see cref="CommandResult"/>.
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
        /// Catches <see cref="UnauthorizedAccessException"/> and <see cref="Exception"/>, returning an appropriate contextual failure <see cref="CommandResult"/>.
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
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

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
                        await onSuccess(cancellationToken);
                    }

                    var successMsg = successMessageFormatter(serviceName);
                    Logger.Info(successMsg);
                    return CommandResult.Ok(successMsg);
                }
                else
                {
                    Logger.Error(res.ErrorMessage);
                    return CommandResult.Fail(res.ErrorMessage);
                }
            });
        }

        /// <summary>
        /// Centralizes exception logging and CommandResult formatting for both synchronous and asynchronous command executions.
        /// </summary>
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