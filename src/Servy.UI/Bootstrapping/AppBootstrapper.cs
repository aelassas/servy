using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Resources;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System.Diagnostics;
using System.IO;
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
    /// orchestration, configuration loading via JSON, embedded resource extraction, and target app availability monitoring.
    /// </remarks>
    public class AppBootstrapper
    {
        #region Private Fields

        private readonly BootstrapperOptions _options;
        private readonly IProcessKiller _processKiller;
        private IConfiguration? _configuration;
        private ProtectedKeyProvider? _protectedKeyProvider;

        private FileSystemWatcher? _availabilityWatcher;
        private FileSystemEventHandler? _availabilityChangedHandler;
        private RenamedEventHandler? _availabilityRenamedHandler;
        private readonly CancellationTokenSource _appLifetimeCts = new CancellationTokenSource();

        #endregion

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
        /// <returns><see langword="true"/> if security and environment checks passed; otherwise <see langword="false"/>.</returns>
        public bool OnStartup(Application app, StartupEventArgs e)
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
                    Strings.Msg_FatalError_Body,
                    Strings.Msg_FatalError_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            // 4. UI Thread exceptions (Dispatcher)
            app.DispatcherUnhandledException += (sender, args) =>
            {
                Logger.Error("UI Dispatcher Exception", args.Exception);
                if (!(args.Exception is OutOfMemoryException))
                {
                    MessageBox.Show(
                        Strings.Msg_UnexpectedError_Body,
                        Strings.Msg_UnexpectedError_Title,
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
                return false; // ABORT STARTUP
            }

            // 6. Security Check: SQLite Environment
            if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
            {
                string message = string.Format(_options.SqliteVersionWarningMessageFormat!, detectedVersion, AppConfig.MinRequiredSqliteVersion);
                MessageBox.Show(message, _options.SqliteVersionWarningTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                app.Shutdown(1);
                return false; // ABORT STARTUP
            }

            // 7. Rendering Detection
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

            return true; // PROCEED
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

            ConnectionString = _configuration.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
            AESKeyFilePath = _configuration["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
            AESIVFilePath = _configuration["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;
        }

        /// <summary>
        /// Applies the loaded logger settings to the static <see cref="Logger"/> instance.
        /// </summary>
        private void ApplyLoggerSettings()
        {
            if (_configuration == null) return;

            LoggerConfigurator.ConfigureFromAppSettings(_configuration);

            _options.CustomConfigAction?.Invoke(_configuration);
        }

        /// <summary>
        /// Asynchronously initializes the application and handles any critical faults 
        /// that occur before the UI is ready.
        /// </summary>
        /// <param name="app">The active WPF application instance.</param>
        /// <param name="e">The startup event arguments.</param>
        /// <param name="caption">Error dialog caption.</param>
        /// <returns>A <see cref="Task"/> representing the execution.</returns>
        public async Task InitializeAppWithFaultHandlingAsync(Application app, StartupEventArgs e, string caption)
        {
            try
            {
                await InitializeAppAsync(app, e);
            }
            catch (Exception ex)
            {
                Logger.Error("Critical Startup Fault in InitializeApp", ex);

                await app.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        MessageBox.Show(
                            $"Critical Startup Fault: {ex.Message}",
                            caption,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    finally
                    {
                        app.Shutdown(1);
                    }
                });
            }
        }

        /// <summary>
        /// Asynchronously handles heavy initialization tasks such as database migrations, 
        /// resource extraction, and window orchestration.
        /// </summary>
        /// <param name="app">The active WPF application instance.</param>
        /// <param name="e">The startup event arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous initialization process.</returns>
        public async Task InitializeAppAsync(Application app, StartupEventArgs e)
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
                if (showSplash && _options.SplashWindowFactory != null)
                {
                    splash = _options.SplashWindowFactory();
                    splash?.Show();
                    await Task.Yield(); // Allow the UI thread to pump messages and render the window
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
                    _protectedKeyProvider = new ProtectedKeyProvider(AESKeyFilePath, AESIVFilePath);
                    SecureData = new SecureData(_protectedKeyProvider);

                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();

                    ServiceRepository = new ServiceRepository(dapperExecutor, SecureData, xmlSerializer, jsonSerializer);
                    var sh = new ServiceHelper(ServiceRepository);
                    var resourceHelper = new ResourceHelper(sh, _processKiller);

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
                MessageBox.Show(string.Format(Strings.Msg_StartupError_Format, ex.Message));
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
        /// Starts a real-time, resilient background monitor to track the availability of the target application.
        /// </summary>
        /// <param name="targetAppPublishPath">The path to the target application executable.</param>
        /// <param name="updateAvailabilityCallback">The callback to invoke when the target application's availability state changes.</param>
        /// <param name="app">The active WPF application instance.</param>
        public async Task StartAvailabilityMonitorAsync(string? targetAppPublishPath, Action<bool> updateAvailabilityCallback, Application app)
        {
            if (string.IsNullOrEmpty(targetAppPublishPath)) return;

            string? directory = Path.GetDirectoryName(targetAppPublishPath);
            string fileName = Path.GetFileName(targetAppPublishPath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) return;

            // Outer loop keeps the monitor alive for the lifetime of the application
            while (!_appLifetimeCts.Token.IsCancellationRequested)
            {
                try
                {
                    // PHASE 1: Waiting for Installation
                    // Deferred Attachment: Wait for the directory to exist naturally on disk.
                    while (!Directory.Exists(directory))
                    {
                        await Task.Delay(AppConfig.AppAvailabilityPollIntervalMs, _appLifetimeCts.Token);
                    }

                    // PHASE 2: Attachment
                    // Initialize the watcher now that the path is valid.
                    _availabilityWatcher = new FileSystemWatcher(directory, fileName)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };

                    _availabilityChangedHandler = (s, e) => UpdateAvailabilityState(targetAppPublishPath, updateAvailabilityCallback, app);
                    _availabilityRenamedHandler = (s, e) => UpdateAvailabilityState(targetAppPublishPath, updateAvailabilityCallback, app);

                    _availabilityWatcher.Created += _availabilityChangedHandler;
                    _availabilityWatcher.Deleted += _availabilityChangedHandler;
                    _availabilityWatcher.Changed += _availabilityChangedHandler;
                    _availabilityWatcher.Renamed += _availabilityRenamedHandler;

                    // Log unexpected buffer overflows, but we no longer rely on this for directory renames
                    _availabilityWatcher.Error += (s, e) =>
                        Logger.Warn($"FileSystemWatcher for {fileName} entered an error state.");

                    // Perform an initial check immediately upon attachment
                    UpdateAvailabilityState(targetAppPublishPath, updateAvailabilityCallback, app);

                    // PHASE 3: The Heartbeat
                    // FileSystemWatcher is completely blind if its own root directory is renamed or deleted.
                    // We use a low-cost heartbeat to verify the directory still exists.
                    while (Directory.Exists(directory))
                    {
                        await Task.Delay(AppConfig.AppAvailabilityPollIntervalMs, _appLifetimeCts.Token);
                    }

                    // PHASE 4: Recovery
                    // If the code reaches this point, the parent directory was renamed or deleted.
                    // We clean up the stale watcher, force a UI update to 'False', and let the 
                    // outer loop drop us seamlessly back into Phase 1 to wait for it to return.
                    app.Dispatcher.Invoke(() =>
                    {
                        CleanupAvailabilityWatcher();
                        UpdateAvailabilityState(targetAppPublishPath, updateAvailabilityCallback, app);
                    });
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Availability monitor cycle failed for {fileName}; restarting after delay", ex);
                    try { CleanupAvailabilityWatcher(); } catch { /* ignore */ }
                    try { await Task.Delay(AppConfig.AppAvailabilityPollIntervalMs, _appLifetimeCts.Token); }
                    catch (TaskCanceledException) { return; }
                }
            }
        }

        private void UpdateAvailabilityState(string targetAppPublishPath, Action<bool> updateAvailabilityCallback, Application app)
        {
            // Dispatch to UI thread to safely update data-bound properties from the watcher's background thread
            app.Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(targetAppPublishPath))
                {
                    updateAvailabilityCallback(File.Exists(targetAppPublishPath));
                }
            });
        }

        /// <summary>
        /// Safely detaches event handlers and disposes of the current availability watcher to prevent 
        /// memory leaks and ensure a clean state before re-attachment or shutdown.
        /// </summary>
        private void CleanupAvailabilityWatcher()
        {
            if (_availabilityWatcher != null)
            {
                // Disable event raising first to prevent race conditions during detachment
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

                _availabilityWatcher.Dispose();
                _availabilityWatcher = null;
            }
        }

        /// <summary>
        /// Orchestrates the deterministic cleanup of application resources during the exit sequence.
        /// </summary>
        /// <param name="e">The <see cref="ExitEventArgs"/> containing the event data.</param>
        public void OnExit(ExitEventArgs e)
        {
            CleanupAvailabilityWatcher();
            TryDispose(() => _appLifetimeCts?.Cancel(), nameof(_appLifetimeCts));
            // Do NOT dispose _appLifetimeCts here - the async void monitor still
            // accesses .Token. Let the GC reclaim it after the monitor unwinds.
            TryDispose(() => _availabilityWatcher?.Dispose(), nameof(_availabilityWatcher));

            TryDispose(() => DbContext?.Dispose(), nameof(DbContext));
            TryDispose(() => SecureData?.Dispose(), nameof(SecureData));
            TryDispose(() => _protectedKeyProvider?.Dispose(), nameof(_protectedKeyProvider));
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