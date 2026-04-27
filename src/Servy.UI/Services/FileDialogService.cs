namespace Servy.UI.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IFileDialogService"/> that uses standard Windows dialogs.
    /// </summary>
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
            var dlg = new Microsoft.Win32.OpenFileDialog
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
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                Title = title
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        #endregion

        /// <inheritdoc />
        public string? OpenExecutable() =>
            ShowOpenDialog("Executable files (*.exe)|*.exe|All files (*.*)|*.*", "Select process executable");

        /// <inheritdoc />
        public string? OpenXml() =>
            ShowOpenDialog("XML files (*.xml)|*.xml|All files (*.*)|*.*", "Select XML file");

        /// <inheritdoc />
        public string? OpenJson() =>
            ShowOpenDialog("JSON files (*.json)|*.json|All files (*.*)|*.*", "Select JSON file");

        /// <inheritdoc />
        public string? OpenFolder()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select startup directory",
                ShowNewFolderButton = true
            })
            {
                return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null;
            }
        }

        /// <inheritdoc />
        public string? SaveFile(string title) =>
            ShowSaveDialog("All files (*.*)|*.*", title);

        /// <inheritdoc />
        public string? SaveXml(string title) =>
            ShowSaveDialog("XML files (*.xml)|*.xml|All files (*.*)|*.*", title);

        /// <inheritdoc />
        public string? SaveJson(string title) =>
            ShowSaveDialog("JSON files (*.json)|*.json|All files (*.*)|*.*", title);
    }
}