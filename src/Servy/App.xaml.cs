using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Views;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace Servy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        private const string ResourcesNamespace = "Servy.Resources";

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
        /// Called when the WPF application starts.
        /// Loads configuration settings, initializes the database if necessary,
        /// subscribes to unhandled exception handlers, and extracts required embedded resources.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

                // Ensure db and security folders exist
                AppFoldersHelper.EnsureFolders(ConnectionString, AESKeyFilePath, AESIVFilePath);

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

                var asm = Assembly.GetExecutingAssembly();

                // Copy service executable from embedded resources
                if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "exe"))
                {
                    MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIExe}");
                }

                // Copy Sysinternals from embedded resources
                if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe"))
                {
                    MessageBox.Show($"Failed copying embedded resource: {AppConfig.HandleExe}");
                }

#if DEBUG
                // Copy debug symbols from embedded resources (only in debug builds)
                if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "pdb"))
                {
                    MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIFileName}.pdb");
                }
#else

                // Copy Servy.Core.dll from embedded resources
                if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyCoreDllName, "dll"))
                {
                    MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyCoreDllName}.dll");
                }
#endif
                string serviceName = null;

                if (e.Args != null && e.Args.Length > 0)
                {
                    serviceName = e.Args.FirstOrDefault();
                }

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
            }
        }

    }
}
