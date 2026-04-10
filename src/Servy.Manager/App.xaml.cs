using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Manager.Resources;
using Servy.Manager.Views;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Servy.Manager
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
        public const string ResourcesNamespace = "Servy.Manager.Resources";

        #endregion

        #region Private Fields

        private SecureData _secureData;

        #endregion

        #region Properties

        /// <summary>
        /// Connection string.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Gets the file path for the AES encryption key.
        /// </summary>
        public string AESKeyFilePath { get; private set; }

        /// <summary>
        /// Gets the file path for the AES initialization vector (IV).
        /// </summary>
        public string AESIVFilePath { get; private set; }

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
        /// Servy Configuration App publish path.
        /// </summary>
        public string ConfigurationAppPublishPath { get; private set; }

        /// <summary>
        /// Indicates whether the configuration application is available.
        /// </summary>
        public bool IsConfigurationAppAvailable { get; private set; }

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
        public bool ForceSoftwareRendering { get; private set; }

        /// <summary>
        /// Log level for the application, loaded from configuration. This determines the verbosity of logs.
        /// </summary>
        public LogLevel LogLevel { get; private set; }

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
            Logger.Initialize("Servy.Manager.log");

            // Run the security check from Infrastructure
            if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
            {
                // 2. Format the message using your Resources
                string message = string.Format(
                    Strings.SqliteVersionWarningMessage,
                    detectedVersion,
                    AppConfig.MinRequiredSqliteVersion
                );

                string title = Strings.SqliteVersionWarningTitle;

                // 3. Show the warning
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Bit-shift to get the major tier (0, 1, or 2)
            // Tier 0 = No hardware acceleration
            // Tier 1 = Partial (DirectX 7/8)
            // Tier 2 = Full (DirectX 9+)
            var renderingTier = RenderCapability.Tier >> 16;
            var isRemote = SystemParameters.IsRemoteSession;

            // Check for manual override flag
            ForceSoftwareRendering = e.Args.Any(arg => arg.Equals(AppConfig.ForceSoftwareRenderingArg, StringComparison.OrdinalIgnoreCase));

            // 1. Log RenderingTier and RemoteSession
            Logger.Info($"Startup initialized. RenderingTier={renderingTier}, RemoteSession={isRemote}, ForceSoftwareRendering={ForceSoftwareRendering}");

            // 2. Rendering Fallback
            // This avoids issues caused by broken GPU drivers, RDP sessions, VMs, or old hardware.
            if (renderingTier == 0 || isRemote || ForceSoftwareRendering)
            {
                Logger.Warn("Low rendering capabilities detected. Forcing Software Rendering Mode.");
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            base.OnStartup(e);

            // 3. Fire-and-forget with safety net
            _ = InitializeApp(e).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.Flatten().InnerException;
                    Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Critical Startup Fault: {ex?.Message}");
                        Shutdown();
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
                // Explicitly dispose of the secure data provider to clear 
                // sensitive buffers and release key file handles.
                _secureData?.Dispose();
            }
            finally
            {
                // Ensure the base implementation is called so the 
                // application exit sequence completes correctly.
                base.OnExit(e);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Orchestrates the asynchronous application initialization sequence.
        /// </summary>
        /// <remarks>
        /// This method performs the following operations:
        /// <list type="bullet">
        /// <item>Configures global exception handlers for the <see cref="AppDomain"/> and <see cref="Application.DispatcherUnhandledException"/>.</item>
        /// <item>Displays the <see cref="SplashWindow"/> (unless suppressed via command-line arguments).</item>
        /// <item>Initializes system event sources and loads configuration from <c>appsettings.manager.json</c>.</item>
        /// <item>Spawns a background task to handle high-latency operations:
        ///     <list type="number">
        ///         <item>Ensures required directory structures exist.</item>
        ///         <item>Initializes the SQLite database and underlying schema.</item>
        ///         <item>Extracts embedded executables (Servy UI and Handle utility) to the local file system.</item>
        ///     </list>
        /// </item>
        /// <item>Instantiates and displays the <see cref="MainWindow"/> before dismissing the splash screen.</item>
        /// </list>
        /// </remarks>
        /// <param name="e">The <see cref="StartupEventArgs"/> containing command-line arguments. 
        /// If the first argument is "false", the splash screen will be disabled.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous initialization process.</returns>
        private async Task InitializeApp(StartupEventArgs e)
        {
            // Subscribe to unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, args2) =>
            {
                MessageBox.Show("Unhandled exception: " + args2.ExceptionObject);
            };

            DispatcherUnhandledException += (s, args2) =>
            {
                MessageBox.Show("UI thread exception: " + args2.Exception);
                args2.Handled = true;
            };

            // Initialize and show splash screen if enabled
            var showSplash = true;

            if (e.Args != null)
            {
                var positionalArgs = e.Args.Where(arg => !arg.Equals(AppConfig.ForceSoftwareRenderingArg, StringComparison.OrdinalIgnoreCase)).ToList();

                // First positional argument: Splash Screen (true/false)
                if (positionalArgs.Count > 0)
                {
                    bool.TryParse(positionalArgs[0], out showSplash);
                }
            }

            SplashWindow splash = null;
            try
            {
                splash = new SplashWindow();

                if (showSplash)
                {
                    splash.Show();

                    await Task.Yield(); // let UI render
                }

                // Ensure event source exists
                Helper.EnsureEventSourceExists();

                // Load configuration from appsettings.json
                var builder = new ConfigurationBuilder();
#if DEBUG
                builder.AddJsonFile("appsettings.manager.json", optional: true, reloadOnChange: true);
#else
                builder.SetBasePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!)
                       .AddJsonFile("appsettings.manager.json", optional: true, reloadOnChange: true);
#endif
                var config = builder.Build();

                ConnectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                AESKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                AESIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;
                RefreshIntervalInSeconds = int.TryParse(config["RefreshIntervalInSeconds"], out var result)
                    ? result
                    : AppConfig.DefaultRefreshIntervalInSeconds;
                PerformanceRefreshIntervalInMs = int.TryParse(config["PerformanceRefreshIntervalInMs"], out var presult)
                    ? presult
                    : AppConfig.DefaultPerformanceRefreshIntervalInMs;
                ConsoleRefreshIntervalInMs = int.TryParse(config["ConsoleRefreshIntervalInMs"], out var cresult)
                    ? cresult
                    : AppConfig.DefaultConsoleRefreshIntervalInMs;
                ConsoleMaxLines = int.TryParse(config["ConsoleMaxLines"], out var consoleMaxLines)
                    ? consoleMaxLines
                    : AppConfig.DefaultConsoleMaxLines;
                DependenciesRefreshIntervalInMs = int.TryParse(config["DependenciesRefreshIntervalInMs"], out var drresult)
                                    ? drresult
                                    : AppConfig.DefaultDependenciesRefreshIntervalInMs;

                if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel))
                {
                    logLevel = LogLevel.Info;
                }
                Logger.SetLogLevel(logLevel);
                LogLevel = logLevel;

                if (!Enum.TryParse<DateRotationType>(config["LogRollingInterval"], true, out var dateRotationType))
                {
                    dateRotationType = DateRotationType.None;
                }
                Logger.SetDateRotationType(dateRotationType);

                if (int.TryParse(config["LogRotationSizeMB"], out var size) && size > 0)
                {
                    Logger.SetLogRotationSize(size);
                }
                else
                {
                    Logger.SetLogRotationSize(Logger.DefaultLogRotationSizeMB);
                }

                string rawUseLocalTimeForRotationConfig = config["UseLocalTimeForRotation"] ?? AppConfig.DefaultUseLocalTimeForRotation.ToString();

                if (!bool.TryParse(rawUseLocalTimeForRotationConfig, out bool useLocalTimeForRotation))
                {
                    useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation;
                }
                Logger.SetUseLocalTimeForRotation(useLocalTimeForRotation);

#if DEBUG
                ConfigurationAppPublishPath = AppConfig.ConfigrationAppPublishDebugPath;
#else
                var baseDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? string.Empty;
                ConfigurationAppPublishPath = config["ConfigurationAppPublishPath"] ?? AppConfig.DefaultConfigrationAppPublishPath;
                // If the path is relative, combine it with the base directory
                if (!Path.IsPathRooted(ConfigurationAppPublishPath))
                {
                    ConfigurationAppPublishPath = Path.GetFullPath(Path.Combine(baseDirectory, ConfigurationAppPublishPath));
                }
#endif

                IsConfigurationAppAvailable = !string.IsNullOrEmpty(ConfigurationAppPublishPath) && File.Exists(ConfigurationAppPublishPath);

                // Run heavy startup work off UI thread
                await Task.Run(async () =>
                {
                    var stopwatch = Stopwatch.StartNew();

                    // Ensure db and security folders exist
                    AppFoldersHelper.EnsureFolders(ConnectionString, AESKeyFilePath, AESIVFilePath);

                    var asm = Assembly.GetExecutingAssembly();

                    var dbContext = new AppDbContext(ConnectionString);
                    DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

                    var dapperExecutor = new DapperExecutor(dbContext);
                    var protectedKeyProvider = new ProtectedKeyProvider(AESKeyFilePath, AESIVFilePath);
                    _secureData = new SecureData(protectedKeyProvider);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();

                    var serviceRepository = new ServiceRepository(dapperExecutor, _secureData, xmlSerializer, jsonSerializer);

                    var resourceHelper = new ResourceHelper(serviceRepository);

                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "exe"))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIExe}")
                        );
                    }

                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.HandleExe}")
                        );
                    }

#if DEBUG
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "pdb", false))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIFileName}.pdb")
                        );
                    }
#endif

                    stopwatch.Stop();

                    // Delay on UI thread if elapsed time is too short
                    if (showSplash && stopwatch.ElapsedMilliseconds < 1000)
                    {
                        await Task.Delay(500);
                    }

                });

                var main = new MainWindow();

                MainWindow = main;
                main.Show();
            }
            catch (Exception ex)
            {
                Logger.Error("Startup error", ex);
                MessageBox.Show("Startup error: " + ex.Message);
                Shutdown();
            }
            finally
            {
                if (showSplash && splash?.IsVisible == true)
                {
                    splash.Close();
                }
            }
        }

        #endregion
    }

}
