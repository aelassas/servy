using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Views;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

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
            // This avoids issues caused by broken GPU drivers, RDP sessions, VMs, or old hardware.
            // Tier 0 = No hardware acceleration
            // Tier 1 = Partial (DirectX 7/8)
            // Tier 2 = Full (DirectX 9+)
            var renderingTier = RenderCapability.Tier >> 16;
            var isRemote = SystemParameters.IsRemoteSession;

            Debug.WriteLine($"RenderingTier={renderingTier}, RemoteSession={isRemote}");

            if (renderingTier == 0 || isRemote)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            base.OnStartup(e);

            // Start the sequence without blocking the UI thread
            _ = InitializeApp(e).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // This runs if an exception escaped the internal try/catch
                    Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Critical Startup Fault: " + t.Exception?.Flatten().InnerException?.Message);
                        Shutdown();
                    });
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
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

            // Subscribe to unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show("Unhandled exception: " + args.ExceptionObject);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show("UI thread exception: " + args.Exception);
                args.Handled = true;
            };

            // Initialize configuration and splash screen if enabled
            string serviceName = null;
            var showSplash = true;

            if (e.Args != null)
            {
                if (e.Args.Length > 0)
                {
                    bool.TryParse(e.Args[0], out showSplash);
                }
                if (e.Args.Length > 1)
                {
                    serviceName = e.Args[1];
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
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
#else
                builder.SetBasePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!)
                       .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
#endif
                var config = builder.Build();

                ConnectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                AESKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                AESIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

#if DEBUG
                ManagerAppPublishPath = AppConfig.ManagerAppPublishDebugPath;
#else
                ManagerAppPublishPath = config["ManagerAppPublishPath"] ?? AppConfig.DefaultManagerAppPublishPath;
#endif
                //if (!File.Exists(ManagerAppPublishPath))
                //{
                //    MessageBox.Show($"Manager App not found: {ManagerAppPublishPath}");
                //}

                IsManagerAppAvailable = !string.IsNullOrEmpty(ManagerAppPublishPath) && File.Exists(ManagerAppPublishPath);

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
                    var secureData = new SecureData(protectedKeyProvider);
                    var xmlSerializer = new XmlServiceSerializer();

                    var serviceRepository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer);

                    var resourceHelper = new ResourceHelper(serviceRepository);

                    // Copy service executable from embedded resources
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "exe"))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIExe}")
                        );
                    }

                    // Copy Sysinternals from embedded resources
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.HandleExe}")
                        );
                    }

#if DEBUG
                    // Copy debug symbols from embedded resources (only in debug builds)
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

                var mainWindow = new MainWindow();
                mainWindow.Show();

                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    await mainWindow.LoadServiceConfiguration(serviceName);
                }

            }
            catch (Exception ex)
            {
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
