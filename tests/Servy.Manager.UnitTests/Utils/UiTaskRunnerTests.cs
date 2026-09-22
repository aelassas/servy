using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Manager.Utils;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Unit tests for <see cref="UiTaskRunner"/>, the centralized error handler asynchronous
    /// UI event handlers delegate their failure policy to.
    /// Both catch arms exist to keep a faulted handler from reaching the dispatcher, so each one
    /// is pinned here with the exception type it claims to absorb.
    /// </summary>
    public class UiTaskRunnerTests
    {
        private const string Context = "TestContext";

        [Fact]
        public async Task RunAsync_ActionThrowsOperationCanceledException_CompletesWithoutThrowing()
        {
            // Arrange - a superseded operation is the case the first catch arm exists for.
            Func<Task> action = () => throw new OperationCanceledException();

            // Act
            var task = UiTaskRunner.RunAsync(action, Context);
            await task;

            // Assert - awaiting a faulted task would rethrow, so reaching this line is half the
            // contract; the terminal state is the other half.
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        }

        [Fact]
        public async Task RunAsync_ActionThrowsException_CompletesWithoutThrowing()
        {
            // Arrange - any other failure is logged by the second arm and must not propagate.
            Func<Task> action = () => throw new InvalidOperationException("boom");

            // Act
            var task = UiTaskRunner.RunAsync(action, Context);
            await task;

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        }

        [Fact]
        public async Task RunAsync_ActionThrowsException_LogsFailureWithContext()
        {
            // Arrange - swallowing is only half of the second arm's contract; the other half is
            // that the failure reaches the log with the context that identifies the caller.
            var logFileName = $"UiTaskRunnerTests_{Guid.NewGuid():N}.log";
            var logPath = Path.Combine(AppConfig.LogsFolderPath, logFileName);
            Func<Task> action = () => throw new InvalidOperationException("boom");

            try
            {
                // The logger is static, so take it over for this test only and hand it back below.
                Logger.Shutdown();
                Logger.Initialize(logFileName);

                // Act
                var task = UiTaskRunner.RunAsync(action, Context);
                await task;

                // Assert
                Assert.Equal(TaskStatus.RanToCompletion, task.Status);

                // Flush the writer before reading the file back.
                Logger.Shutdown();

                Assert.True(File.Exists(logPath), $"The logger wrote no file at '{logPath}'.");

                var content = File.ReadAllText(logPath);
                Assert.Contains($"Async UI handler failed in {Context}.", content);
                Assert.Contains("boom", content);
            }
            finally
            {
                Logger.Shutdown();
                try { if (File.Exists(logPath)) File.Delete(logPath); } catch { /* teardown must not hide the result */ }
            }
        }

        [Fact]
        public async Task RunAsync_ActionSucceeds_InvokesActionAndCompletesSuccessfully()
        {
            // Arrange
            bool ran = false;
            Func<Task> action = () =>
            {
                ran = true;
                return Task.CompletedTask;
            };

            // Act
            var task = UiTaskRunner.RunAsync(action, Context);
            await task;

            // Assert
            Assert.True(ran);
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        }
    }
}
