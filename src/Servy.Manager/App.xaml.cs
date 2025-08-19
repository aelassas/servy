using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System;
using System.Configuration;
using System.IO;
using System.Windows;

namespace Servy.Manager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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

        /// <summary>
        /// Called when the WPF application starts.
        /// Loads configuration settings, initializes the database if necessary,
        /// subscribes to unhandled exception handlers, and extracts required embedded resources.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Load configuration from App.confg
                var config = ConfigurationManager.AppSettings;

                ConnectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
                AESKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                AESIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;
                RefreshIntervalInSeconds = int.TryParse(config["RefreshIntervalInSeconds"], out var result) ? result : AppConfig.DefaultRefreshIntervalInSeconds;

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

                // Ensure db and security folders exist
                AppFoldersHelper.EnsureFolders(ConnectionString, AESKeyFilePath, AESIVFilePath);

                // Initialize database
                var dbContext = new AppDbContext(ConnectionString);
                DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

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

            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message);
            }
        }
    }

}
