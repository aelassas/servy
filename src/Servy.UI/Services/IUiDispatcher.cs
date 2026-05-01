namespace Servy.UI.Services
{
    /// <summary>
    /// Defines an abstraction for UI dispatcher operations to decouple ViewModels 
    /// from specific UI framework threading models.
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>
        /// Asynchronously yields execution back to the UI dispatcher, 
        /// allowing the UI to process pending events (like repaints) 
        /// before proceeding with the current operation.
        /// </summary>
        /// <returns>A task representing the yielding operation.</returns>
        Task YieldAsync();
    }
}