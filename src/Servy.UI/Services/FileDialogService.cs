namespace Servy.UI.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IFileDialogService"/> that uses standard Windows dialogs.
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <inheritdoc />
        public string OpenExecutable()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select process executable"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <inheritdoc />
        public string OpenXml()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Select XML file"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <inheritdoc />
        public string OpenJson()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select JSON file"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <inheritdoc />
        public string OpenFolder()
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
        public string SaveFile(string title)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = "All files (*.*)|*.*"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <inheritdoc />
        public string SaveXml(string title)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <inheritdoc />
        public string SaveJson(string title)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}
