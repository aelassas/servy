using Servy.UI.Commands;

namespace Servy.UI.UnitTests.Commands
{
    public class AsyncCommandTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_NullExecute_ThrowsArgumentNullException()
        {
            // Branch: execute ?? throw new ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => new AsyncCommand(null!));
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Execute_DoesNotThrowWhenInnerTaskFails(bool isCancellation)
        {
            // Branch: catch (Exception ex) in Execute(object parameter)
            // This test ensures the 'async void' entry point does not crash the process.

            var previousContext = SynchronizationContext.Current;
            var testContext = new TestSynchronizationContext();
            try
            {
                SynchronizationContext.SetSynchronizationContext(testContext);

                var command = new AsyncCommand(_ => isCancellation
                    ? Task.FromCanceled(new CancellationToken(true))
                    : throw new Exception("Command Failure"));

                command.Execute(null);
                await testContext.WaitForCompletionAsync();

                Assert.Empty(testContext.UnhandledExceptions);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        #endregion

        [Fact]
        public void RaiseCanExecuteChanged_DoesNotThrow()
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

        #region Helper Classes

        /// <summary>
        /// A custom <see cref="SynchronizationContext"/> implementation designed to track 
        /// async void operation completions and capture any unhandled exceptions during unit testing.
        /// </summary>
        private class TestSynchronizationContext : SynchronizationContext
        {
            private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _pendingOperations;

            /// <summary>
            /// Gets the collection of unhandled exceptions posted back to this synchronization context.
            /// </summary>
            public List<Exception> UnhandledExceptions { get; } = new List<Exception>();

            /// <summary>
            /// Notifies the context that an asynchronous operation has started.
            /// Increments the pending operation counter.
            /// </summary>
            public override void OperationStarted()
            {
                Interlocked.Increment(ref _pendingOperations);
            }

            /// <summary>
            /// Notifies the context that an asynchronous operation has completed.
            /// Decrements the pending operation counter and completes the completion task when zero.
            /// </summary>
            public override void OperationCompleted()
            {
                if (Interlocked.Decrement(ref _pendingOperations) == 0)
                {
                    _tcs.TrySetResult(true);
                }
            }

            /// <summary>
            /// Dispatches an asynchronous message to the synchronization context.
            /// </summary>
            /// <param name="d">The <see cref="SendOrPostCallback"/> delegate to call.</param>
            /// <param name="state">The object passed to the delegate.</param>
            public override void Post(SendOrPostCallback d, object? state)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    SetSynchronizationContext(this);
                    try
                    {
                        d(state);
                    }
                    catch (Exception ex)
                    {
                        lock (UnhandledExceptions)
                        {
                            UnhandledExceptions.Add(ex);
                        }
                    }
                });
            }

            /// <summary>
            /// Asynchronously waits until all tracked operations posted to this synchronization context have completed.
            /// </summary>
            /// <returns>A <see cref="Task"/> that completes when all tracked operations are finished.</returns>
            public Task WaitForCompletionAsync() => _tcs.Task;
        }

        #endregion
    }
}
