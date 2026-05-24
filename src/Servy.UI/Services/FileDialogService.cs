using Microsoft.Win32;
using Servy.Core.Resources;
using System.Diagnostics.CodeAnalysis;

namespace Servy.UI.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IFileDialogService"/> that uses standard Windows dialogs.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class FileDialogService : IFileDialogService
    {
        #region Private Helpers

        /// <summary>
        /// Displays a standard Windows Open File dialog with the specified filter and title.
        /// </summary>
        /// <param name="filter">The file extension filter string (e.g., "Text files (*.txt)|*.txt").</param>
        /// <param name="title">The text to display in the title bar of the dialog.</param>
        /// <returns>The full path of the selected file if the user clicks OK; otherwise, <see langword="null"/>.</returns>
        private static string? ShowOpenDialog(string filter, string title)
        {
            var dlg = new OpenFileDialog
            {
                Filter = filter,
                Title = title
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <summary>
        /// Displays a standard Windows Save File dialog with the specified filter and title.
        /// </summary>
        /// <param name="filter">The file extension filter string (e.g., "JSON files (*.json)|*.json").</param>
        /// <param name="title">The text to display in the title bar of the dialog.</param>
        /// <returns>The full path where the file should be saved if the user clicks OK; otherwise, <see langword="null"/>.</returns>
        private static string? ShowSaveDialog(string filter, string title)
        {
            var dlg = new SaveFileDialog
            {
                Filter = filter,
                Title = title
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        #endregion

        /// <inheritdoc />
        public string? OpenExecutable() =>
            ShowOpenDialog(Strings.FileFilter_Executable, Strings.Title_SelectExecutable);

        /// <inheritdoc />
        public string? OpenXml() =>
            ShowOpenDialog(Strings.FileFilter_Xml, Strings.Title_SelectXml);

        /// <inheritdoc />
        public string? OpenJson() =>
            ShowOpenDialog(Strings.FileFilter_Json, Strings.Title_SelectJson);

        /// <inheritdoc />
        public string? OpenFolder()
        {
            var dlg = new OpenFolderDialog
            {
                Title = Strings.Title_SelectStartupDirectory,
            };
            return dlg.ShowDialog() == true ? dlg.FolderName : null;
        }

        /// <inheritdoc />
        public string? SaveFile(string title) =>
            ShowSaveDialog(Strings.FileFilter_AllFiles, title);

        /// <inheritdoc />
        public string? SaveXml(string title) =>
            ShowSaveDialog(Strings.FileFilter_Xml, title);

        /// <inheritdoc />
        public string? SaveJson(string title) =>
            ShowSaveDialog(Strings.FileFilter_Json, title);
    }
}