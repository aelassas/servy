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
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation. The task completes without
        /// invoking the command when an execution is already in progress, or when the command's
        /// <see cref="ICommand.CanExecute"/> predicate rejects the parameter;
        /// callers that need to know whether the command ran should check
        /// <see cref="ICommand.CanExecute"/> first.
        /// </returns>
        Task ExecuteAsync(object? parameter);

        /// <summary>
        /// Requests a re-evaluation of command bindings so UI elements can update their enabled state.
        /// </summary>
        /// <remarks>
        /// Implemented via <see cref="CommandManager.InvalidateRequerySuggested"/>, which is global:
        /// one call re-queries every command binding on the calling thread, not only this command.
        /// Must be called on the UI thread - <see cref="CommandManager"/> is thread-affine, and a call
        /// from a background thread is a silent no-op.
        /// </remarks>
        void RaiseCanExecuteChanged();
    }
}
