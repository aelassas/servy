using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Servy.Testing
{
    /// <summary>
    /// Routes the static <see cref="Logger"/> into a private temporary directory for the duration of
    /// one action and returns what it wrote there, so a test can assert on the file sink without
    /// creating or re-ACLing the product's own logs directory.
    /// </summary>
    /// <remarks>
    /// The <c>logDirectory</c> argument of <c>Logger.Initialize</c> is sticky - <c>Shutdown</c> does not
    /// clear it - so every overload points
    /// the logger back at <see cref="AppConfig.LogsFolderPath"/> on the way out. Without that, every
    /// later test in the assembly writes into the temporary directory this one has just deleted.
    /// </remarks>
    public static class LogCapture
    {
        /// <summary>
        /// Asynchronously captures the file log and a return result during task execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the asynchronous operation.</typeparam>
        /// <param name="testAction">An asynchronous delegate returning a task with <typeparamref name="TResult"/> to execute while the static logger is redirected.</param>
        /// <param name="level">The level the static logger is initialized at.</param>
        /// <returns>A tuple containing the operation's return result (<c>Result</c>) and the captured log text (<c>Log</c>).</returns>
        public static async Task<(TResult Result, string Log)> RunAsync<TResult>(Func<Task<TResult>> testAction, LogLevel level = LogLevel.Info)
        {
            var capture = new Capture(level);
            try
            {
                var result = await testAction();
                return (result, capture.ReadBack());
            }
            finally
            {
                capture.Dispose();
            }
        }

        /// <summary>
        /// Asynchronously captures the file log during task execution.
        /// </summary>
        /// <param name="testAction">An asynchronous delegate returning a task to execute while the static logger is redirected.</param>
        /// <param name="level">The level the static logger is initialized at.</param>
        /// <returns>The captured log text.</returns>
        public static async Task<string> RunAsync(Func<Task> testAction, LogLevel level = LogLevel.Info)
        {
            var captured = await RunAsync(async () => { await testAction(); return true; }, level);
            return captured.Log;
        }

        /// <summary>
        /// Synchronously captures the file log and a return result during action execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the synchronous operation.</typeparam>
        /// <param name="testAction">A delegate returning <typeparamref name="TResult"/> to execute while the static logger is redirected.</param>
        /// <param name="level">The level the static logger is initialized at.</param>
        /// <returns>A tuple containing the operation's return result (<c>Result</c>) and the captured log text (<c>Log</c>).</returns>
        public static (TResult Result, string Log) Run<TResult>(Func<TResult> testAction, LogLevel level = LogLevel.Info)
        {
            var capture = new Capture(level);
            try
            {
                var result = testAction();
                return (result, capture.ReadBack());
            }
            finally
            {
                capture.Dispose();
            }
        }

        /// <summary>
        /// Synchronously captures the file log during action execution.
        /// </summary>
        /// <param name="testAction">The delegate or test action to execute while the static logger is redirected.</param>
        /// <param name="level">The level the static logger is initialized at.</param>
        /// <returns>The captured log text.</returns>
        public static string Run(Action testAction, LogLevel level = LogLevel.Info)
        {
            var captured = Run(() => { testAction(); return true; }, level);
            return captured.Log;
        }

        /// <summary>
        /// Owns the temporary log directory and the static logger state for one capture.
        /// </summary>
        private sealed class Capture : IDisposable
        {
            private readonly string _directory;
            private readonly string _filePath;

            public Capture(LogLevel level)
            {
                _directory = Path.Combine(Path.GetTempPath(), "ServyTestLogs", Guid.NewGuid().ToString("N"));
                var fileName = string.Format("Servy_Test_Log_{0}.log", Guid.NewGuid().ToString("N"));
                _filePath = Path.Combine(_directory, fileName);

                Logger.Shutdown();
                Logger.Initialize(fileName, level, logDirectory: _directory);
            }

            /// <summary>
            /// Flushes and releases the log file handle, then returns what was written.
            /// </summary>
            /// <returns>The log text, or an empty string when nothing was written.</returns>
            public string ReadBack()
            {
                Logger.Shutdown();
                return File.Exists(_filePath) ? File.ReadAllText(_filePath) : string.Empty;
            }

            /// <summary>
            /// Restores the static logger's default directory and removes the temporary one.
            /// </summary>
            public void Dispose()
            {
                Logger.Shutdown();

                // Initialize's logDirectory override is sticky - Shutdown does not clear it - so point
                // the static logger back at its default folder before leaving, or every later test in
                // the assembly writes into the temporary directory this one is about to delete.
                Logger.Initialize(null, logDirectory: AppConfig.LogsFolderPath);
                Logger.Shutdown();

                if (Directory.Exists(_directory))
                {
                    try { Directory.Delete(_directory, recursive: true); } catch { /* Ignore cleanup errors */ }
                }
            }
        }
    }
}
