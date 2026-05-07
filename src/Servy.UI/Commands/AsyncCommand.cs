using Servy.Core.Logging;
using System;
using System.Threading;
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
        private readonly string _name;
        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;

        // Use an integer for atomic Interlocked operations (0 = idle, 1 = executing)
        private int _isExecuting;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncCommand"/> class.
        /// </summary>
        /// <param name="execute">The asynchronous task to execute when the command is invoked.</param>
        /// <param name="canExecute">An optional predicate to determine if the command is allowed to execute.</param>
        /// <param name="name">Command name.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="execute"/> is null.</exception>
        public AsyncCommand(Func<object, Task> execute, Predicate<object> canExecute = null, string name = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _name = name;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data, this object can be set to <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the command is not currently executing and the predicate allows it; otherwise, <see langword="false"/>.</returns>
        public bool CanExecute(object parameter)
        {
            // Thread-safe read of the execution state
            return Volatile.Read(ref _isExecuting) == 0 && (_canExecute?.Invoke(parameter) ?? true);
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
                Logger.Error($"AsyncCommand '{_name ?? "<unnamed>"}' execution failed.", ex);
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
            // Atomic check-and-set: Attempt to transition from 0 (idle) to 1 (executing).
            // If the original value was not 0, another execution is already in progress.
            if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0) return;

            try
            {
                // Secondary check: ensure the custom predicate still allows execution
                if (_canExecute != null && !_canExecute(parameter)) return;

                RaiseCanExecuteChanged();
                await _execute(parameter);
            }
            finally
            {
                // Atomic reset to idle
                Interlocked.Exchange(ref _isExecuting, 0);
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