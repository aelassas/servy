﻿using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Views;
using System;
using System.Collections.Generic;
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

                    // Copy Sysinternals from embedded resources
                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
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
                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceUIFileName, "pdb", false))
                    {
                        Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed copying embedded resource: {AppConfig.ServyServiceUIFileName}.pdb")
                        );
                    }
#else
                    // Copy *.dll from embedded resources
                    resourceItems.AddRange(new List<ResourceItem>
                    {
                        new ResourceItem{ FileNameWithoutExtension = "Servy.Core", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "Dapper", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "Microsoft.Bcl.AsyncInterfaces", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "Newtonsoft.Json", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "Servy.Infrastructure", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "System.Data.SQLite", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "System.Runtime.CompilerServices.Unsafe", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "System.Threading.Tasks.Extensions", Extension= "dll"},
                        new ResourceItem{ FileNameWithoutExtension = "SQLite.Interop",  Extension= "dll", Subfolder = "x64"},
                        new ResourceItem{ FileNameWithoutExtension = "SQLite.Interop", Extension= "dll", Subfolder = "x86"},
                    });
#endif
                    // Copy embedded resources
                    CopyResources(asm, resourceItems);

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
        /// Copies the specified embedded resources from the given assembly and handles errors by 
        /// showing a message box if the operation fails.
        /// </summary>
        /// <param name="asm">
        /// The <see cref="Assembly"/> that contains the embedded resources.
        /// </param>
        /// <param name="dllResources">
        /// A list of <see cref="ResourceItem"/> objects representing the resources to copy.
        /// </param>
        /// <param name="stopServices">
        /// If <c>true</c>, running Servy services will be stopped before copying and restarted afterward. 
        /// Default is <c>true</c>.
        /// </param>
        /// <remarks>
        /// This method calls <see cref="ResourceHelper.CopyResources"/> to perform the actual copying.  
        /// If the operation fails, the user is notified via a message box on the UI thread.
        /// </remarks>
        private void CopyResources(Assembly asm, List<ResourceItem> dllResources, bool stopServices = true)
        {
            if (!ResourceHelper.CopyResources(asm, ResourcesNamespace, dllResources, stopServices))
            {
                Current.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Failed copying embedded resources.")
                );
            }
        }

        #endregion

    }
}
