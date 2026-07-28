using Servy.Core.Logging;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Provides helper methods for safely executing asynchronous UI event handlers.
    /// </summary>
    public static class UiTaskRunner
    {
        /// <summary>
        /// Executes an asynchronous UI delegate with centralized error handling and logging.
        /// Silently swallows <see cref="OperationCanceledException"/> when an action is superseded or canceled.
        /// </summary>
        /// <param name="action">The asynchronous operation to execute.</param>
        /// <param name="context">Descriptive name of the calling UI context or view used in error logs.</param>
        public static async Task RunAsync(Func<Task> action, string context)
        {
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Expected - a newer user action or navigation event superseded this operation.
            }
            catch (Exception ex)
            {
                Logger.Error($"Async UI handler failed in {context}.", ex);
            }
        }
    }
}