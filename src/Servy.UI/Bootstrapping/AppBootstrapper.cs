using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System.Diagnostics;
#if !DEBUG
using System.IO;
#endif
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Servy.UI.Bootstrapping
{
    /// <summary>
    /// Orchestrates the application lifecycle and initialization for Servy desktop applications.
    /// </summary>
    /// <remarks>
    /// This class centralizes shared logic for Rendering Tier detection, unhandled exception 
    /// orchestration, configuration loading via JSON, and embedded resource extraction.
    /// </remarks>
    public class AppBootstrapper
    {
        #region Properties

        /// <summary>
        /// Gets the initialized database context.
        /// </summary>
        public AppDbContext? DbContext { get; private set; }

        /// <summary>
        /// Gets the repository for service data access.
        /// </summary>
        public IServiceRepository? ServiceRepository { get; private set; }

        /// <summary>
        /// Gets the secure data provider for encryption operations.
        /// </summary>
        public ISecureData? SecureData { get; private set; }

        /// <summary>
        /// Gets the active database connection string.
        /// </summary>
        public string? ConnectionString { get; private set; }

        /// <summary>
        /// Gets the file path for the AES encryption key.
        /// </summary>
        public string? AESKeyFilePath { get; private set; }

        /// <summary>
        /// Gets the file path for the AES initialization vector.
        /// </summary>
        public string? AESIVFilePath { get; private set; }

        /// <summary>
        /// Gets a value indicating whether software rendering has been forced.
        /// </summary>
        public bool ForceSoftwareRendering { get; private set; }

        #endregion

        private readonly BootstrapperOptions _options;
        private readonly IProcessKiller _processKiller;
        private IConfiguration? _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppBootstrapper"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the bootstrap process.</param>
        /// <param name="processKiller">Service responsible for terminating child processes.</param>
        public AppBootstrapper(BootstrapperOptions options, IProcessKiller processKiller)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
        }

        /// <summary>
        /// Handles the synchronous portion of the application startup.
        /// Configures logging, rendering modes, and global exception handlers.
        /// </summary>
        /// <param name="app">The active WPF application instance.</param>
        /// <param name="e">Startup arguments.</param>
        public void OnStartup(Application app, StartupEventArgs e)
        {
            if (_options == null)
            {
                throw new InvalidOperationException("Bootstrapper options must be provided.");
            }

            // 1. Initialize Configuration and Logger settings immediately
            LoadConfiguration();

            // 2. Initialize Logger with the correct settings before any logging occurs
            Logger.Initialize(_options.LogFileName);
            ApplyLoggerSettings();

            // 3. Global AppDomain exceptions (Fatal)
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception? ex = args.ExceptionObject as Exception;
                Logger.Error("FATAL: AppDomain Unhandled Exception. Process is terminating.", ex);
                MessageBox.Show(
                    "A fatal error occurred and the application must close. Detailed diagnostics have been saved to the log file.",
                    "Servy - Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            // 4. UI Thread exceptions (Dispatcher)
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

            // 5. Security Check: Admin Rights
            if (!SecurityHelper.IsAdministrator())
            {
                MessageBox.Show(_options.SecurityWarningMessage, _options.SecurityWarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                app.Shutdown(1);
                return;
            }

            // 6. Security Check: SQLite Environment
            if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
            {
                string message = string.Format(_options.SqliteVersionWarningMessageFormat!, detectedVersion, AppConfig.MinRequiredSqliteVersion);
                MessageBox.Show(message, _options.SqliteVersionWarningTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                app.Shutdown();
                return;
            }

            // 7. Rendering Detection
            var renderingTier = RenderCapability.Tier >> 16;
            var isRemote = SystemParameters.IsRemoteSession;
            ForceSoftwareRendering = e.Args.Any(arg => arg.Equals(AppConfig.ForceSoftwareRenderingArg, StringComparison.OrdinalIgnoreCase));

            Logger.Info($"Startup initialized. RenderingTier={renderingTier}, RemoteSession={isRemote}, ForceSoftwareRendering={ForceSoftwareRendering}");

            if (renderingTier == 0 || isRemote || ForceSoftwareRendering)
            {
                Logger.Warn("Low rendering capabilities detected. Forcing Software Rendering Mode.");
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
        }

        /// <summary>
        /// Loads the application configuration from the JSON file synchronously to ensure settings are available for immediate use.
        /// </summary>
        private void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder();
#if DEBUG
            builder.AddJsonFile(_options.AppSettingsFileName!, optional: true, reloadOnChange: true);
#else
            builder.SetBasePath(AppFoldersHelper.GetAppDirectory())
                   .AddJsonFile(_options.AppSettingsFileName!, optional: true, reloadOnChange: true);
#endif
            _configuration = builder.Build();

            ConnectionString = _configuration["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
            AESKeyFilePath = _configuration["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
            AESIVFilePath = _configuration["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;
        }

        /// <summary>
        /// Applies the loaded logger settings to the static <see cref="Logger"/> instance.
        /// </summary>
        private void ApplyLoggerSettings()
        {
            if (_configuration == null) return;

            if (!Enum.TryParse<LogLevel>(_configuration["LogLevel"], true, out var logLevel)) logLevel = LogLevel.Info;
            Logger.SetLogLevel(logLevel);

            if (!Enum.TryParse<DateRotationType>(_configuration["LogRollingInterval"], true, out var dateRotationType)) dateRotationType = DateRotationType.None;
            Logger.SetDateRotationType(dateRotationType);

            if (int.TryParse(_configuration["LogRotationSizeMB"], out var size) && size > 0) Logger.SetLogRotationSize(size);
            else Logger.SetLogRotationSize(Logger.DefaultLogRotationSizeMB);

            if (int.TryParse(_configuration["MaxBackupLogFiles"], out var maxBackupFiles) && maxBackupFiles >= 0) Logger.SetMaxBackupLogFiles(maxBackupFiles);
            else Logger.SetMaxBackupLogFiles(Logger.DefaultMaxBackupLogFiles);

            string rawUseLocalTime = _configuration["UseLocalTimeForRotation"] ?? AppConfig.DefaultUseLocalTimeForRotation.ToString();
            if (!bool.TryParse(rawUseLocalTime, out bool useLocalTime)) useLocalTime = AppConfig.DefaultUseLocalTimeForRotation;
            Logger.SetUseLocalTimeForRotation(useLocalTime);

            _options.CustomConfigAction?.Invoke(_configuration);
        }

        /// <summary>
        /// Asynchronously handles heavy initialization tasks such as database migrations, 
        /// resource extraction, and window orchestration.
        /// </summary>
        /// <param name="app">The active WPF application instance.</param>
        /// <param name="e">The startup event arguments.</param>
        /// <param name="processHelper">An instance of <see cref="IProcessHelper"/> used to query running processes.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous initialization process.</returns>
        public async Task InitializeAppAsync(Application app, StartupEventArgs e, IProcessHelper processHelper)
        {
            string? serviceName = null;
            var showSplash = true;

            if (e.Args != null)
            {
                var positionalArgs = e.Args.Where(arg => !arg.Equals(AppConfig.ForceSoftwareRenderingArg, StringComparison.OrdinalIgnoreCase)).ToList();
                if (positionalArgs.Count > 0) bool.TryParse(positionalArgs[0], out showSplash);
                if (positionalArgs.Count > 1) serviceName = positionalArgs[1];
            }

            Window? splash = null;
            try
            {
                // 1. Splash Screen
                if (_options.SplashWindowFactory != null)
                {
                    splash = _options.SplashWindowFactory();
                    if (showSplash && splash != null)
                    {
                        splash.Show();
                        await Task.Yield();
                    }
                }

                Helper.EnsureEventSourceExists();

                // 2. Background Initialization (I/O & DB)
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

                    DbContext = new AppDbContext(ConnectionString);
                    DatabaseInitializer.InitializeDatabase(DbContext, SQLiteDbInitializer.Initialize);

                    var dapperExecutor = new DapperExecutor(DbContext);
                    var protectedKeyProvider = new ProtectedKeyProvider(AESKeyFilePath, AESIVFilePath);
                    SecureData = new SecureData(protectedKeyProvider);

                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();

                    ServiceRepository = new ServiceRepository(dapperExecutor, SecureData, xmlSerializer, jsonSerializer);
                    var resourceHelper = new ResourceHelper(ServiceRepository, _processKiller);

                    // Copy embedded files
                    await resourceHelper.CopyEmbeddedResource(asm, _options.ResourcesNamespace!, AppConfig.ServyServiceUIFileName, "exe");
                    await resourceHelper.CopyEmbeddedResource(asm, _options.ResourcesNamespace!, AppConfig.HandleExeFileName, "exe", false);

#if DEBUG
                    await resourceHelper.CopyEmbeddedResource(asm, _options.ResourcesNamespace!, AppConfig.ServyServiceUIFileName, "pdb", false);
#endif
                    stopwatch.Stop();

                    if (showSplash && stopwatch.ElapsedMilliseconds < 1000)
                    {
                        await Task.Delay(500);
                    }
                });

                // 3. Main Window Factory
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
                if (showSplash && splash?.IsVisible == true)
                {
                    splash.Close();
                }
            }
        }

        /// <summary>
        /// Orchestrates the deterministic cleanup of application resources during the exit sequence.
        /// </summary>
        /// <param name="e">The <see cref="ExitEventArgs"/> containing the event data.</param>
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
                Logger.Warn($"{name} disposal failed", ex);
            }
        }
    }
}