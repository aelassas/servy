using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Views;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

#if DEBUG
                ManagerAppPublishPath = AppConfig.ManagerAppPublishDebugPath;
#else
                ManagerAppPublishPath = config["ManagerAppPublishPath"] ?? AppConfig.DefaultManagerAppPublishPath;
#endif
                if (!File.Exists(ManagerAppPublishPath))
                {
                    MessageBox.Show($"Manager App not found: {ManagerAppPublishPath}");
                }

                IsManagerAppAvailable = !string.IsNullOrEmpty(ManagerAppPublishPath) && File.Exists(ManagerAppPublishPath);

                // Run heavy startup work off UI thread
                await Task.Run(() =>
                {
                    var stopwatch = Stopwatch.StartNew();

                    // Ensure db and security folders exist
                    AppFoldersHelper.EnsureFolders(ConnectionString, AESKeyFilePath, AESIVFilePath);

                    var asm = Assembly.GetExecutingAssembly();

                    // Copy service executable from embedded resources
                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "exe"))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIExe}")
                        );
                    }

                    // Copy Sysinternals from embedded resources
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
