using System.Threading.Tasks;
using System.Windows.Input;

namespace Servy.UI.Commands
{
    /// <summary>
    /// Defines an asynchronous command that can be executed with a parameter.
    /// Extends <see cref="ICommand"/> to support <see cref="Task"/>-based execution.
    /// </summary>
    public interface IAsyncCommand : ICommand
    {
        /// <summary>
        /// Executes the command asynchronously.
        /// </summary>
        /// <param name="parameter">An optional parameter for the command execution.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task ExecuteAsync(object parameter);

        /// <summary>
        /// Notifies subscribers that the ability of the command to execute has changed.
        /// </summary>
        /// <remarks>Call this method to raise the CanExecuteChanged event, indicating that the result of
        /// the CanExecute method may have changed. This is typically used to update the enabled state of UI elements
        /// bound to the command.</remarks>
        void RaiseCanExecuteChanged();
    }
}
