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
        /// Encapsulates console stream redirection and guarantees stream restoration before the backing StringWriters are disposed.
        /// </summary>
        private readonly struct ConsoleRedirect : IDisposable
        {
            private readonly TextWriter _oldOut;
            private readonly TextWriter _oldErr;

            public StringWriter StdOutWriter { get; }
            public StringWriter StdErrWriter { get; }

            public ConsoleRedirect(StringWriter stdOutWriter, StringWriter stdErrWriter)
            {
                _oldOut = Console.Out;
                _oldErr = Console.Error;
                StdOutWriter = stdOutWriter;
                StdErrWriter = stdErrWriter;

                Console.SetOut(stdOutWriter);
                Console.SetError(stdErrWriter);
            }

            /// <summary>
            /// Restores the original static Console stream outputs.
            /// </summary>
            public void Dispose()
            {
                Console.SetOut(_oldOut);
                Console.SetError(_oldErr);
            }
        }

        /// <summary>
        /// Synchronously captures standard output, standard error, and a return result during action execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the synchronous operation.</typeparam>
        /// <param name="testAction">A delegate returning <typeparamref name="TResult"/> to execute while console output and error streams are redirected.</param>
        /// <returns>A tuple containing the captured standard output string (<c>StdOut</c>), standard error string (<c>StdErr</c>), and the operation's return result (<c>Result</c>).</returns>
        public static (string StdOut, string StdErr, TResult Result) Run<TResult>(Func<TResult> testAction)
        {
            _consoleSemaphore.Wait();
            try
            {
                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    TResult result;
                    using (var redirect = new ConsoleRedirect(swOut, swErr))
                    {
                        result = testAction();
                    }

                    return (swOut.ToString(), swErr.ToString(), result);
                }
            }
            finally
            {
                _consoleSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronously captures standard output and standard error streams during action execution.
        /// </summary>
        /// <param name="testAction">The delegate or test action to execute while console output and error streams are redirected.</param>
        /// <returns>A tuple containing the captured standard output string (<c>StdOut</c>) and standard error string (<c>StdErr</c>).</returns>
        public static (string StdOut, string StdErr) Run(Action testAction)
        {
            var captured = Run(() => { testAction(); return true; });
            return (captured.StdOut, captured.StdErr);
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
            try
            {
                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    TResult result;
                    using (var redirect = new ConsoleRedirect(swOut, swErr))
                    {
                        result = await testAction();
                    }

                    return (swOut.ToString(), swErr.ToString(), result);
                }
            }
            finally
            {
                _consoleSemaphore.Release();
            }
        }

        /// <summary>
        /// Asynchronously captures standard output and standard error streams during task execution.
        /// </summary>
        /// <param name="testAction">An asynchronous delegate returning a task to execute while console output and error streams are redirected.</param>
        /// <returns>A tuple containing the captured standard output string (<c>StdOut</c>) and standard error string (<c>StdErr</c>).</returns>
        public static async Task<(string StdOut, string StdErr)> RunAsync(Func<Task> testAction)
        {
            var captured = await RunAsync(async () => { await testAction(); return true; });
            return (captured.StdOut, captured.StdErr);
        }
    }
}
