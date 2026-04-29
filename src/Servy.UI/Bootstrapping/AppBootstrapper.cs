using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Servy.UI.Bootstrapping
{
    /// <summary>
    /// Serves as the Composition Root for Servy desktop applications, centralizing startup, initialization, and teardown logic.
    /// </summary>
    /// <remarks>
    /// This class orchestrates environment validation, global exception handling, rendering configuration, and asynchronous resource extraction to ensure a consistent startup experience across both the main application and the manager.
    /// </remarks>
    public class AppBootstrapper
    {
        #region Properties

        /// <summary>
        /// Gets the initialized application database context.
        /// </summary>
        public AppDbContext DbContext { get; private set; }

        /// <summary>
        /// Gets the repository responsible for managing service data.
        /// </summary>
        public IServiceRepository ServiceRepository { get; private set; }

        /// <summary>
        /// Gets the provider for managing sensitive encrypted data.
        /// </summary>
        public ISecureData SecureData { get; private set; }

        /// <summary>
        /// Gets the connection string used for database operations.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Gets the file path where the AES encryption key is stored.
        /// </summary>
        public string AESKeyFilePath { get; private set; }

        /// <summary>
        /// Gets the file path where the AES initialization vector (IV) is stored.
        /// </summary>
        public string AESIVFilePath { get; private set; }

        /// <summary>
        /// Gets a value indicating whether software rendering has been forced due to environment constraints or manual override.
        /// </summary>
        public bool ForceSoftwareRendering { get; private set; }

        #endregion

        private readonly BootstrapperOptions _options;
        private readonly IProcessHelper _processHelper;
        private readonly IProcessKiller _processKiller;

        /// <summary>
        /// Initializes a new instance of the AppBootstrapper class.
        /// </summary>
        /// <param name="options">Configuration options for the bootstrap process.</param>
        /// <param name="processHelper">The process helper used to manage processes. Cannot be null.</param>
        /// <param name="processKiller">Service responsible for terminating child processes.</param>
        public AppBootstrapper(
            BootstrapperOptions options,
            IProcessHelper processHelper,
            IProcessKiller processKiller
            )
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
        }

        /// <summary>
        /// Handles the initial, synchronous startup logic including logging initialization, global exception subscriptions, and rendering tier detection.
        /// </summary>
        /// <param name="app">The active WPF application instance.</param>
        /// <param name="e">The startup event arguments.</param>
        public void OnStartup(Application app, StartupEventArgs e)
        {
            // 1. Initialize Configuration and Logger settings immediately
            LoadConfiguration();

            // 2. Initialize Logger with the correct settings before any logging occurs
            Logger.Initialize(_options.LogFileName);
            ApplyLoggerSettings();

            // 3. Global AppDomain exceptions (Fatal crashes outside the UI dispatcher)
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                Logger.Error("FATAL: AppDomain Unhandled Exception. Process is terminating.", ex);
                MessageBox.Show(
                    "A fatal error occurred and the application must close. Detailed diagnostics have been saved to the log file.",
                    "Servy - Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            // 4. UI Thread exceptions (Dispatcher errors)
            app.DispatcherUnhandledException += (sender, args) =>
            {
                Logger.Error("UI Dispatcher Exception", args.Exception);
                bool isOutOfMemory = args.Exception is OutOfMemoryException;

                if (!isOutOfMemory)
                {
                    MessageBox.Show(
                        "An unexpected error occurred in the interface, but the application will attempt to continue. Details have been logged.",
                        "Servy - Unexpected Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    args.Handled = true;
                }
                else
                {
                    Logger.Error("Non-recoverable OutOfMemoryException detected. Shutting down.");
                    args.Handled = false;
                    app.Shutdown(1);
                }
            };

            // 5. Environmental Security Checks
            if (!SecurityHelper.IsAdministrator())
            {
                MessageBox.Show(_options.SecurityWarningMessage, _options.SecurityWarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                app.Shutdown(1);
                return;
            }

            if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
            {
                string message = string.Format(_options.SqliteVersionWarningMessageFormat, detectedVersion, AppConfig.MinRequiredSqliteVersion);
                MessageBox.Show(message, _options.SqliteVersionWarningTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                app.Shutdown();
                return;
            }

            // 6. Hardware Acceleration Check
            var renderingTier = RenderCapability.Tier >> 16;
            var isRemote = SystemParameters.IsRemoteSession;
            ForceSoftwareRendering = e.Args.Any(arg => arg.Equals(AppConfig.ForceSoftwareRenderingArg, StringComparison.OrdinalIgnoreCase));

            Logger.Info($"Startup initialized. RenderingTier={renderingTier}, RemoteSession={isRemote}, ForceSoftwareRendering={ForceSoftwareRendering}");

            // Fallback to software rendering for stability in RDP or low-tier graphics scenarios
            if (renderingTier == 0 || isRemote || ForceSoftwareRendering)
            {
                Logger.Warn("Low rendering capabilities detected. Forcing Software Rendering Mode.");
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
        }

        /// <summary>
        /// Loads the application configuration from the application settings synchronously to ensure settings are available for immediate use.
        /// </summary>
        private void LoadConfiguration()
        {
            var config = ConfigurationManager.AppSettings;
            ConnectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
            AESKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
            AESIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;
        }

        /// <summary>
        /// Applies the loaded logger settings to the static logger instance based on the standard configuration parameters.
        /// </summary>
        private void ApplyLoggerSettings()
        {
            var config = ConfigurationManager.AppSettings;

            // Logging verbosity and rotation setup
            if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel)) logLevel = LogLevel.Info;
            Logger.SetLogLevel(logLevel);

            if (!Enum.TryParse<DateRotationType>(config["LogRollingInterval"], true, out var dateRotationType)) dateRotationType = DateRotationType.None;
            Logger.SetDateRotationType(dateRotationType);

            if (int.TryParse(config["LogRotationSizeMB"], out var size) && size > 0) Logger.SetLogRotationSize(size);
            else Logger.SetLogRotationSize(AppConfig.DefaultRotationSizeMB);

            if (int.TryParse(config["MaxBackupLogFiles"], out var maxBackupFiles) && maxBackupFiles >= 0) Logger.SetMaxBackupLogFiles(maxBackupFiles);
            else Logger.SetMaxBackupLogFiles(Logger.DefaultMaxBackupLogFiles);

            string rawUseLocalTime = config["UseLocalTimeForRotation"] ?? AppConfig.DefaultUseLocalTimeForRotation.ToString();
            if (!bool.TryParse(rawUseLocalTime, out bool useLocalTime)) useLocalTime = AppConfig.DefaultUseLocalTimeForRotation;
            Logger.SetUseLocalTimeForRotation(useLocalTime);

            // Invoke project-specific configuration logic
            _options.CustomConfigAction?.Invoke(config);
        }

        /// <summary>
        /// Asynchronously handles heavy initialization tasks such as database migrations, configuration loading, resource extraction, and window orchestration.
        /// </summary>
        /// <param name="app">The active WPF application instance.</param>
        /// <param name="e">The startup event arguments.</param>
        /// <param name="processHelper">An instance of the process helper used to query running processes.</param>
        /// <returns>A Task representing the asynchronous initialization process.</returns>
        public async Task InitializeAppAsync(Application app, StartupEventArgs e, IProcessHelper processHelper)
        {
            string serviceName = null;
            var showSplash = true;

            // Command line parsing for deep-linking and splash control
            if (e.Args != null)
            {
                var positionalArgs = e.Args.Where(arg => !arg.Equals(AppConfig.ForceSoftwareRenderingArg, StringComparison.OrdinalIgnoreCase)).ToList();
                if (positionalArgs.Count > 0) bool.TryParse(positionalArgs[0], out showSplash);
                if (positionalArgs.Count > 1) serviceName = positionalArgs[1];
            }

            Window splash = null;
            try
            {
                // 1. Show Splash Screen if required
                if (_options.SplashWindowFactory != null)
                {
                    splash = _options.SplashWindowFactory();
                    if (showSplash && splash != null)
                    {
                        splash.Show();
                        await Task.Yield(); // Allow the UI thread to pump messages and render the window
                    }
                }

                Helper.EnsureEventSourceExists();

                // 2. Parallelized System Initialization (Off-UI Thread)
                // Note: Configuration and Logger settings are already loaded synchronously in OnStartup.
                await Task.Run(async () =>
                {
                    if (string.IsNullOrEmpty(ConnectionString) || string.IsNullOrEmpty(AESKeyFilePath) || string.IsNullOrEmpty(AESIVFilePath))
                    {
                        throw new InvalidOperationException("Critical configuration values are missing. Ensure that the appsettings.json file is present and correctly configured.");
                    }

                    var stopwatch = Stopwatch.StartNew();

                    AppFoldersHelper.EnsureFolders(ConnectionString, AESKeyFilePath, AESIVFilePath);

                    var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                    // Database and Repository setup
                    DbContext = new AppDbContext(ConnectionString);
                    DatabaseInitializer.InitializeDatabase(DbContext, SQLiteDbInitializer.Initialize);

                    var dapperExecutor = new DapperExecutor(DbContext);
                    var protectedKeyProvider = new ProtectedKeyProvider(AESKeyFilePath, AESIVFilePath);
                    SecureData = new SecureData(protectedKeyProvider);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();

                    ServiceRepository = new ServiceRepository(dapperExecutor, SecureData, xmlSerializer, jsonSerializer);
                    var resourceHelper = new ResourceHelper(ServiceRepository, _processHelper, _processKiller);

                    // Binary Resource Extraction
                    if (!await resourceHelper.CopyEmbeddedResource(asm, _options.ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        await app.Dispatcher.InvokeAsync(() => MessageBox.Show($"Failed copying embedded resource: {AppConfig.HandleExe}"));
                    }

                    var resourceItems = new List<ResourceItem>
                    {
                        new ResourceItem{ FileNameWithoutExtension = AppConfig.ServyServiceUIFileName, Extension= "exe"}
                    };

#if DEBUG
                    if (!await resourceHelper.CopyEmbeddedResource(asm, _options.ResourcesNamespace, AppConfig.ServyServiceUIFileName, "pdb", false))
                    {
                        await app.Dispatcher.InvokeAsync(() => MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIFileName}.pdb"));
                    }
#else
                    // Runtime DLL requirements
                    resourceItems.AddRange(new List<ResourceItem>
                    {
                        new ResourceItem{ FileNameWithoutExtension = "Servy.Core", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "Dapper", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "Microsoft.Bcl.AsyncInterfaces", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "Newtonsoft.Json", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "Servy.Infrastructure", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "System.Data.SQLite", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "System.Runtime.CompilerServices.Unsafe", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "System.Threading.Tasks.Extensions", Extension= "dll" },
                        new ResourceItem{ FileNameWithoutExtension = "e_sqlite3", Extension= "dll" },
                    });
#endif
                    if (!await resourceHelper.CopyResources(asm, _options.ResourcesNamespace, resourceItems))
                    {
                        await app.Dispatcher.InvokeAsync(() => MessageBox.Show($"Failed copying embedded resources."));
                    }

                    stopwatch.Stop();

                    // Prevent "splash screen flicker" by ensuring it stays visible for a minimum duration
                    if (showSplash && stopwatch.ElapsedMilliseconds < 1000)
                    {
                        await Task.Delay(500);
                    }
                });

                // 3. Instantiate and show the primary MainWindow
                if (_options.MainWindowFactoryAsync != null)
                {
                    var mainWindow = await _options.MainWindowFactoryAsync(serviceName);
                    if (app.MainWindow != mainWindow && mainWindow != null)
                    {
                        app.MainWindow = mainWindow;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Startup error", ex);
                MessageBox.Show("Startup error: " + ex.Message);
                app.Shutdown();
            }
            finally
            {
                // Ensure splash is cleaned up even if startup fails
                if (showSplash && splash?.IsVisible == true)
                {
                    splash.Close();
                }
            }
        }

        /// <summary>
        /// Orchestrates the deterministic cleanup of application resources during the exit sequence.
        /// </summary>
        /// <param name="e">The ExitEventArgs containing the event data.</param>
        /// <remarks>
        /// This method ensures that critical resources like the database context and secure data providers are released properly, even if individual disposal attempts encounter issues.
        /// </remarks>
        public void OnExit(ExitEventArgs e)
        {
            TryDispose(() => DbContext?.Dispose(), nameof(DbContext));
            TryDispose(() => SecureData?.Dispose(), nameof(SecureData));
        }

        /// <summary>
        /// Attempts to execute a disposal action within a safety block to prevent exit-time crashes.
        /// </summary>
        /// <param name="dispose">The disposal action to execute.</param>
        /// <param name="name">The name of the resource being disposed, used for logging.</param>
        private static void TryDispose(Action dispose, string name)
        {
            try
            {
                dispose();
            }
            catch (Exception ex)
            {
                // We use Warn here because failure to dispose at exit is rarely 
                // fatal but should be noted for debugging resource leaks.
                Logger.Warn($"{name} disposal failed", ex);
            }
        }

    }
}