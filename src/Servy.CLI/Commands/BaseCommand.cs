using Servy.CLI.Models;
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
        /// Catches <see cref="UnauthorizedAccessException"/> and <see cref="Exception"/>, returning appropriate failure <see cref="CommandResult"/>.
        /// </summary>
        /// <param name="action">The synchronous command logic to execute.</param>
        /// <returns>A <see cref="CommandResult"/> representing success or failure of the command.</returns>
        protected CommandResult ExecuteWithHandling(Func<CommandResult> action)
        {
            try
            {
                return action();
            }
            catch (UnauthorizedAccessException)
            {
                return CommandResult.Fail("Administrator privileges are required.");
            }
            catch (Exception ex)
            {
                Logger.Error("An unexpected error occurred.", ex);
                return CommandResult.Fail($"An unexpected error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes an asynchronous command action with common error handling.
        /// Catches <see cref="UnauthorizedAccessException"/> and <see cref="Exception"/>, returning appropriate failure <see cref="CommandResult"/>.
        /// </summary>
        /// <param name="action">The asynchronous command logic to execute.</param>
        /// <returns>A <see cref="Task{CommandResult}"/> representing success or failure of the command.</returns>
        protected async Task<CommandResult> ExecuteWithHandlingAsync(Func<Task<CommandResult>> action)
        {
            try
            {
                return await action();
            }
            catch (UnauthorizedAccessException)
            {
                return CommandResult.Fail("Administrator privileges are required.");
            }
            catch (Exception ex)
            {
                Logger.Error("An unexpected error occurred.", ex);
                return CommandResult.Fail($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
