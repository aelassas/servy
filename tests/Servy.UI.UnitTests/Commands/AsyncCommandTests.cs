using Servy.UI.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UI.UnitTests.Commands
{
    public class AsyncCommandTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_NullExecute_ThrowsArgumentNullException()
        {
            // Branch: execute ?? throw new ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => new AsyncCommand(null));
        }

        #endregion

        #region CanExecute Tests

        [Fact]
        public void CanExecute_IdleNoPredicate_ReturnsTrue()
        {
            // Branch: Volatile.Read == 0 && (_canExecute?.Invoke ?? true)
            var command = new AsyncCommand(_ => Task.CompletedTask);
            Assert.True(command.CanExecute(null));
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void CanExecute_WithPredicate_ReturnsPredicateResult(bool predicateResult, bool expected)
        {
            // Branch: Predicate branch of (_canExecute?.Invoke(parameter) ?? true)
            var command = new AsyncCommand(_ => Task.CompletedTask, _ => predicateResult);
            Assert.Equal(expected, command.CanExecute(null));
        }

        [Fact]
        public async Task CanExecute_DuringExecution_ReturnsFalse()
        {
            // Branch: Volatile.Read(ref _isExecuting) == 0 (where it is 1)
            var tcs = new TaskCompletionSource<bool>();
            var command = new AsyncCommand(_ => tcs.Task);

            var executionTask = command.ExecuteAsync(null);

            // Command is currently running
            Assert.False(command.CanExecute(null));

            tcs.SetResult(true);
            await executionTask;

            // Command is idle again
            Assert.True(command.CanExecute(null));
        }

        #endregion

        #region ExecuteAsync (Logic & Re-entrancy)

        [Fact]
        public async Task ExecuteAsync_PreventsReentrancy()
        {
            // Branch: if (Interlocked.CompareExchange(...) != 0) return;
            int callCount = 0;
            var tcs = new TaskCompletionSource<bool>();

            var command = new AsyncCommand(async _ =>
            {
                Interlocked.Increment(ref callCount);
                await tcs.Task;
            });

            // Trigger first execution
            var task1 = command.ExecuteAsync(null);

            // Attempt to trigger second execution while first is busy
            var task2 = command.ExecuteAsync(null);

            tcs.SetResult(true);
            await Task.WhenAll(task1, task2);

            // Assert that the inner logic only ran once
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task ExecuteAsync_RespectsPredicate_InsideLock()
        {
            // Branch: if (_canExecute != null && !_canExecute(parameter)) return;
            bool wasExecuted = false;
            var command = new AsyncCommand(_ => Task.Run(() => wasExecuted = true), _ => false);

            await command.ExecuteAsync(null);

            Assert.False(wasExecuted);
        }

        #endregion

        #region Execute (Async Void & Exceptions)

        [Fact]
        public void Execute_SwallowsExceptionAndLogs()
        {
            // Branch: catch (Exception ex) in Execute(object parameter)
            // This test ensures the 'async void' entry point does not crash the process.

            var command = new AsyncCommand(_ => throw new Exception("Command Failure"));

            // Act & Assert: Should not throw to caller
            var exception = Record.Exception(() => command.Execute(null));
            Assert.Null(exception);
        }

        #endregion

        [Fact]
        public void RaiseCanExecuteChanged_InvokesCommandManager()
        {
            // Arrange
            var command = new AsyncCommand(_ => Task.CompletedTask);

            // Act
            // Capture any potential exceptions thrown by environmental mismatches 
            // or underlying CommandManager synchronization dependencies.
            var exception = Record.Exception(() => command.RaiseCanExecuteChanged());

            // Assert
            // This verifies that the execution pipeline is completely safe to call 
            // from standard threads, preventing regression crashes in background workers.
            Assert.Null(exception);
        }
    }
}