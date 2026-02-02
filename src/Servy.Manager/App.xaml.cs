using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Manager.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading.Tasks;
using System.Windows;

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

        #endregion

        #region Events

        /// <summary>
        /// Called when the WPF application starts.
        /// Loads configuration settings, initializes the database if necessary,
        /// subscribes to unhandled exception handlers, and extracts required embedded resources.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

            if (e.Args != null && e.Args.Length > 0)
            {
                bool.TryParse(e.Args[0], out showSplash);
            }

            var splash = new SplashWindow();

            if (showSplash)
            {
                splash.Show();

                await Task.Yield(); // let UI render
            }

            try
            {
                // Ensure event source exists
                Helper.EnsureEventSourceExists();

                // Load configuration from App.config
                var config = ConfigurationManager.AppSettings;

                ConnectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
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

#if DEBUG
                ConfigurationAppPublishPath = AppConfig.ConfigrationAppPublishDebugPath;
#else
                ConfigurationAppPublishPath = config["ConfigurationAppPublishPath"] ?? AppConfig.DefaultConfigrationAppPublishPath;
#endif

                //if (!File.Exists(ConfigurationAppPublishPath))
                //{
                //    MessageBox.Show($"Configurator App not found: {ConfigurationAppPublishPath}");
                //}

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
                    var securePassword = new SecurePassword(protectedKeyProvider);
                    var xmlSerializer = new XmlServiceSerializer();

                    var serviceRepository = new ServiceRepository(dapperExecutor, securePassword, xmlSerializer);

                    var resourceHelper = new ResourceHelper(serviceRepository);

                    // Copy Sysinternals from embedded resources
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        Current.Dispatcher.Invoke(() =>
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
                        Current.Dispatcher.Invoke(() =>
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
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resources.")
                        );
                    }

                    stopwatch.Stop();

                    // Delay on UI thread if elapsed time is too short
                    if (showSplash && stopwatch.ElapsedMilliseconds < 1000)
                    {
                        Current.Dispatcher.Invoke(async () =>
                        {
                            await Task.Delay(500);
                        }).Wait(); // Wait synchronously inside background thread
                    }

                });

                var main = new MainWindow();

                MainWindow = main;
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message);
                Shutdown();
            }
            finally
            {
                if (showSplash && splash.IsVisible)
                {
                    splash.Close();
                }
            }

        }


        #endregion

    }

}
