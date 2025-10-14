using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Helpers;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Services;
using Servy.UI.Services;
using Servy.Validators;
using Servy.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace Servy.Views
{
    /// <summary>
    /// Interaction logic for <see cref="MainWindow"/>.
    /// Represents the main window of the Servy application.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _mainViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class,
        /// sets up the UI components and initializes the DataContext with the main ViewModel.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _mainViewModel = CreateMainViewModel();
            DataContext = _mainViewModel;
        }

        /// <summary>
        /// Load current service configuration based on windows service name.
        /// </summary>
        /// <param name="serviceName">Service Name.</param>
        /// <returns>A task representing the asynchronous load operation.</returns>
        public async Task LoadServiceConfiguration(string serviceName)
        {
            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                await _mainViewModel.LoadServiceConfiguration(serviceName);
            }
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
                serviceRepository,
                new WmiSearcher()
            );

            // Initialize ViewModel
            var messageBoxService = new MessageBoxService();
            var helperService = new HelpService(messageBoxService);

            var mainViewModel = new MainViewModel(
                new FileDialogService(),
                null,
                messageBoxService,
                serviceRepository,
                helperService
            );

            // Initialize service commands
            var fileDialogService = new FileDialogService();

            var serviceCommands = new ServiceCommands(
                mainViewModel.ModelToServiceDto,
                mainViewModel.BindServiceDtoToModel,
                serviceManager,
                messageBoxService,
                fileDialogService,
                new ServiceConfigurationValidator(messageBoxService)
                );

            mainViewModel.ServiceCommands = serviceCommands;

            // Create main ViewModel
            return mainViewModel;
        }

        /// <summary>
        /// Handles the <see cref="Window.Closed"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> object that contains the event data.</param>
        /// <remarks>
        /// This override ensures that when the main window is closed, all child processes
        /// spawned by the current process are terminated. This prevents orphaned processes
        /// from remaining in the system after the application exits.
        /// 
        /// The method retrieves the current process ID and passes it to
        /// <see cref="ProcessKiller.KillChildren(int)"/> to terminate all descendants.
        /// Any exceptions thrown during this cleanup are caught and logged for debugging.
        /// </remarks>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                var currentPID = Process.GetCurrentProcess().Id;
                ProcessKiller.KillChildren(currentPID);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error killing child processes: {ex}");
            }

            base.OnClosed(e);
        }
    }
}
