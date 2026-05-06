using System;
using System.Threading.Tasks;

namespace Servy.UI.Services
{
    /// <summary>
    /// Defines an abstraction for UI dispatcher operations to decouple ViewModels 
    /// from specific UI framework threading models.
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>
        /// Asynchronously executes the specified <see cref="Action"/> on the UI thread.
        /// </summary>
        /// <param name="action">The delegate to execute.</param>
        /// <returns>A task that represents the completion of the action.</returns>
        Task InvokeAsync(Action action);

        /// <summary>
        /// Asynchronously executes the specified <see cref="Func{T}"/> on the UI thread and returns the result.
        /// </summary>
        /// <typeparam name="T">The type of the return value.</typeparam>
        /// <param name="callback">The delegate to execute.</param>
        /// <returns>A task that represents the completion of the delegate and contains the result.</returns>
        Task<T> InvokeAsync<T>(Func<T> callback);

        /// <summary>
        /// Asynchronously yields execution back to the UI dispatcher, 
        /// allowing the UI to process pending events (like repaints or input) 
        /// before proceeding with the current operation.
        /// </summary>
        /// <returns>A task representing the yielding operation.</returns>
        Task YieldAsync();
    }
}