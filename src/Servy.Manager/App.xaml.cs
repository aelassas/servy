using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.Validation;
using Servy.Manager.Config;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Manager.Views;
using Servy.UI.Bootstrapping;
using Servy.UI.Services;
using Servy.UI.Validation;
using Servy.UI.Views;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using AppConfig = Servy.Core.Config.AppConfig;

namespace Servy.Manager
{
    using Servy.UI.Localization;
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

        /// <summary>
        /// The name of the JSON configuration file specific to the Servy Manager application.
        /// </summary>
        internal const string AppSettingsFileName = "appsettings.manager.json";

        /// <summary>
        /// The configuration key that holds the persisted position, size and state of the main window.
        /// </summary>
        private const string MainWindowPlacementKey = "MainWindowPlacement";

        /// <summary>
        /// The configuration key that holds the persisted column widths of the services table.
        /// </summary>
        private const string ServicesColumnWidthsKey = "ServicesColumnWidths";

        /// <summary>
        /// The configuration key that holds the persisted column widths of the logs table.
        /// </summary>
        private const string LogsColumnWidthsKey = "LogsColumnWidths";

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
        private string _servicesHiddenColumns = string.Empty;
        private string _logsHiddenColumns = string.Empty;
        private bool _isDisplayNameColumnMigrated;

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

        internal IServiceRepository? ServiceRepository => _bootstrapper.ServiceRepository;

        #endregion

        #region Properties

        /// <inheritdoc />
        public int RefreshIntervalInSeconds { get; private set; }

        /// <inheritdoc />
        public int PerformanceRefreshIntervalMs { get; private set; }

        /// <inheritdoc />
        public int ConsoleRefreshIntervalMs { get; private set; }

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
        public int DependenciesRefreshIntervalMs { get; private set; }

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

        /// <inheritdoc />
        public string ServicesHiddenColumns
        {
            get => _servicesHiddenColumns;
            private set
            {
                if (_servicesHiddenColumns != value)
                {
                    _servicesHiddenColumns = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <inheritdoc />
        public string LogsHiddenColumns
        {
            get => _logsHiddenColumns;
            private set
            {
                if (_logsHiddenColumns != value)
                {
                    _logsHiddenColumns = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <inheritdoc />
        public bool IsDisplayNameColumnMigrated
        {
            get => _isDisplayNameColumnMigrated;
            private set
            {
                if (_isDisplayNameColumnMigrated != value)
                {
                    _isDisplayNameColumnMigrated = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <inheritdoc />
        public string MainWindowPlacement { get; private set; } = string.Empty;

        /// <inheritdoc />
        public string ServicesColumnWidths { get; private set; } = string.Empty;

        /// <inheritdoc />
        public string LogsColumnWidths { get; private set; } = string.Empty;

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
            // Select the display language before the first resource string is read
            StartupCulture.Apply(
                Path.Combine(AppFoldersHelper.GetAppDirectory(), AppSettingsFileName),
                Environment.GetCommandLineArgs().Skip(1));

            var services = new ServiceCollection();

            // Register dependencies
            services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
            services.AddSingleton<IProcessHelper, ProcessHelper>();
            services.AddSingleton<IProcessKiller, ProcessKiller>();

            Services = services.BuildServiceProvider();

            var options = new BootstrapperOptions
            {
                LogFileName = "Servy.Manager.log",
                AppSettingsFileName = AppSettingsFileName,
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
                        ServiceRepository!,
                        new ServyExePermissionsHardener(),
                        new NamedPipesService()
                    );

                    var fileDialogService = new FileDialogService();
                    var uiDispatcher = Services.GetRequiredService<IUiDispatcher>();
                    var messageBoxService = new MessageBoxService(uiDispatcher);
                    var helpService = new HelpService(messageBoxService, _bootstrapper!.AppLifetimeToken);
                    var serviceValidationRules = new ServiceValidationRules(processHelper);
                    var serviceConfigurationValidator = new ServiceConfigurationValidator(messageBoxService, serviceValidationRules, UiAppConfig.Caption);
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
                        this, // IAppConfiguration
                        processHelper,
                        uiDispatcher, // Pass the UI Dispatcher
                        cursorService
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
                    var main = new MainWindow(viewModel, messageBoxService, processKiller, this);
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

                    PerformanceRefreshIntervalMs = ConfigParser.GetConfigInt(config, "PerformanceRefreshIntervalInMs",
                        AppConfig.DefaultMonitoringRefreshIntervalMs,
                        AppConfig.MinPerformanceRefreshIntervalMs,
                        AppConfig.MaxPerformanceRefreshIntervalMs);

                    ConsoleRefreshIntervalMs = ConfigParser.GetConfigInt(config, "ConsoleRefreshIntervalInMs",
                        AppConfig.DefaultMonitoringRefreshIntervalMs,
                        AppConfig.MinConsoleRefreshIntervalMs,
                        AppConfig.MaxConsoleRefreshIntervalMs);

                    ConsoleMaxLines = ConfigParser.GetConfigInt(config, "ConsoleMaxLines",
                        AppConfig.DefaultConsoleMaxLines,
                        AppConfig.MinConsoleMaxLines,
                        AppConfig.MaxConsoleMaxLines);

                    DependenciesRefreshIntervalMs = ConfigParser.GetConfigInt(config, "DependenciesRefreshIntervalInMs",
                        AppConfig.DefaultMonitoringRefreshIntervalMs,
                        AppConfig.MinDependenciesRefreshIntervalMs,
                        AppConfig.MaxDependenciesRefreshIntervalMs);

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

                    if (PathSecurityGuard.TryResolveWithinAppDirectory(configuredPath, baseDirectory, out var resolvedPath))
                    {
                        DesktopAppPublishPath = resolvedPath;
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
                        PathSecurityGuard.WarnIfDirectoryAclNotHardened(DesktopAppPublishPath);
                    }

                    ServicesHiddenColumns = config["ServicesHiddenColumns"] ?? string.Empty;
                    LogsHiddenColumns = config["LogsHiddenColumns"] ?? string.Empty;
                    IsDisplayNameColumnMigrated = ConfigParser.ParseBool(config["IsDisplayNameColumnMigrated"], false, "IsDisplayNameColumnMigrated");

                    MainWindowPlacement = config[MainWindowPlacementKey] ?? string.Empty;
                    ServicesColumnWidths = config[ServicesColumnWidthsKey] ?? string.Empty;
                    LogsColumnWidths = config[LogsColumnWidthsKey] ?? string.Empty;
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
        /// Runs the bootstrapper's synchronous startup (configuration and logger), starts the
        /// companion-app availability monitor, and fire-and-forgets the asynchronous application
        /// initialization, which is where the database is initialized and resources are extracted.
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
            _ = _bootstrapper.InitializeAppWithFaultHandlingAsync(this, e, UiAppConfig.Caption);
        }

        /// <summary>
        /// Raises the <see cref="Application.Exit"/> event.
        /// Performs final cleanup of sensitive data providers and encryption keys before the application terminates.
        /// </summary>
        /// <param name="e">An <see cref="ExitEventArgs"/> that contains the event data.</param>
        /// <remarks>
        /// This override ensures that the bootstrapper's secure data (which may hold sensitive cryptographic
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

        #region Settings Persistence

        /// <summary>
        /// Writes a key-value setting into <c>appsettings.manager.json</c>, creating the file if needed.
        /// </summary>
        /// <param name="key">The JSON property name to set.</param>
        /// <param name="value">The string value to store.</param>
        private static void PersistSetting(string key, string value)
        {
            PersistSetting(Path.Combine(AppFoldersHelper.GetAppDirectory(), AppSettingsFileName), key, value);
        }

        /// <summary>
        /// Writes a key-value setting into the JSON configuration file at <paramref name="configPath"/>,
        /// creating the file if needed. The file is replaced atomically through
        /// <see cref="Helper.WriteFileAtomic(string, Action{Stream}, System.Threading.CancellationToken)"/>, so an
        /// interrupted write never leaves a truncated or half-written file behind.
        /// </summary>
        /// <param name="configPath">The full path of the JSON configuration file to update.</param>
        /// <param name="key">The JSON property name to set.</param>
        /// <param name="value">The string value to store.</param>
        /// <remarks>
        /// Seam for unit testing: <see cref="PersistSetting(string, string)"/> forwards to this overload unchanged with
        /// the path of <c>appsettings.manager.json</c> next to the executable. The content is written as UTF-8
        /// without a byte order mark, as <see cref="File.WriteAllText(string, string)"/> did before. Failures are
        /// logged as warnings and never thrown.
        /// </remarks>
        internal static void PersistSetting(string configPath, string key, string value)
        {
            PersistSettings(configPath, new[] { new KeyValuePair<string, string>(key, value) });
        }

        /// <summary>
        /// Writes several key-value settings into the JSON configuration file at <paramref name="configPath"/>
        /// in a single atomic replacement, creating the file if needed.
        /// </summary>
        /// <param name="configPath">The full path of the JSON configuration file to update.</param>
        /// <param name="settings">The JSON property names to set, each with the string value to store.</param>
        /// <remarks>
        /// The file is read once, every key is set, and the result is written once through
        /// <see cref="Helper.WriteFileAtomic(string, Action{Stream}, System.Threading.CancellationToken)"/>, so
        /// either all the settings are saved or none is. Nothing is read or written when
        /// <paramref name="settings"/> is empty. The content is written as UTF-8 without a byte order mark.
        /// Failures are logged as warnings and never thrown.
        /// </remarks>
        internal static void PersistSettings(string configPath, IReadOnlyList<KeyValuePair<string, string>> settings)
        {
            if (settings.Count == 0)
            {
                return;
            }

            try
            {
                JObject jsonObject;
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    jsonObject = JObject.Parse(json);
                }
                else
                {
                    jsonObject = new JObject();
                }

                foreach (var setting in settings)
                {
                    jsonObject[setting.Key] = setting.Value;
                }

                var content = jsonObject.ToString(Newtonsoft.Json.Formatting.Indented);
                Helper.WriteFileAtomic(configPath, stream =>
                {
                    var bytes = new UTF8Encoding(false).GetBytes(content);
                    stream.Write(bytes, 0, bytes.Length);
                });
                foreach (var setting in settings)
                {
                    Logger.Info($"Saved {setting.Key} setting to {AppSettingsFileName}: {(string.IsNullOrWhiteSpace(setting.Value) ? "<empty>" : setting.Value)}");
                }
            }
            catch (Exception ex)
            {
                foreach (var setting in settings)
                {
                    Logger.Warn($"Failed to save {setting.Key} setting to {AppSettingsFileName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Updates the services hidden columns string and persists it to the configuration file.
        /// </summary>
        public void SaveServicesHiddenColumns(string servicesHiddenColumns)
        {
            ServicesHiddenColumns = servicesHiddenColumns ?? string.Empty;
            PersistSetting("ServicesHiddenColumns", ServicesHiddenColumns);
        }

        /// <summary>
        /// Updates the logs hidden columns string and persists it to the configuration file.
        /// </summary>
        public void SaveLogsHiddenColumns(string logsHiddenColumns)
        {
            LogsHiddenColumns = logsHiddenColumns ?? string.Empty;
            PersistSetting("LogsHiddenColumns", LogsHiddenColumns);
        }

        /// <summary>
        /// Updates the migration state for services grid columns and persists it to the configuration file.
        /// </summary>
        public void SaveIsDisplayNameColumnMigrated(bool isMigrated)
        {
            IsDisplayNameColumnMigrated = isMigrated;
            PersistSetting("IsDisplayNameColumnMigrated", isMigrated.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// Updates the persisted window layout (the placement of the main window and the column widths of the
        /// services and logs tables) and writes the values that changed to the configuration file.
        /// </summary>
        /// <param name="mainWindowPlacement">The new <see cref="MainWindowPlacement"/>, or <see langword="null"/> to leave it as it is.</param>
        /// <param name="servicesColumnWidths">The new <see cref="ServicesColumnWidths"/>, or <see langword="null"/> to leave it as it is.</param>
        /// <param name="logsColumnWidths">The new <see cref="LogsColumnWidths"/>, or <see langword="null"/> to leave it as it is.</param>
        /// <remarks>
        /// The changed values are written together in one replacement of the file, and the file is not
        /// touched at all when nothing changed, so closing the window without moving anything costs no write.
        /// </remarks>
        public void SaveWindowLayout(string? mainWindowPlacement, string? servicesColumnWidths, string? logsColumnWidths)
        {
            var changes = GetLayoutChanges(
                MainWindowPlacement, mainWindowPlacement,
                ServicesColumnWidths, servicesColumnWidths,
                LogsColumnWidths, logsColumnWidths);

            foreach (var change in changes)
            {
                switch (change.Key)
                {
                    case MainWindowPlacementKey: MainWindowPlacement = change.Value; break;
                    case ServicesColumnWidthsKey: ServicesColumnWidths = change.Value; break;
                    case LogsColumnWidthsKey: LogsColumnWidths = change.Value; break;
                }
            }

            PersistSettings(Path.Combine(AppFoldersHelper.GetAppDirectory(), AppSettingsFileName), changes);
        }

        /// <summary>
        /// Selects the window layout settings whose value differs from the one currently held.
        /// </summary>
        /// <param name="currentPlacement">The current <see cref="MainWindowPlacement"/>.</param>
        /// <param name="newPlacement">The new placement, or <see langword="null"/> to leave it as it is.</param>
        /// <param name="currentServicesWidths">The current <see cref="ServicesColumnWidths"/>.</param>
        /// <param name="newServicesWidths">The new services column widths, or <see langword="null"/> to leave them as they are.</param>
        /// <param name="currentLogsWidths">The current <see cref="LogsColumnWidths"/>.</param>
        /// <param name="newLogsWidths">The new logs column widths, or <see langword="null"/> to leave them as they are.</param>
        /// <returns>The configuration key and new value of each setting to write, empty when nothing changed.</returns>
        internal static IReadOnlyList<KeyValuePair<string, string>> GetLayoutChanges(
            string currentPlacement, string? newPlacement,
            string currentServicesWidths, string? newServicesWidths,
            string currentLogsWidths, string? newLogsWidths)
        {
            var changes = new List<KeyValuePair<string, string>>();

            AddIfChanged(changes, MainWindowPlacementKey, currentPlacement, newPlacement);
            AddIfChanged(changes, ServicesColumnWidthsKey, currentServicesWidths, newServicesWidths);
            AddIfChanged(changes, LogsColumnWidthsKey, currentLogsWidths, newLogsWidths);

            return changes;
        }

        /// <summary>
        /// Adds a setting to <paramref name="changes"/> when a new value is supplied and differs from the current one.
        /// </summary>
        /// <param name="changes">The list of settings to write.</param>
        /// <param name="key">The configuration key of the setting.</param>
        /// <param name="current">The value currently held.</param>
        /// <param name="updated">The new value, or <see langword="null"/> to leave the setting as it is.</param>
        private static void AddIfChanged(List<KeyValuePair<string, string>> changes, string key, string current, string? updated)
        {
            if (updated != null && !string.Equals(updated, current, StringComparison.Ordinal))
            {
                changes.Add(new KeyValuePair<string, string>(key, updated));
            }
        }

        #endregion
    }
}
