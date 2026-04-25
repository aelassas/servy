using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Resources;
using Servy.Services;
using Servy.UI.Bootstrapping;
using Servy.UI.Services;
using Servy.Validators;
using Servy.ViewModels;
using Servy.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
#if !DEBUG
using Servy.Core.Helpers;
using System.Diagnostics;
#endif
using System.IO;
using System.Windows;
using Servy.Config;
using AppConfig = Servy.Core.Config.AppConfig;
using Servy.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Servy.Core.Validators;

namespace Servy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IAppConfiguration
    {
        #region Constants

        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        public static readonly string ResourcesNamespace = "Servy.Resources";

        #endregion

        #region Static Properties

        /// <summary>
        /// Service provider for dependency injection, initialized by the bootstrapper.
        /// </summary>
        public static IServiceProvider Services { get; private set; }

        #endregion

        #region Fields

        private readonly AppBootstrapper _bootstrapper;
        private bool _isManagerAppAvailable;
        private FileSystemWatcher _availabilityWatcher;
        private FileSystemEventHandler _availabilityChangedHandler;
        private RenamedEventHandler _availabilityRenamedHandler;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property name.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Internal Properties

        internal AppDbContext DbContext => _bootstrapper.DbContext;
        internal IServiceRepository ServiceRepository => _bootstrapper.ServiceRepository;
        internal ISecureData SecureData => _bootstrapper.SecureData;

        #endregion

        #region Properties

        /// <summary>
        /// Connection string.
        /// </summary>
        public string ConnectionString => _bootstrapper.ConnectionString;

        /// <summary>
        /// Gets the file path for the AES encryption key.
        /// </summary>
        public string AESKeyFilePath => _bootstrapper.AESKeyFilePath;

        /// <summary>
        /// Gets the file path for the AES initialization vector (IV).
        /// </summary>
        public string AESIVFilePath => _bootstrapper.AESIVFilePath;

        /// <summary>
        /// Servy Manager App publish path.
        /// </summary>
        public string ManagerAppPublishPath { get; private set; }

        /// <summary>
        /// Indicates whether the Manager application is available.
        /// Dynamically updates if the application is installed or removed during runtime.
        /// </summary>
        public bool IsManagerAppAvailable
        {
            get => _isManagerAppAvailable;
            private set
            {
                if (_isManagerAppAvailable != value)
                {
                    _isManagerAppAvailable = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether software rendering has been forced for the current session.
        /// </summary>
        /// <remarks>
        /// This property is typically set during application startup if the system detects 
        /// a remote session, low-tier graphics hardware, or if the <see cref="AppConfig.ForceSoftwareRenderingArg"/> 
        /// command-line argument is present.
        /// </remarks>
        public bool ForceSoftwareRendering => _bootstrapper.ForceSoftwareRendering;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor configures the <see cref="BootstrapperOptions"/> required for the shared 
        /// <see cref="AppBootstrapper"/>. It sets up project-specific paths, localized strings, 
        /// and the factories responsible for instantiating the UI components and custom configuration logic.
        /// </remarks>
        public App()
        {
            var options = new BootstrapperOptions
            {
                LogFileName = "Servy.log",
                AppSettingsFileName = "appsettings.json",
                ResourcesNamespace = ResourcesNamespace,
                SecurityWarningTitle = Strings.SecurityWarningTitle,
                SecurityWarningMessage = Strings.SecurityWarningMessage,
                SqliteVersionWarningTitle = Strings.SqliteVersionWarningTitle,
                SqliteVersionWarningMessageFormat = Strings.SqliteVersionWarningMessage,
                SplashWindowFactory = () => new SplashWindow(),
                // CRITICAL: The composition root is here, not in the View.
                MainWindowFactoryAsync = async (serviceName) =>
                {
                    // 0. Retrieve DI-managed services
                    var processHelper = Services.GetRequiredService<IProcessHelper>();

                    // 1. Initialize Infrastructure & Domain Services
                    Func<string, IServiceControllerWrapper> controllerFactory = name => new ServiceControllerWrapper(name);
                    var serviceManager = new ServiceManager(
                        controllerFactory,
                        new ServiceControllerProvider(controllerFactory),
                        new WindowsServiceApi(),
                        new Win32ErrorProvider(),
                        this.ServiceRepository
                    );

                    // 2. Initialize UI Services
                    var fileDialogService = new FileDialogService();
                    var messageBoxService = new MessageBoxService();
                    var helperService = new HelpService(messageBoxService);
                    var serviceValidationRules = new ServiceValidationRules(processHelper);
                    var configValidator = new ServiceConfigurationValidator(messageBoxService, serviceValidationRules);

                    // 3. Resolve Circular Dependency using Proxies
                    MainViewModel viewModel = null;

                    Func<ServiceDto> modelToDtoProxy = () => viewModel?.ModelToServiceDto();
                    Action<ServiceDto> bindDtoProxy = (dto) => viewModel?.BindServiceDtoToModel(dto);


                    var serviceCommands = new ServiceCommands(
                        modelToDtoProxy,
                        bindDtoProxy,
                        serviceManager,
                        messageBoxService,
                        fileDialogService,
                        configValidator,
                        new XmlServiceValidator(processHelper),
                        new JsonServiceValidator(processHelper),
                        this,
                        new CursorService()
                    );

                    // 4. Initialize Main ViewModel
                    viewModel = new MainViewModel(
                        fileDialogService,
                        serviceCommands,
                        messageBoxService,
                        ServiceRepository,
                        helperService,
                        this
                    );

                    // 5. Inject dependencies into the View and initialize
                    var mainWindow = new MainWindow(viewModel);
                    mainWindow.Show();

                    if (!string.IsNullOrWhiteSpace(serviceName))
                    {
                        await mainWindow.LoadServiceConfiguration(serviceName);
                    }

                    return mainWindow;
                },
                CustomConfigAction = (config) =>
                {
#if DEBUG
                    ManagerAppPublishPath = AppConfig.ManagerAppPublishReleasePath;
#else
                    var baseDirectory = AppFoldersHelper.GetAppDirectory();
                    ManagerAppPublishPath = config["ManagerAppPublishPath"] ?? AppConfig.DefaultManagerAppPublishPath;
                    if (!Path.IsPathRooted(ManagerAppPublishPath))
                    {
                        ManagerAppPublishPath = Path.GetFullPath(Path.Combine(baseDirectory, ManagerAppPublishPath));
                    }
#endif
                    IsManagerAppAvailable = !string.IsNullOrEmpty(ManagerAppPublishPath) && File.Exists(ManagerAppPublishPath);
                    if (!IsManagerAppAvailable)
                    {
                        Logger.Warn($"Manager app executable not found: {ManagerAppPublishPath}");
                    }

                    StartAvailabilityMonitor();
                }
            };

            _bootstrapper = new AppBootstrapper(options);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Starts a real-time background watcher to monitor whether the Manager configuration
        /// app becomes available or unavailable after Servy has started.
        /// </summary>
        private void StartAvailabilityMonitor()
        {
            if (string.IsNullOrEmpty(ManagerAppPublishPath)) return;

            string directory = Path.GetDirectoryName(ManagerAppPublishPath);
            string fileName = Path.GetFileName(ManagerAppPublishPath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) return;

            try
            {
                // Ensure the directory exists before watching it, otherwise FileSystemWatcher throws an exception
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _availabilityWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                // Watch for all file lifecycle events to ensure we catch installations, uninstalls, and updates
                _availabilityChangedHandler = (s, e) => UpdateAvailabilityState();
                _availabilityRenamedHandler = (s, e) => UpdateAvailabilityState();
                _availabilityWatcher.Created += _availabilityChangedHandler;
                _availabilityWatcher.Deleted += _availabilityChangedHandler;
                _availabilityWatcher.Changed += _availabilityChangedHandler;
                _availabilityWatcher.Renamed += _availabilityRenamedHandler;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize FileSystemWatcher for {fileName}", ex);
            }
        }

        /// <summary>
        /// Re-evaluates the availability of the target app and safely updates the UI binding.
        /// </summary>
        private void UpdateAvailabilityState()
        {
            // Dispatch to UI thread to safely update data-bound properties from the watcher's background thread
            Current.Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(ManagerAppPublishPath))
                {
                    IsManagerAppAvailable = File.Exists(ManagerAppPublishPath);
                }
            });
        }

        #endregion

        #region Events

        /// <summary>
        /// Called when the WPF application starts.
        /// Loads configuration settings, initializes the database, and fire-and-forgets 
        /// the asynchronous application initialization.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            // Register dependencies
            services.AddSingleton<IProcessHelper, ProcessHelper>();

            Services = services.BuildServiceProvider();

            _bootstrapper.OnStartup(this, e);
            base.OnStartup(e);

            // Use a dedicated async method instead of a chained ContinueWith 
            // to ensure the startup lifecycle and any faults are correctly observed.
            _ = InitializeAppWithFaultHandlingAsync(e, Services.GetRequiredService<IProcessHelper>());
        }

        /// <summary>
        /// Asynchronously initializes the application and handles any critical faults 
        /// that occur before the UI is ready.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        /// <param name="processHelper">An instance of <see cref="IProcessHelper"/> used to query running processes.</param>
        /// <returns>A <see cref="Task"/> representing the initialization process.</returns>
        private async Task InitializeAppWithFaultHandlingAsync(StartupEventArgs e, IProcessHelper processHelper)
        {
            try
            {
                await _bootstrapper.InitializeAppAsync(this, e, processHelper);
            }
            catch (Exception ex)
            {
                // Ensure we catch the actual exception, not just a faulted task
                Logger.Error("Critical Startup Fault in InitializeApp", ex);

                // Ensure UI interaction happens on the UI thread to prevent 
                // cross-thread exceptions during the crash report.
                await Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        MessageBox.Show(
                            $"Critical Startup Fault: {ex.Message}",
                            Config.AppConfig.Caption,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    finally
                    {
                        // Hard exit to prevent the app from lingering as a background process.
                        Shutdown(1);
                    }
                });
            }
        }

        /// <summary>
        /// Raises the <see cref="Application.Exit"/> event.
        /// Performs final cleanup of sensitive data providers and encryption keys before the application terminates.
        /// </summary>
        /// <param name="e">An <see cref="ExitEventArgs"/> that contains the event data.</param>
        /// <remarks>
        /// This override ensures that <see cref="SecureData"/> (which may hold sensitive cryptographic 
        /// material or file handles for AES keys) is explicitly disposed of, following the 
        /// deterministic disposal pattern before the process exits.
        /// </remarks>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_availabilityWatcher != null)
                {
                    _availabilityWatcher.EnableRaisingEvents = false;
                    if (_availabilityChangedHandler != null)
                    {
                        _availabilityWatcher.Created -= _availabilityChangedHandler;
                        _availabilityWatcher.Deleted -= _availabilityChangedHandler;
                        _availabilityWatcher.Changed -= _availabilityChangedHandler;
                    }
                    if (_availabilityRenamedHandler != null)
                    {
                        _availabilityWatcher.Renamed -= _availabilityRenamedHandler;
                    }
                }
                _availabilityWatcher?.Dispose();
                _bootstrapper.OnExit(e);
            }
            finally
            {
                base.OnExit(e);
            }
        }

        #endregion
    }
}