namespace Servy.CLI.Helpers
{
    /// <summary>
    /// Provides helper methods for console applications.
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// Runs an asynchronous action while displaying a console loading spinner.
        /// The spinner shows next to a custom message until the action completes.
        /// </summary>
        /// <param name="action">The asynchronous work to execute while the spinner is shown.</param>
        /// <param name="message">
        /// The message to display next to the spinner. Defaults to "Preparing environment...".
        /// </param>
        /// <returns>A task that completes when the action finishes and the spinner is cleared.</returns>
        /// <remarks>
        /// This method runs the spinner on a background task and cancels it automatically
        /// when the action completes. The console line is cleared after the spinner stops.
        /// </remarks>
        public static async Task RunWithLoadingAnimation(Func<Task> action, string message = "Preparing environment...")
        {
            var spinnerChars = new[] { '|', '/', '-', '\\' };
            var spinnerIndex = 0;

            using (var cts = new CancellationTokenSource())
            {
                var spinnerTask = Task.Run(async () =>
                {
                    // Only attempt to show spinner if output is not redirected
                    if (Console.IsOutputRedirected) return;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        Console.Write($"\r{message} {spinnerChars[spinnerIndex++ % spinnerChars.Length]}");
                        try
                        {
                            await Task.Delay(100, cts.Token);
                        }
                        catch (OperationCanceledException) { /* Expected */ }
                    }
                });

                try
                {
                    await action();
                }
                finally
                {
                    // 1. Signal cancellation
                    cts.Cancel();

                    // 2. Wait for the spinner task to finish gracefully
                    try
                    {
                        await spinnerTask;
                    }
                    catch (Exception) { /* Ignore background task faults on exit */ }

                    // 3. SAFE CLEARING: Only clear the line if we have a valid window
                    // This prevents IOException in CI/Piped environments
                    try
                    {
                        if (!Console.IsOutputRedirected)
                        {
                            int width = Console.WindowWidth;
                            if (width > 0)
                            {
                                // Clear the line and return the cursor to the start
                                Console.Write("\r" + new string(' ', width - 1) + "\r");
                            }
                        }
                    }
                    catch (IOException)
                    {
                        // Fallback: If we still hit an IO issue, just write a newline to move on
                        Console.WriteLine();
                    }
                }
            }
        }

    }
}
