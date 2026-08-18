using System.IO;

namespace Servy.Testing
{
    /// <summary>
    /// Provides synchronized, robust console redirection and stream capture capabilities for unit tests.
    /// </summary>
    public static class ConsoleCapture
    {
        private static readonly SemaphoreSlim _consoleSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Synchronously captures standard output and standard error streams during action execution.
        /// </summary>
        /// <param name="testAction">The delegate or test action to execute while console output and error streams are redirected.</param>
        /// <returns>A tuple containing the captured standard output string (<c>StdOut</c>) and standard error string (<c>StdErr</c>).</returns>
        public static (string StdOut, string StdErr) Run(Action testAction)
        {
            _consoleSemaphore.Wait();
            var oldOut = Console.Out;
            var oldErr = Console.Error;

            try
            {
                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);

                    testAction();

                    // Restore streams before StringWriters are disposed
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);

                    return (swOut.ToString(), swErr.ToString());
                }
            }
            finally
            {
                Console.SetOut(oldOut);
                Console.SetError(oldErr);
                _consoleSemaphore.Release();
            }
        }

        /// <summary>
        /// Asynchronously captures standard output, standard error, and a return result during task execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the asynchronous operation.</typeparam>
        /// <param name="testAction">An asynchronous delegate returning a task with <typeparamref name="TResult"/> to execute while console output and error streams are redirected.</param>
        /// <returns>A tuple containing the captured standard output string (<c>StdOut</c>), standard error string (<c>StdErr</c>), and the operation's return result (<c>Result</c>).</returns>
        public static async Task<(string StdOut, string StdErr, TResult Result)> RunAsync<TResult>(Func<Task<TResult>> testAction)
        {
            await _consoleSemaphore.WaitAsync();
            var oldOut = Console.Out;
            var oldErr = Console.Error;

            try
            {
                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);

                    var result = await testAction();

                    // Restore streams before StringWriters are disposed
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);

                    return (swOut.ToString(), swErr.ToString(), result);
                }
            }
            finally
            {
                Console.SetOut(oldOut);
                Console.SetError(oldErr);
                _consoleSemaphore.Release();
            }
        }
    }
}
