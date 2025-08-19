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
        /// <returns>The selected file path or null if canceled.</returns>
        string OpenExecutable();

        /// <summary>
        /// Opens a file dialog to select an XML configuration file.
        /// </summary>
        /// <returns>The selected file path or null if canceled.</returns>
        string OpenXml();

        /// <summary>
        /// Opens a file dialog to select an JSON configuration file.
        /// </summary>
        /// <returns>The selected file path or null if canceled.</returns>
        string OpenJson();

        /// <summary>
        /// Opens a folder browser dialog to select a startup directory.
        /// </summary>
        /// <returns>The selected folder path or null if canceled.</returns>
        string OpenFolder();

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
