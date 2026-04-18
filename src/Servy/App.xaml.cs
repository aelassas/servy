using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Infrastructure.Data;
using Servy.Resources;
using Servy.UI.Bootstrapping;
using Servy.Views;
#if !DEBUG
using System.Diagnostics;
#endif
using System.IO;
using System.Windows;

namespace Servy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region Constants

        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        public static readonly string ResourcesNamespace = "Servy.Resources";

        #endregion

        #region Fields

        private readonly AppBootstrapper _bootstrapper;

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
        /// </summary>
        public bool IsManagerAppAvailable { get; private set; }

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
                MainWindowFactoryAsync = async (serviceName) =>
                {
                    var mainWindow = new MainWindow();
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
                    ManagerAppPublishPath = AppConfig.ManagerAppPublishDebugPath;
#else
                    var baseDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
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
                }
            };

            _bootstrapper = new AppBootstrapper(options);
        }

        #endregion

        #region Events

        /// <summary>
        /// Called when the WPF application starts.
        /// Loads configuration settings, initializes the database if necessary,
        /// subscribes to unhandled exception handlers, and extracts required embedded resources.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            _bootstrapper.OnStartup(this, e);
            base.OnStartup(e);

            _ = _bootstrapper.InitializeAppAsync(this, e).ContinueWith(async t =>
            {
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.Flatten().InnerException;
                    Logger.Error("Critical Startup Fault in InitializeApp", ex);
                    await Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"Critical Startup Fault: {ex?.Message}");
                        Shutdown(1);
                    });
                }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
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
