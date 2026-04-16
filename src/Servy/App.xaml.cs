using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Resources;
using Servy.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

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

        #region Internal Properties

        internal AppDbContext DbContext { get; private set; }
        internal IServiceRepository ServiceRepository { get; private set; }
        internal ISecureData SecureData { get; private set; }

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
        public bool ForceSoftwareRendering { get; private set; }

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
            // 1. INITIALIZE LOGGER FIRST
            // Moved to the top so it's ready to capture errors in the handlers below.
            Logger.Initialize("Servy.log");

            // 2. Global AppDomain exceptions (Fatal)
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs args)
            {
                Exception ex = args.ExceptionObject as Exception;

                // Log the fatal error for post-mortem debugging
                Logger.Error("FATAL: AppDomain Unhandled Exception. Process is terminating.", ex);

                MessageBox.Show(
                    "A fatal error occurred and the application must close. Detailed diagnostics have been saved to the log file.",
                    "Servy - Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // No Handled property here; the process will terminate.
            };

            // 3. UI Thread exceptions (Dispatcher)
            DispatcherUnhandledException += delegate (object sender, DispatcherUnhandledExceptionEventArgs args)
            {
                // Log the exception details
                Logger.Error("UI Dispatcher Exception", args.Exception);

                // .NET 4.8 / C# 7.3 syntax for type checking
                bool isOutOfMemory = args.Exception is OutOfMemoryException;

                if (!isOutOfMemory)
                {
                    MessageBox.Show(
                        "An unexpected error occurred in the interface, but the application will attempt to continue. Details have been logged.",
                        "Servy - Unexpected Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Only mark as handled if it's not a memory exhaustion event
                    args.Handled = true;
                }
                else
                {
                    Logger.Error("Non-recoverable OutOfMemoryException detected. Shutting down.");
                    args.Handled = false;
                    Shutdown(1);
                }
            };

            if (!SecurityHelper.IsAdministrator())
            {
                MessageBox.Show(Strings.SecurityWarningMessage, Strings.SecurityWarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }

            // Run the security check from Infrastructure
            if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
            {
                string message = string.Format(
                    Strings.SqliteVersionWarningMessage,
                    detectedVersion,
                    AppConfig.MinRequiredSqliteVersion
                );

                string title = Strings.SqliteVersionWarningTitle;

                // Show a critical error icon and halt startup
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

                // CRITICAL: Shutdown the application to prevent exploitation
                Application.Current.Shutdown();
                return;
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
            _ = InitializeApp(e).ContinueWith(async t =>
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
                // Explicitly dispose of the secure data provider to clear 
                // sensitive buffers and release key file handles.
                SecureData?.Dispose();
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
        /// Asynchronously handles the application initialization lifecycle, including configuration loading, 
        /// resource extraction, and UI orchestration.
        /// </summary>
        /// <remarks>
        /// The initialization sequence performs the following:
        /// <list type="number">
        ///     <item>Attaches global exception handlers to capture unhandled background and UI thread errors.</item>
        ///     <item>Parses command-line arguments to determine splash screen visibility and target service deep-linking.</item>
        ///     <item>Displays the <see cref="SplashWindow"/> and yields to the UI thread to ensure it renders correctly.</item>
        ///     <item>Initializes the <see cref="IConfiguration"/> provider, supporting environment-specific JSON settings.</item>
        ///     <item>Offloads heavy I/O and database operations to a background task:
        ///         <list type="bullet">
        ///             <item>Verifies file system prerequisites (database and security folders).</item>
        ///             <item>Initializes the SQLite database schema.</item>
        ///             <item>Extracts embedded binaries (service UI and Sysinternals utilities) to the application directory.</item>
        ///         </list>
        ///     </item>
        ///     <item>Instantiates the <see cref="MainWindow"/>, optionally loading a specific service configuration if a <paramref name="serviceName"/> was provided.</item>
        ///     <item>Dismisses the splash screen and cleans up startup resources.</item>
        /// </list>
        /// </remarks>
        /// <param name="e">The <see cref="StartupEventArgs"/> containing:
        /// <list type="bullet">
        ///     <item><c>Args[0]</c>: A boolean string to toggle the splash screen.</item>
        ///     <item><c>Args[1]</c>: (Optional) The name of a service to load automatically on startup.</item>
        /// </list>
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous startup operation.</returns>
        private async Task InitializeApp(StartupEventArgs e)
        {
            // Initialize configuration and splash screen if enabled
            string serviceName = null;
            var showSplash = true;

            if (e.Args != null)
            {
                var positionalArgs = e.Args.Where(arg => !arg.Equals(AppConfig.ForceSoftwareRenderingArg, StringComparison.OrdinalIgnoreCase)).ToList();

                // First positional argument: Splash Screen (true/false)
                if (positionalArgs.Count > 0)
                {
                    bool.TryParse(positionalArgs[0], out showSplash);
                }

                // Second positional argument: Service Name
                if (positionalArgs.Count > 1)
                {
                    serviceName = positionalArgs[1];
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

                // Load configuration from App.config
                var config = ConfigurationManager.AppSettings;
                ConnectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
                AESKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                AESIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel))
                {
                    logLevel = LogLevel.Info;
                }
                Logger.SetLogLevel(logLevel);

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
                ManagerAppPublishPath = AppConfig.ManagerAppPublishDebugPath;
#else
                var baseDirectory = AppFoldersHelper.GetApplicationDirectory();
                ManagerAppPublishPath = config["ManagerAppPublishPath"] ?? AppConfig.DefaultManagerAppPublishPath;
                // If the path is relative, combine it with the base directory
                if (!Path.IsPathRooted(ManagerAppPublishPath))
                {
                    ManagerAppPublishPath = Path.GetFullPath(Path.Combine(baseDirectory, ManagerAppPublishPath));
                }
#endif

                IsManagerAppAvailable = !string.IsNullOrEmpty(ManagerAppPublishPath) && File.Exists(ManagerAppPublishPath);

                // Run heavy startup work off UI thread
                await Task.Run(async () =>
                {
                    var stopwatch = Stopwatch.StartNew();

                    // Ensure db and security folders exist
                    AppFoldersHelper.EnsureFolders(ConnectionString, AESKeyFilePath, AESIVFilePath);

                    var asm = Assembly.GetExecutingAssembly();

                    DbContext = new AppDbContext(ConnectionString);
                    DatabaseInitializer.InitializeDatabase(DbContext, SQLiteDbInitializer.Initialize);

                    var dapperExecutor = new DapperExecutor(DbContext);
                    var protectedKeyProvider = new ProtectedKeyProvider(AESKeyFilePath, AESIVFilePath);
                    SecureData = new SecureData(protectedKeyProvider);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();

                    ServiceRepository = new ServiceRepository(dapperExecutor, SecureData, xmlSerializer, jsonSerializer);

                    var resourceHelper = new ResourceHelper(ServiceRepository);

                    // Copy Sysinternals from embedded resources
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        await Current.Dispatcher.InvokeAsync(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.HandleExe}")
                        );
                    }

                    // Copy service executable from embedded resources
                    var resourceItems = new List<ResourceItem>
                    {
                        new ResourceItem{ FileNameWithoutExtension = AppConfig.ServyServiceUIFileName, Extension= "exe"},
                    };
#if DEBUG
                    // Copy debug symbols from embedded resources (only in debug builds)
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "pdb", false))
                    {
                        await Current.Dispatcher.InvokeAsync(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIFileName}.pdb")
                        );
                    }
#else
                    // Copy *.dll from embedded resources
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
                    // Copy embedded resources
                    if (!await resourceHelper.CopyResources(asm, ResourcesNamespace, resourceItems))
                    {
                        await Current.Dispatcher.InvokeAsync(() =>
                            MessageBox.Show($"Failed copying embedded resources.")
                        );
                    }

                    stopwatch.Stop();

                    // Delay on UI thread if elapsed time is too short
                    if (showSplash && stopwatch.ElapsedMilliseconds < 1000)
                    {
                        await Task.Delay(500);
                    }

                });


                var mainWindow = new MainWindow();
                mainWindow.Show();

                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    await mainWindow.LoadServiceConfiguration(serviceName);
                }

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
