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
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error($"Failed to {action} (Unauthorized)", ex);

                // This uses your specific resource string: 
                // "Access Denied. Please restart your shell as Administrator to run the '{0}' command."
                var errorMessage = string.Format(Strings.Msg_AdminPrivilegesRequired, commandName);

                // We can skip the 'fallbackSuggestion' since your message is already so clear.
                return CommandResult.Fail(errorMessage);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to {action}", ex);

                var errorMessage = $"Failed to {action}: {ex.Message}";
                if (!string.IsNullOrEmpty(suggestion))
                {
                    errorMessage += $"{Environment.NewLine}Suggestion: {suggestion}";
                }

                return CommandResult.Fail(errorMessage);
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
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error($"Failed to {action} (Unauthorized)", ex);

                // This uses your specific resource string: 
                // "Access Denied. Please restart your shell as Administrator to run the '{0}' command."
                var errorMessage = string.Format(Strings.Msg_AdminPrivilegesRequired, commandName);

                // We can skip the 'fallbackSuggestion' since your message is already so clear.
                return CommandResult.Fail(errorMessage);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to {action}", ex);

                var errorMessage = $"Failed to {action}: {ex.Message}";
                if (!string.IsNullOrEmpty(suggestion))
                {
                    errorMessage += $"{Environment.NewLine}Suggestion: {suggestion}";
                }

                return CommandResult.Fail(errorMessage);
            }
        }
    }
}