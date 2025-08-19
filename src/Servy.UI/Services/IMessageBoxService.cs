using System.Threading.Tasks;

namespace Servy.UI.Services
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
        Task ShowInfoAsync(string message, string caption);

        /// <summary>
        /// Shows a warning message box with the specified message and caption.
        /// </summary>
        /// <param name="message">The warning message text to display.</param>
        /// <param name="caption">The caption/title of the message box.</param>
        Task ShowWarningAsync(string message, string caption);

        /// <summary>
        /// Shows an error message box with the specified message and caption.
        /// </summary>
        /// <param name="message">The error message text to display.</param>
        /// <param name="caption">The caption/title of the message box.</param>
        Task ShowErrorAsync(string message, string caption);

        /// <summary>
        /// Displays a confirmation dialog with the specified message and caption.
        /// </summary>
        /// <param name="message">The text to display in the dialog.</param>
        /// <param name="caption">The title of the dialog window.</param>
        /// <returns>
        /// <c>true</c> if the user confirms (e.g., clicks Yes/OK); otherwise, <c>false</c>.
        /// </returns>
        Task<bool> ShowConfirmAsync(string message, string caption);

    }
}
