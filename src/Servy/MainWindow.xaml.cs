using Servy.Core.Services;
using Servy.Services;
using Servy.ViewModels;
using System.Windows;

namespace Servy
{
    /// <summary>
    /// Interaction logic for <see cref="MainWindow"/>.
    /// Represents the main window of the Servy application.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class,
        /// sets up the UI components and initializes the DataContext with the main ViewModel.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(
                new FileDialogService(),
                new ServiceCommands(
                    new ServiceManager(
                        _ => new ServiceControllerWrapper(MainViewModel.DefaultServiceName),
                        new WindowsServiceApi(),
                        new Win32ErrorProvider()
                        ),
                    new MessageBoxService()));
        }
    }
}
