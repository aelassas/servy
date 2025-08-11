using Servy.Core.Services;
using Servy.Services;
using Servy.ViewModels;
using System.Windows;
using System.Windows.Controls;

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
                        name => new ServiceControllerWrapper(name),
                        new WindowsServiceApi(),
                        new Win32ErrorProvider()
                        ),
                    new MessageBoxService()));
        }

        /// <summary>
        /// Handles the PasswordChanged event of the PasswordBox for the main password.
        /// Updates the ViewModel's Password property with the current password value.
        /// </summary>
        /// <param name="sender">The PasswordBox that raised the event.</param>
        /// <param name="e">The routed event data.</param>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }

        /// <summary>
        /// Handles the PasswordChanged event of the PasswordBox for the confirm password.
        /// Updates the ViewModel's ConfirmPassword property with the current password value.
        /// </summary>
        /// <param name="sender">The PasswordBox that raised the event.</param>
        /// <param name="e">The routed event data.</param>
        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is PasswordBox pb)
            {
                vm.ConfirmPassword = pb.Password;
            }
        }

    }
}
