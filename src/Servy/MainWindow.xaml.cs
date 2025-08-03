using Servy.ViewModels;
using System.Windows;
using System.Windows.Forms;

namespace Servy
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void BrowseProcessPath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
            dlg.Title = "Select process executable";

            if (dlg.ShowDialog() == true)
            {
                // Assuming your DataContext is MainViewModel and it has a ProcessPath property
                if (DataContext is MainViewModel vm)
                {
                    vm.ProcessPath = dlg.FileName;
                }
            }
        }

        private void BrowseStartupDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select startup directory",
                ShowNewFolderButton = true
            })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (DataContext is MainViewModel vm)
                    {
                        vm.StartupDirectory = dlg.SelectedPath;
                    }
                }
            }
        }
    }
}
