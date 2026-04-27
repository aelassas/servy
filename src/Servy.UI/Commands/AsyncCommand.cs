using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Servy.UI.Commands
{
    /// <summary>
    /// An implementation of the <see cref="ICommand"/> interface that supports asynchronous operations 
    /// and provides automatic UI state management via the WPF <see cref="CommandManager"/>.
    /// </summary>
    public class AsyncCommand : IAsyncCommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;
        private volatile bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncCommand"/> class.
        /// </summary>
        /// <param name="execute">The asynchronous task to execute when the command is invoked.</param>
        /// <param name="canExecute">An optional predicate to determine if the command is allowed to execute.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="execute"/> is null.</exception>
        public AsyncCommand(Func<object, Task> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data, this object can be set to <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the command is not currently executing and the predicate allows it; otherwise, <see langword="false"/>.</returns>
        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        /// <summary>
        /// The standard entry point for WPF command execution. Dispatches to <see cref="ExecuteAsync"/> and 
        /// captures any exceptions to prevent dispatcher crashes.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        public async void Execute(object parameter)
        {
            try
            {
                await ExecuteAsync(parameter);
            }
            catch (Exception ex)
            {
                // Log and surface - never let an async void exception escape into the dispatcher.
                Core.Logging.Logger.Error("AsyncCommand execution failed.", ex);
            }
        }

        /// <summary>
        /// Executes the asynchronous logic associated with this command. 
        /// Manages the internal execution state to ensure re-entrancy protection.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// Delegated to the WPF <see cref="CommandManager.RequerySuggested"/> to ensure reliable UI updates.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Triggers a global re-evaluation of all command bindings within the UI.
        /// Utilizes <see cref="CommandManager.InvalidateRequerySuggested"/> to safely marshal the update to the UI thread.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            // Note: Inversion of control - this automatically dispatches to the UI thread!
            CommandManager.InvalidateRequerySuggested();
        }
    }
}