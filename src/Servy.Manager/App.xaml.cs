using Microsoft.Extensions.DependencyInjection;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.Validation;
using Servy.Infrastructure.Data;
using Servy.Manager.Config;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.Validation;
using Servy.Manager.ViewModels;
using Servy.Manager.Views;
using Servy.UI.Bootstrapping;
using Servy.UI.Services;
using Servy.UI.Views;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using AppConfig = Servy.Core.Config.AppConfig;

namespace Servy.Manager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class App : Application, IAppConfiguration
    {
        #region Constants

        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        public const string ResourcesNamespace = "Servy.Manager.Resources";

        #endregion

        #region Static Properties

        /// <summary>
        /// Service provider for dependency injection, built by the App constructor.
        /// Cleared on exit for test hosts.
        /// </summary>
        public static IServiceProvider? Services { get; internal set; }

        #endregion

        #region Fields

        private readonly AppBootstrapper _bootstrapper;
        private bool _isDesktopAppAvailable;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property name.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Internal Properties

        internal AppDbContext? DbContext => _bootstrapper.DbContext;
        internal IServiceRepository? ServiceRepository => _bootstrapper.ServiceRepository;
        internal ISecureData? SecureData => _bootstrapper.SecureData;

        #endregion

        #region Properties

        /// <summary>
        /// Connection string.
        /// </summary>
        public string? ConnectionString => _bootstrapper.ConnectionString;

        /// <summary>
        /// Gets the file path for the AES encryption key.
        /// </summary>
        public string? AESKeyFilePath => _bootstrapper.AESKeyFilePath;

        /// <summary>
        /// Gets the file path for the AES initialization vector (IV).
        /// </summary>
        public string? AESIVFilePath => _bootstrapper.AESIVFilePath;

        /// <inheritdoc />
        public int RefreshIntervalInSeconds { get; private set; }

        /// <inheritdoc />
        public int PerformanceRefreshIntervalInMs { get; private set; }

        /// <inheritdoc />
        public int ConsoleRefreshIntervalInMs { get; private set; }

        /// <inheritdoc />
        public int ConsoleMaxLines { get; private set; }

        /// <inheritdoc />
        public string? DesktopAppPublishPath { get; private set; }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public int DependenciesRefreshIntervalInMs { get; private set; }

        /// <inheritdoc />
        public bool ForceSoftwareRendering => _bootstrapper.ForceSoftwareRendering;

        /// <summary>
        /// Gets the configured logging verbosity level loaded from <c>appsettings.manager.json</c>.
        /// </summary>
        public LogLevel LogLevel { get; private set; }

        /// <inheritdoc />
        public int LogsWindowDays { get; private set; }

        /// <inheritdoc />
        public int SearchDebounceDelayMs { get; private set; }

        /// <inheritdoc />
        public int MaxBulkOperationParallelism { get; private set; }

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
            var services = new ServiceCollection();

            // Register dependencies
            services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
            services.AddSingleton<IProcessHelper, ProcessHelper>();
            services.AddSingleton<IProcessKiller, ProcessKiller>();

            Services = services.BuildServiceProvider();

            var options = new BootstrapperOptions
            {
                LogFileName = "Servy.Manager.log",
                AppSettingsFileName = "appsettings.manager.json",
                ResourcesNamespace = ResourcesNamespace,
                SecurityWarningTitle = Strings.Msg_SecurityWarningTitle,
                SecurityWarningMessage = Strings.Msg_SecurityWarningMessage,
                SqliteVersionWarningTitle = Strings.Msg_SqliteVersionWarningTitle,
                SqliteVersionWarningMessageFormat = Strings.Msg_SqliteVersionWarningMessage,
                ResourceExtractionWarningTitle = UI.Resources.Strings.Msg_ResourceExtractionWarningTitle,
                SplashWindowFactory = () => new SplashWindow(Strings.Splash_Text),
                // CRITICAL: The composition root is here, not in the View.
                MainWindowFactoryAsync = (_) =>
                {
                    // 0. Retrieve DI-managed services
                    var processHelper = Services.GetRequiredService<IProcessHelper>();
                    var processKiller = Services.GetRequiredService<IProcessKiller>();

                    // 1. Initialize Infrastructure & Services
                    Func<string, IServiceControllerWrapper> controllerFactory = name => new ServiceControllerWrapper(name);
                    var serviceManager = new ServiceManager(
                        controllerFactory,
                        new ServiceControllerProvider(controllerFactory),
                        new WindowsServiceApi(),
                        new Win32ErrorProvider(),
                        ServiceRepository!
                    );

                    var fileDialogService = new FileDialogService();
                    var uiDispatcher = Services.GetRequiredService<IUiDispatcher>();
                    var messageBoxService = new MessageBoxService(uiDispatcher);
                    var helpService = new HelpService(messageBoxService);
                    var serviceValidationRules = new ServiceValidationRules(processHelper);
                    var serviceConfigurationValidator = new ServiceConfigurationValidator(messageBoxService, serviceValidationRules);
                    var eventLogService = new EventLogService(new EventLogReader());
                    var cursorService = new CursorService();

                    // Break the circular dependency using local proxy functions
                    MainViewModel? viewModel = null;
                    Action<string> removeServiceProxy = (name) => viewModel?.RemoveService(name);
                    Func<Task> refreshProxy = () => viewModel != null ? viewModel.RefreshAsync() : Task.CompletedTask;

                    var serviceCommands = new ServiceCommands(
                        serviceManager,
                        ServiceRepository!,
                        messageBoxService,
                        fileDialogService,
                        removeServiceProxy,
                        refreshProxy,
                        serviceConfigurationValidator,
                        new XmlServiceValidator(serviceValidationRules),
                        new JsonServiceValidator(serviceValidationRules),
                        new XmlServiceSerializer(),
                        new JsonServiceSerializer(),
                        this, // IAppConfiguration
                        processHelper,
                        uiDispatcher // Pass the UI Dispatcher
                    );

                    // 2. Initialize Main ViewModel
                    viewModel = new MainViewModel(
                        serviceManager,
                        ServiceRepository!,
                        serviceCommands,
                        helpService,
                        messageBoxService,
                        new PerformanceViewModel(ServiceRepository!, serviceCommands, this, cursorService, processHelper, uiDispatcher),
                        new ConsoleViewModel(ServiceRepository!, serviceCommands, messageBoxService, this, cursorService, uiDispatcher),
                        new DependenciesViewModel(ServiceRepository!, serviceManager, serviceCommands, this, cursorService, uiDispatcher, messageBoxService),
                        new LogsViewModel(this, eventLogService, cursorService, messageBoxService),
                        this,
                        cursorService,
                        processHelper
                    );

                    // 3. Inject Dependencies into the View
                    var main = new MainWindow(viewModel, messageBoxService, processKiller);
                    main.Show();

                    return Task.FromResult<Window>(main);
                },
                CustomConfigAction = (config) =>
                {
                    // Extract Manager-specific polling and refresh intervals with strict centralized bounds-checking
                    RefreshIntervalInSeconds = ConfigParser.GetConfigInt(config, "RefreshIntervalInSeconds",
                        AppConfig.DefaultRefreshIntervalInSeconds,
                        AppConfig.MinRefreshIntervalInSeconds,
                        AppConfig.MaxRefreshIntervalInSeconds);

                    PerformanceRefreshIntervalInMs = ConfigParser.GetConfigInt(config, "PerformanceRefreshIntervalInMs",
                        AppConfig.DefaultPerformanceRefreshIntervalInMs,
                        AppConfig.MinPerformanceRefreshIntervalInMs,
                        AppConfig.MaxPerformanceRefreshIntervalInMs);

                    ConsoleRefreshIntervalInMs = ConfigParser.GetConfigInt(config, "ConsoleRefreshIntervalInMs",
                        AppConfig.DefaultConsoleRefreshIntervalInMs,
                        AppConfig.MinConsoleRefreshIntervalInMs,
                        AppConfig.MaxConsoleRefreshIntervalInMs);

                    ConsoleMaxLines = ConfigParser.GetConfigInt(config, "ConsoleMaxLines",
                        AppConfig.DefaultConsoleMaxLines,
                        AppConfig.MinConsoleMaxLines,
                        AppConfig.MaxConsoleMaxLines);

                    DependenciesRefreshIntervalInMs = ConfigParser.GetConfigInt(config, "DependenciesRefreshIntervalInMs",
                        AppConfig.DefaultDependenciesRefreshIntervalInMs,
                        AppConfig.MinDependenciesRefreshIntervalInMs,
                        AppConfig.MaxDependenciesRefreshIntervalInMs);

                    SearchDebounceDelayMs = ConfigParser.GetConfigInt(config, "SearchDebounceDelayMs",
                        AppConfig.DefaultSearchDebounceDelayMs,
                        AppConfig.MinSearchDebounceDelayMs,
                        AppConfig.MaxSearchDebounceDelayMs);

                    MaxBulkOperationParallelism = ConfigParser.GetConfigInt(config, "MaxBulkOperationParallelism",
                        AppConfig.DefaultMaxBulkOperationParallelism,
                        AppConfig.MinMaxBulkOperationParallelism,
                        AppConfig.MaxMaxBulkOperationParallelism);

                    LogsWindowDays = ConfigParser.GetConfigInt(config, "LogsWindowDays",
                        AppConfig.DefaultLogsWindowDays,
                        AppConfig.MinLogsWindowDays,
                        AppConfig.MaxLogsWindowDays);

                    LogLevel = ConfigParser.ParseEnum(config["LogLevel"], AppConfig.DefaultLogLevel, "LogLevel");

#if DEBUG
                    DesktopAppPublishPath = AppConfig.DesktopAppPublishReleasePath;
#else
                    var baseDirectory = AppFoldersHelper.GetAppDirectory();
                    string configuredPath = config["DesktopAppPublishPath"] ?? AppConfig.DefaultDesktopAppPublishPath;

                    if (PathSecurityGuard.IsSafelyContainedWithinAppDirectory(configuredPath, baseDirectory))
                    {
                        DesktopAppPublishPath = Helper.IsAbsolute(configuredPath)
                            ? Path.GetFullPath(configuredPath)
                            : Path.GetFullPath(Path.Combine(baseDirectory, configuredPath));
                    }
                    else
                    {
                        Logger.Warn($"Security refusal: DesktopAppPublishPath '{configuredPath}' is not contained within application directory '{baseDirectory}'.");
                        DesktopAppPublishPath = string.Empty;
                    }
#endif
                    IsDesktopAppAvailable = !string.IsNullOrEmpty(DesktopAppPublishPath) && File.Exists(DesktopAppPublishPath);
                    if (!IsDesktopAppAvailable)
                    {
                        Logger.Warn($"Desktop app executable not found: {DesktopAppPublishPath}");
                    }
                    else
                    {
                        // Perform diagnostic ACL check on target executable directory
                        PathSecurityGuard.IsDirectoryAclHardened(DesktopAppPublishPath);
                    }
                }
            };

            _bootstrapper = new AppBootstrapper(
                options,
                Services.GetRequiredService<IProcessKiller>()
                );
        }

        #endregion

        #region Application Lifecycle

        /// <summary>
        /// Called when the WPF application starts.
        /// Loads configuration settings, initializes the database, and fire-and-forgets
        /// the asynchronous application initialization.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            if (Services == null)
            {
                throw new InvalidOperationException("Service provider is not initialized.");
            }

            // 1. Run base bootstrapper startup (initializes configuration and logger)
            // If OnStartup returns false, it has already called app.Shutdown() internally.
            if (!_bootstrapper.OnStartup(this, e))
            {
                return; // Short-circuit to avoid initializing DB and extracting resources
            }

            base.OnStartup(e);

            // 2. SAFE START: Start the monitor now that _bootstrapper is guaranteed to be initialized
            // and configuration has been processed by the call above.
            _ = _bootstrapper.StartAvailabilityMonitorAsync(DesktopAppPublishPath, isAvailable => IsDesktopAppAvailable = isAvailable, this);

            // 3. Fire-and-forget initialization
            // Use a dedicated async method instead of a chained ContinueWith
            // to ensure the startup lifecycle and any faults are correctly observed.
            _ = _bootstrapper.InitializeAppWithFaultHandlingAsync(this, e, Config.UiAppConfig.Caption);
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
                _bootstrapper.OnExit(e); // disposal warnings still logged
            }
            finally
            {
                (Services as IDisposable)?.Dispose();
                Services = null; // clear the static reference for test hosts
                try { Logger.Shutdown(); } catch { /* fail-silent */ }
                base.OnExit(e);
            }
        }

        #endregion

    }
}
