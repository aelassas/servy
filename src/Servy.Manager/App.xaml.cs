using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Manager.Resources;
using Servy.Manager.Validators;
using Servy.Manager.ViewModels;
using Servy.Manager.Services;
using Servy.Manager.Views;
using Servy.UI.Bootstrapping;
using Servy.UI.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
#if !DEBUG
using Servy.Core.Helpers;
using System.Diagnostics;
#endif
using System.IO;
using System.Windows;
using System;
using System.Threading.Tasks;

namespace Servy.Manager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, INotifyPropertyChanged
    {
        #region Constants

        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        public const string ResourcesNamespace = "Servy.Manager.Resources";

        #endregion

        #region Fields

        private readonly AppBootstrapper _bootstrapper;
        private bool _isDesktopAppAvailable;
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
        /// Service status refresh interval in seconds.
        /// </summary>
        public int RefreshIntervalInSeconds { get; private set; }

        /// <summary>
        /// Performance refresh interval in seconds.
        /// </summary>
        public int PerformanceRefreshIntervalInMs { get; private set; }

        /// <summary>
        /// Console refresh interval in seconds.
        /// </summary>
        public int ConsoleRefreshIntervalInMs { get; private set; }

        /// <summary>
        /// Gets the maximum number of lines that can be displayed in the console output.
        /// </summary>
        public int ConsoleMaxLines { get; private set; }

        /// <summary>
        /// Servy Desktop App publish path.
        /// </summary>
        public string DesktopAppPublishPath { get; private set; }

        /// <summary>
        /// Indicates whether the configuration application is available.
        /// Dynamically updates if the application is installed or removed during runtime.
        /// </summary>
        public bool IsDesktopAppAvailable
        {
            get => _isDesktopAppAvailable;
            private set
            {
                if (_isDesktopAppAvailable != value)
                {
                    _isDesktopAppAvailable = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Dependencis tab refresh interval in seconds.
        /// </summary>
        public int DependenciesRefreshIntervalInMs { get; private set; }

        /// <summary>
        /// Gets a value indicating whether software rendering has been forced for the current session.
        /// </summary>
        /// <remarks>
        /// This property is typically set during application startup if the system detects 
        /// a remote session, low-tier graphics hardware, or if the <see cref="AppConfig.ForceSoftwareRenderingArg"/> 
        /// command-line argument is present.
        /// </remarks>
        public bool ForceSoftwareRendering => _bootstrapper.ForceSoftwareRendering;

        /// <summary>
        /// Log level for the application, loaded from configuration. This determines the verbosity of logs.
        /// </summary>
        public LogLevel LogLevel { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class for the Servy Manager.
        /// </summary>
        /// <remarks>
        /// This constructor sets up the <see cref="BootstrapperOptions"/> specifically for the monitoring 
        /// and management interface. It configures the manager-specific log file, JSON settings, 
        /// and extracts specialized UI parameters such as performance polling intervals and 
        /// console line limits from the configuration provider.
        /// </remarks>
        public App()
        {
            var options = new BootstrapperOptions
            {
                LogFileName = "Servy.Manager.log",
                ResourcesNamespace = ResourcesNamespace,
                SecurityWarningTitle = Strings.SecurityWarningTitle,
                SecurityWarningMessage = Strings.SecurityWarningMessage,
                SqliteVersionWarningTitle = Strings.SqliteVersionWarningTitle,
                SqliteVersionWarningMessageFormat = Strings.SqliteVersionWarningMessage,
                SplashWindowFactory = () => new SplashWindow(),
                // CRITICAL: The composition root is here, not in the View.
                MainWindowFactoryAsync = (serviceName) =>
                {
                    // 1. Initialize Infrastructure & Services
                    Func<string, IServiceControllerWrapper> controllerFactory = name => new ServiceControllerWrapper(name);
                    var serviceManager = new ServiceManager(
                        controllerFactory,
                        new ServiceControllerProvider(controllerFactory),
                        new WindowsServiceApi(),
                        new Win32ErrorProvider(),
                        ServiceRepository
                    );

                    var fileDialogService = new FileDialogService();
                    var messageBoxService = new MessageBoxService();
                    var helpService = new HelpService(messageBoxService);
                    var serviceConfigurationValidator = new ServiceConfigurationValidator(messageBoxService);
                    var eventLogService = new EventLogService(new EventLogReader());

                    // 2. Initialize Standalone ViewModels
                    var logsVm = new LogsViewModel(eventLogService);

                    // Break the circular dependency using local proxy functions
                    MainViewModel viewModel = null;
                    Action<string> removeServiceProxy = (name) => viewModel?.RemoveService(name);
                    Func<Task> refreshProxy = () => viewModel != null ? viewModel.Refresh() : Task.CompletedTask;

                    var serviceCommands = new ServiceCommands(
                        serviceManager,
                        ServiceRepository,
                        messageBoxService,
                        fileDialogService,
                        removeServiceProxy,
                        refreshProxy,
                        serviceConfigurationValidator,
                        new XmlServiceValidator(),
                        new JsonServiceValidator()
                    );

                    // 3. Initialize Main ViewModel
                    viewModel = new MainViewModel(
                        serviceManager,
                        ServiceRepository,
                        serviceCommands,
                        helpService,
                        messageBoxService,
                        new PerformanceViewModel(ServiceRepository, serviceCommands),
                        new ConsoleViewModel(ServiceRepository, serviceCommands),
                        new DependenciesViewModel(ServiceRepository, serviceManager, serviceCommands)
                    );

                    // 4. Inject Dependencies into the View
                    var main = new MainWindow(viewModel, logsVm, messageBoxService);
                    main.Show();

                    return Task.FromResult<Window>(main);
                },
                CustomConfigAction = (config) =>
                {
                    // Extract Manager-specific polling and refresh intervals
                    RefreshIntervalInSeconds = int.TryParse(config["RefreshIntervalInSeconds"], out var r) ? r : AppConfig.DefaultRefreshIntervalInSeconds;
                    PerformanceRefreshIntervalInMs = int.TryParse(config["PerformanceRefreshIntervalInMs"], out var pr) ? pr : AppConfig.DefaultPerformanceRefreshIntervalInMs;
                    ConsoleRefreshIntervalInMs = int.TryParse(config["ConsoleRefreshIntervalInMs"], out var cr) ? cr : AppConfig.DefaultConsoleRefreshIntervalInMs;
                    ConsoleMaxLines = int.TryParse(config["ConsoleMaxLines"], out var cml) ? cml : AppConfig.DefaultConsoleMaxLines;
                    DependenciesRefreshIntervalInMs = int.TryParse(config["DependenciesRefreshIntervalInMs"], out var dr) ? dr : AppConfig.DefaultDependenciesRefreshIntervalInMs;

                    if (Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel)) LogLevel = logLevel;
                    else LogLevel = LogLevel.Info;

#if DEBUG
                    DesktopAppPublishPath = AppConfig.DesktopAppPublishReleasePath;
#else
                    var baseDirectory = AppFoldersHelper.GetAppDirectory();
                    DesktopAppPublishPath = config["DesktopAppPublishPath"] ?? AppConfig.DefaultDesktopAppPublishPath;
                    if (!Path.IsPathRooted(DesktopAppPublishPath))
                    {
                        DesktopAppPublishPath = Path.GetFullPath(Path.Combine(baseDirectory, DesktopAppPublishPath));
                    }
#endif
                    IsDesktopAppAvailable = !string.IsNullOrEmpty(DesktopAppPublishPath) && File.Exists(DesktopAppPublishPath);
                    if (!IsDesktopAppAvailable)
                    {
                        Logger.Warn($"Desktop app executable not found: {DesktopAppPublishPath}");
                    }

                    StartAvailabilityMonitor();
                }
            };

            _bootstrapper = new AppBootstrapper(options);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Starts a real-time background watcher to monitor whether the Desktop configuration
        /// app becomes available or unavailable after Manager has started.
        /// </summary>
        private void StartAvailabilityMonitor()
        {
            if (string.IsNullOrEmpty(DesktopAppPublishPath)) return;

            string directory = Path.GetDirectoryName(DesktopAppPublishPath);
            string fileName = Path.GetFileName(DesktopAppPublishPath);

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
                if (!string.IsNullOrEmpty(DesktopAppPublishPath))
                {
                    IsDesktopAppAvailable = File.Exists(DesktopAppPublishPath);
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
            _bootstrapper.OnStartup(this, e);
            base.OnStartup(e);

            // Use a dedicated async method instead of a chained ContinueWith 
            // to ensure the startup lifecycle and any faults are correctly observed.
            _ = InitializeAppWithFaultHandlingAsync(e);
        }

        /// <summary>
        /// Asynchronously initializes the application and handles any critical faults 
        /// that occur before the UI is ready.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        /// <returns>A <see cref="Task"/> representing the initialization process.</returns>
        private async Task InitializeAppWithFaultHandlingAsync(StartupEventArgs e)
        {
            try
            {
                await _bootstrapper.InitializeAppAsync(this, e);
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