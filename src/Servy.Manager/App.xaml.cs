using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Manager.Views;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        /// Servy Configuration App publish path.
        /// </summary>
        public string ConfigurationAppPublishPath { get; private set; }

        /// <summary>
        /// Indicates whether the configuration application is available.
        /// </summary>
        public bool IsConfigurationAppAvailable { get; private set; }

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
                // Load configuration from App.config
                var config = ConfigurationManager.AppSettings;

                ConnectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
                AESKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                AESIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;
                RefreshIntervalInSeconds = int.TryParse(config["RefreshIntervalInSeconds"], out var result)
                    ? result
                    : AppConfig.DefaultRefreshIntervalInSeconds;

#if DEBUG
                ConfigurationAppPublishPath = AppConfig.ConfigrationAppPublishDebugPath;
#else
                ConfigurationAppPublishPath = config["ConfigurationAppPublishPath"] ?? AppConfig.DefaultConfigrationAppPublishPath;
#endif

                if (!File.Exists(ConfigurationAppPublishPath))
                {
                    MessageBox.Show($"Configurator App not found: {ConfigurationAppPublishPath}");
                }

                IsConfigurationAppAvailable = !string.IsNullOrEmpty(ConfigurationAppPublishPath) && File.Exists(ConfigurationAppPublishPath);

                // Run heavy startup work off UI thread
                await Task.Run(() =>
                {
                    var stopwatch = Stopwatch.StartNew();

                    // Ensure db and security folders exist
                    AppFoldersHelper.EnsureFolders(ConnectionString, AESKeyFilePath, AESIVFilePath);

                    var asm = Assembly.GetExecutingAssembly();

                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "exe"))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIExe}")
                        );
                    }

                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.HandleExe}")
                        );
                    }

#if DEBUG
                    // Copy debug symbols from embedded resources (only in debug builds)
                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "pdb", false))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIFileName}.pdb")
                        );
                    }
#else
                    // Copy *.dll from embedded resources
                    CopyDllResource(asm, "Servy.Core");
                    CopyDllResource(asm, "Dapper");
                    CopyDllResource(asm, "Microsoft.Bcl.AsyncInterfaces");
                    CopyDllResource(asm, "Newtonsoft.Json");
                    CopyDllResource(asm, "Servy.Infrastructure");
                    CopyDllResource(asm, "System.Data.SQLite");
                    CopyDllResource(asm, "System.Runtime.CompilerServices.Unsafe");
                    CopyDllResource(asm, "System.Threading.Tasks.Extensions");

                    // Copy SQLite interop dlls for x64 and x86
                    CopyDllResource(asm, "SQLite.Interop", true, "x64");
                    CopyDllResource(asm, "SQLite.Interop", true, "x86");
#endif

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


        #region Helpers

        /// <summary>
        /// Copies an embedded DLL resource from the specified assembly to the target directory.
        /// </summary>
        /// <param name="asm">The assembly containing the embedded resource.</param>
        /// <param name="dllFileNameWithoutExtension">The DLL file name without extension (e.g., "SQLite.Interop").</param>
        /// <param name="stopServices">
        /// Whether to stop running services before copying the resource. Default is <c>true</c>.
        /// </param>
        /// <param name="subfolder">
        /// An optional subfolder within the resources namespace where the DLL is located. Default is <c>null</c>.
        /// </param>
        /// <remarks>
        /// If the copy fails, a message box is shown on the UI thread.
        /// </remarks>
        private void CopyDllResource(Assembly asm, string dllFileNameWithoutExtension, bool stopServices = true, string subfolder = null)
        {
            if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, dllFileNameWithoutExtension, "dll", stopServices, subfolder))
            {
                Current.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Failed copying embedded resource: {dllFileNameWithoutExtension}.dll")
                );
            }
        }

        #endregion

    }

}
