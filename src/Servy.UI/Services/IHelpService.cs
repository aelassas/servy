using System.Threading;
using System.Threading.Tasks;

namespace Servy.UI.Services
{
    /// <summary>
    /// Provides methods to show help-related UI elements and perform update checks.
    /// </summary>
    public interface IHelpService
    {
        /// <summary>
        /// Opens the documentation for the application.
        /// </summary>
        /// <param name="caption">The caption to use for any message box displayed if the documentation cannot be opened.</param>
        /// <param name="cancellationToken">A token that cancels the operation, typically the application lifetime token.</param>
        Task OpenDocumentationAsync(string caption, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks for application updates and reports the result to the user in a message box:
        /// an information dialog when up to date, a confirmation to open the download page when
        /// a newer release exists, or an error dialog when the check fails or times out.
        /// </summary>
        /// <param name="caption">The caption to use for any message box displayed during the update check.</param>
        /// <param name="cancellationToken">A token that cancels the update check, typically the application lifetime token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task CheckUpdatesAsync(string caption, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens an "About" dialog showing application information.
        /// </summary>
        /// <param name="about">The content to display in the About dialog.</param>
        /// <param name="caption">The caption of the About dialog window.</param>
        /// <param name="cancellationToken">A token that cancels the operation, typically the application lifetime token.</param>
        Task OpenAboutDialogAsync(string about, string caption, CancellationToken cancellationToken = default);
    }
}
