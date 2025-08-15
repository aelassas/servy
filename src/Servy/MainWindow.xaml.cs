using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Helpers;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
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

            // Initialize service manager
            var serviceManager = new ServiceManager(
                name => new ServiceControllerWrapper(name),
                new WindowsServiceApi(),
                new Win32ErrorProvider(),
                serviceRepository
            );

            // Initialize service commands
            var messageBoxService = new MessageBoxService();
            var serviceCommands = new ServiceCommands(serviceManager, messageBoxService);

            // Create main ViewModel
            return new MainViewModel(
                new FileDialogService(),
                serviceCommands,
                messageBoxService,
                new ServiceConfigurationValidator(messageBoxService)
            );
        }
    }
}
