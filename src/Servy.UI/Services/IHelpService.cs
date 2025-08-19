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
        void OpenDocumentation();

        /// <summary>
        /// Checks for application updates and optionally displays a message box with the result.
        /// </summary>
        /// <param name="caption">The caption to use for any message box displayed during the update check.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task CheckUpdates(string caption);

        /// <summary>
        /// Opens an "About" dialog showing application information.
        /// </summary>
        /// <param name="about">The content to display in the About dialog.</param>
        /// <param name="caption">The caption of the About dialog window.</param>
        Task OpenAboutDialog(string about, string caption);
    }
}
