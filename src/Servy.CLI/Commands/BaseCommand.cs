using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.Core.Logging;
using System;
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