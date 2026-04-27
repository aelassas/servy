using System.Windows.Input;

namespace Servy.UI.Commands
{
    /// <summary>
    /// A command whose sole purpose is to relay its functionality 
    /// to other objects by invoking delegates with a strongly typed parameter.
    /// </summary>
    /// <typeparam name="T">The type of the parameter.</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand{T}"/> class.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        /// <param name="canExecute">The execution status logic (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="execute"/> is null.</exception>
        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc />
        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null) return true;

            // Safe unboxing: If parameter is null, provide default(T).
            // This prevents InvalidCastException for value types like int or bool.
            return _canExecute(parameter is T typed ? typed : default(T));
        }

        /// <inheritdoc />
        public void Execute(object? parameter)
        {
            // Apply the same safe check for the execution logic.
            _execute(parameter is T typed ? typed : default(T));
        }

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Manually triggers a re-evaluation of the command's CanExecute logic.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}