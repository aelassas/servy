namespace Servy.UI.Services
{
    /// <summary>
    /// Provides an abstraction for file and folder dialog operations.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Opens a file dialog to select an executable file.
        /// </summary>
        /// <param name="title">Optional custom title for the dialog.</param>
        /// <returns>The selected file path or null if canceled.</returns>
        string OpenExecutable(string title = null);

        /// <summary>
        /// Opens a file dialog to select an XML configuration file.
        /// </summary>
        /// <param name="title">Optional custom title for the dialog.</param>
        /// <returns>The selected file path or null if canceled.</returns>
        string OpenXml(string title = null);

        /// <summary>
        /// Opens a file dialog to select a JSON configuration file.
        /// </summary>
        /// <param name="title">Optional custom title for the dialog.</param>
        /// <returns>The selected file path or null if canceled.</returns>
        string OpenJson(string title = null);

        /// <summary>
        /// Opens a folder browser dialog to select a startup directory.
        /// </summary>
        /// <param name="title">Optional custom description/title for the folder browser dialog.</param>
        /// <returns>The selected folder path or null if canceled.</returns>
        string OpenFolder(string title = null);

        /// <summary>
        /// Opens a save file dialog with a specified title.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <returns>The selected file path or null if canceled.</returns>
        string SaveFile(string title);

        /// <summary>
        /// Opens a save XML configuration file dialog with a specified title.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <returns>The selected file path or null if canceled.</returns>
        string SaveXml(string title);

        /// <summary>
        /// Opens a save JSON configuration file dialog with a specified title.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <returns>The selected file path or null if canceled.</returns>
        string SaveJson(string title);
    }
}
