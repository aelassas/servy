namespace Servy.UI.Services
{
    /// <summary>
    /// Provides centralized management for the application's mouse cursor state, 
    /// abstracting away direct WPF Mouse.OverrideCursor dependencies for better testability.
    /// </summary>
    public interface ICursorService
    {
        /// <summary>
        /// Sets the application cursor to the Wait (hourglass/spinner) cursor.
        /// </summary>
        void SetWaitCursor();

        /// <summary>
        /// Explicitly clears any active cursor overrides, returning the cursor to normal.
        /// </summary>
        void ResetCursor();
    }
}