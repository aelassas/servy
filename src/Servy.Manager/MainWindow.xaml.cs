using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Helpers;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Servy.Manager
{
    /// <summary>
    /// Interaction logic for <see cref="MainWindow"/>.
    /// Represents the main window of the Servy Manager application.
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
            DataContext = CreateMainViewModel();
        }

        /// <summary>
        /// Creates and configures the <see cref="MainViewModel"/> with all required dependencies.
        /// </summary>
        /// <returns>A fully initialized <see cref="MainViewModel"/> instance.</returns>
        private MainViewModel CreateMainViewModel()
        {
            var app = (App)Application.Current;

            // Initialize database and helpers
            var dbContext = new AppDbContext(app.ConnectionString);
            DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

            var dapperExecutor = new DapperExecutor(dbContext);
            var protectedKeyProvider = new ProtectedKeyProvider(app.AESKeyFilePath, app.AESIVFilePath);
            var securePassword = new SecurePassword(protectedKeyProvider);
            var xmlSerializer = new XmlServiceSerializer();

            var serviceRepository = new ServiceRepository(dapperExecutor, securePassword, xmlSerializer);

            // Initialize logger
            var logger = new EventLogLogger(AppConfig.EventSource);

            // Initialize service manager
            var serviceManager = new ServiceManager(
                name => new ServiceControllerWrapper(name),
                new WindowsServiceApi(),
                new Win32ErrorProvider(),
                serviceRepository,
                new WmiSearcher()
            );

            // Initialize service commands and helpers
            var fileDialogService = new FileDialogService();
            var messageBoxService = new MessageBoxService();
            var helpService = new HelpService(messageBoxService);

            // Create main ViewModel
            var viewModel = new MainViewModel(
                logger,
                serviceManager,
                serviceRepository,
                null,                   // We'll set ServiceCommands next
                helpService
            );

            var serviceConfigurationValidator = new ServiceConfigurationValidator(messageBoxService);

            var serviceCommands = new ServiceCommands(
                serviceManager,
                serviceRepository,
                messageBoxService,
                logger,
                fileDialogService,
                viewModel.RemoveService,
                viewModel.Resfresh,
                serviceConfigurationValidator
            );

            viewModel.ServiceCommands = serviceCommands;

            return viewModel;
        }

        /// <summary>
        /// Handles the <see cref="Window.Loaded"/> event.
        /// Executes the initial search when the window is loaded.
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.SearchCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// Handles the KeyDown event of the search text box.
        /// Executes the search when the Enter key is pressed.
        /// </summary>
        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            {
                if (vm.SearchCommand.CanExecute(null))
                    await vm.SearchCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Handles PreviewMouseLeftButtonDown for row action buttons (Start, Stop, Restart).
        /// Executes the associated command manually.
        /// </summary>
        private void ActionButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is Button btn && btn.Command != null)
            {
                var parameter = btn.CommandParameter ?? DataContext;
                if (btn.Command.CanExecute(parameter))
                    btn.Command.Execute(parameter);
            }
        }

        /// <summary>
        /// Handles PreviewMouseLeftButtonDown for menu buttons (⋮).
        /// Opens the associated ContextMenu and sets its DataContext.
        /// </summary>
        private void MenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.DataContext = btn.DataContext;

                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Handles KeyDown on the window for global shortcuts.
        /// Executes search when F5 is pressed.
        /// </summary>
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 && DataContext is MainViewModel vm)
            {
                if (vm.SearchCommand.CanExecute(null))
                    await vm.SearchCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Handles the Config menu click.
        /// Executes the configure command for the main ViewModel.
        /// </summary>
        private async void Menu_ConfigClik(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.ConfigureCommand.ExecuteAsync(null);
        }
    }
}
