using System;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.CLI.Helpers
{
    /// <summary>
    /// Provides helper methods for console applications.
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// Runs a synchronous action while displaying a console loading spinner.
        /// The spinner shows next to a custom message until the action completes.
        /// </summary>
        /// <param name="action">The synchronous work to execute while the spinner is shown.</param>
        /// <param name="message">
        /// The message to display next to the spinner. Defaults to "Preparing environment...".
        /// </param>
        /// <returns>A task that completes when the action finishes and the spinner is cleared.</returns>
        /// <remarks>
        /// This method runs the spinner on a background task and cancels it automatically
        /// when the action completes. The console line is cleared after the spinner stops.
        /// </remarks>
        public static async Task RunWithLoadingAnimation(Action action, string message = "Preparing environment...")
        {
            var spinnerChars = new[] { '|', '/', '-', '\\' };
            var spinnerIndex = 0;
            using (var cts = new CancellationTokenSource())
            {
                // Task that shows spinner
                var spinnerTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Console.Write($"\r{message} {spinnerChars[spinnerIndex++ % spinnerChars.Length]}");
                        try
                        {
                            await Task.Delay(100, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Ignore, happens when cancellation is requested
                        }
                    }
                });

                try
                {
                    action();  // synchronous work
                }
                finally
                {
                    // Stop spinner
                    cts.Cancel();
                    await spinnerTask.ConfigureAwait(false); // ensure spinner exits
                    Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r"); // clear line
                }
            }
        }
    }
}
