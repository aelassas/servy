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
using Servy.Validation;
using Servy.ViewModels;
using Servy.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
#if !DEBUG
using System.Diagnostics;
#endif
using System.IO;
using System.Windows;
using Servy.Config;
using AppConfig = Servy.Core.Config.AppConfig;
using Servy.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Servy.Core.Validation;
using System.Diagnostics.CodeAnalysis;

namespace Servy
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
        public const string ResourcesNamespace = "Servy.Resources";

        #endregion

        #region Static Properties

        /// <summary>
        /// Service provider for dependency injection, initialized by the bootstrapper.
        /// </summary>
        public static IServiceProvider? Services { get; private set; }

        #endregion

        #region Fields

        private readonly AppBootstrapper _bootstrapper;
        private bool _isManagerAppAvailable;

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

        /// <inheritdoc/>
        public string? ManagerAppPublishPath { get; private set; }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
            var services = new ServiceCollection();

            // Register dependencies
            services.AddSingleton<IProcessHelper, ProcessHelper>();
            services.AddSingleton<IProcessKiller, ProcessKiller>();

            Services = services.BuildServiceProvider();

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
                    var processKiller = Services.GetRequiredService<IProcessKiller>();

                    // 1. Initialize Infrastructure & Domain Services
                    Func<string, IServiceControllerWrapper> controllerFactory = name => new ServiceControllerWrapper(name);
                    var serviceManager = new ServiceManager(
                        controllerFactory,
                        new ServiceControllerProvider(controllerFactory),
                        new WindowsServiceApi(),
                        new Win32ErrorProvider(),
                        ServiceRepository!
                    );

                    // 2. Initialize UI Services
                    var fileDialogService = new FileDialogService();
                    var messageBoxService = new MessageBoxService(new WpfUiDispatcher());
                    var helperService = new HelpService(messageBoxService);
                    var serviceValidationRules = new ServiceValidationRules(processHelper);
                    var configValidator = new ServiceConfigurationValidator(messageBoxService, serviceValidationRules);

                    // 3. Resolve Circular Dependency using Proxies
                    MainViewModel? viewModel = null;

                    Func<ServiceDto?> modelToDtoProxy = () => viewModel?.ModelToServiceDto();
                    Action<ServiceDto> bindDtoProxy = (dto) => viewModel?.BindServiceDtoToModel(dto);


                    var serviceCommands = new ServiceCommands(
                        modelToDtoProxy,
                        bindDtoProxy,
                        serviceManager,
                        messageBoxService,
                        fileDialogService,
                        configValidator,
                        new XmlServiceValidator(serviceValidationRules),
                        new JsonServiceValidator(serviceValidationRules),
                        this,
                        new CursorService(),
                        new XmlServiceSerializer(),
                        new JsonServiceSerializer()
                    );

                    // 4. Initialize Main ViewModel
                    viewModel = new MainViewModel(
                        fileDialogService,
                        serviceCommands,
                        messageBoxService,
                        ServiceRepository!,
                        helperService,
                        this
                    );

                    // 5. Inject dependencies into the View and initialize
                    var mainWindow = new MainWindow(viewModel, processKiller);
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
                    if (!Helper.IsAbsolute(ManagerAppPublishPath))
                    {
                        ManagerAppPublishPath = Path.GetFullPath(Path.Combine(baseDirectory, ManagerAppPublishPath));
                    }
#endif
                    IsManagerAppAvailable = !string.IsNullOrEmpty(ManagerAppPublishPath) && File.Exists(ManagerAppPublishPath);
                    if (!IsManagerAppAvailable)
                    {
                        Logger.Warn($"Manager app executable not found: {ManagerAppPublishPath}");
                    }
                }
            };

            _bootstrapper = new AppBootstrapper(
                options,
                Services.GetRequiredService<IProcessKiller>()
                );
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
            _ = _bootstrapper.StartAvailabilityMonitorAsync(ManagerAppPublishPath, isAvailable => IsManagerAppAvailable = isAvailable, this);

            // 3. Fire-and-forget initialization
            // Use a dedicated async method instead of a chained ContinueWith 
            // to ensure the startup lifecycle and any faults are correctly observed.
            _ = _bootstrapper.InitializeAppWithFaultHandlingAsync(this, e, Config.AppConfig.Caption);
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