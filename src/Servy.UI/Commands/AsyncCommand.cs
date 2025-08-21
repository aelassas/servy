using System;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.UI.Commands
{
    /// <inheritdoc/>
    public class AsyncCommand : IAsyncCommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;
        private bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncCommand"/> class.
        /// </summary>
        /// <param name="execute">The asynchronous action to execute.</param>
        /// <param name="canExecute">Optional predicate to determine if the command can execute.</param>
        public AsyncCommand(Func<object, Task> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc/>
        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        /// <inheritdoc/>
        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <inheritdoc/>
        public event EventHandler CanExecuteChanged;

        /// <inheritdoc/>
        public void RaiseCanExecuteChanged()
        {
            var context = SynchronizationContext.Current;
            if (context != null)
            {
                context.Post(_ => CanExecuteChanged?.Invoke(this, EventArgs.Empty), null);
            }
            else
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

    }
}
