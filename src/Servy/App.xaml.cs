using Servy.Constants;
using Servy.Core;
using Servy.Core.Helpers;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Windows;
using AppConstants = Servy.Core.AppConstants;

namespace Servy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Servy Service executable filename.
        /// </summary>
        public const string ServyServiceExeFileName = "Servy.Service.Net48";

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
                // Load configuration from appsettings.json
                var config = ConfigurationManager.AppSettings;
                ConnectionString = config["DefaultConnection"] ?? AppConstants.DefaultConnectionString;
                AESKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConstants.DefaultAESKeyPath;
                AESIVFilePath = config["Security:AESIVFilePath"] ?? AppConstants.DefaultAESIVPath;

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
                if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, ServyServiceExeFileName, "exe"))
                {
                    MessageBox.Show($"Failed copying embedded resource: {ServyServiceExeFileName}.exe");
                }

#if DEBUG
                // Copy debug symbols from embedded resources (only in debug builds)
                if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, ServyServiceExeFileName, "pdb"))
                {
                    MessageBox.Show($"Failed copying embedded resource: {ServyServiceExeFileName}.pdb");
                }
#endif
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message);
            }
        }

       
    }
}
