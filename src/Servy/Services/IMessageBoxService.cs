namespace Servy.Services
{
    /// <summary>
    /// Provides methods to display message boxes to the user.
    /// </summary>
    public interface IMessageBoxService
    {
        /// <summary>
        /// Shows an informational message box with the specified message and caption.
        /// </summary>
        /// <param name="message">The message text to display.</param>
        /// <param name="caption">The caption/title of the message box.</param>
        void ShowInfo(string message, string caption);

        /// <summary>
        /// Shows a warning message box with the specified message and caption.
        /// </summary>
        /// <param name="message">The warning message text to display.</param>
        /// <param name="caption">The caption/title of the message box.</param>
        void ShowWarning(string message, string caption);

        /// <summary>
        /// Shows an error message box with the specified message and caption.
        /// </summary>
        /// <param name="message">The error message text to display.</param>
        /// <param name="caption">The caption/title of the message box.</param>
        void ShowError(string message, string caption);
    }
}
