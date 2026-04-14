using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.Core.Common;

namespace Servy.CLI.Helpers
{
    /// <summary>
    /// Provides extension methods for mapping internal operation results to CLI-specific command results.
    /// </summary>
    internal static class ResultExtensions
    {
        /// <summary>
        /// Converts an internal <see cref="OperationResult"/> to a CLI <see cref="CommandResult"/> with a fallback for null error messages.
        /// </summary>
        /// <remarks>
        /// Fix #308: Centralized failure mapping to prevent silent null propagation.
        /// This ensures that even if the core logic fails to provide a specific error string, 
        /// the CLI user receives a localized "Unknown Error" message instead of a blank line.
        /// </remarks>
        /// <param name="res">The source <see cref="OperationResult"/> from the core library.</param>
        /// <returns>A failure <see cref="CommandResult"/> containing either the original error message or a localized fallback.</returns>
        public static CommandResult ToFailure(this OperationResult res)
        {
            // If the whole result object is null, we definitely have an unknown error
            if (res == null) return CommandResult.Fail(Strings.Msg_UnknownError);

            string finalMessage = !string.IsNullOrWhiteSpace(res.ErrorMessage)
                ? res.ErrorMessage
                : Strings.Msg_UnknownError;

            return CommandResult.Fail(finalMessage);
        }
    }
}