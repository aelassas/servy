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

                // Extract required embedded resources
                CopyEmbeddedResource(ServyServiceExeFileName, "exe");
#if DEBUG
                CopyEmbeddedResource(ServyServiceExeFileName, "pdb");
#endif
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message);
            }
        }

        /// <summary>
        /// Kills all running processes with the name.
        /// This is necessary when replacing the embedded service executable.
        /// </summary>
        /// <param name="servyProcessName">Servy Process Name to kill.</param>
        public static void KillServyServiceIfRunning(string servyProcessName)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(servyProcessName))
                {
                    KillProcessAndChildren(process.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to terminate running service: " + ex.Message);
            }
        }

        /// <summary>
        /// Kills process and the entire process tree.
        /// </summary>
        /// <param name="pid">Process PID to kill.</param>
        private static void KillProcessAndChildren(int parentPid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Process WHERE ParentProcessId=" + parentPid);

            ManagementObjectCollection collection = searcher.Get();

            // Kill all child processes recursively first
            foreach (var mo in collection)
            {
                int childPid = Convert.ToInt32(mo["ProcessId"]);
                KillProcessAndChildren(childPid);
            }

            // Now kill the parent process
            try
            {
                Process parentProcess = Process.GetProcessById(parentPid);
                parentProcess.Kill();
                parentProcess.WaitForExit();
            }
            catch (ArgumentException)
            {
                // Process has already exited, no action needed
            }
            catch (Exception)
            {
                // Handle other exceptions if necessary
            }
        }

        /// <summary>
        /// Copies the embedded resource to the application's base directory
        /// if the file does not exist or if the embedded version is newer.
        /// </summary>
        /// <param name="fileName">The name of the embedded resource without extension.</param>
        /// <param name="extension">The extension of the embedded resource.</param>
        private void CopyEmbeddedResource(string fileName, string extension)
        {
            string targetFileName = $"{fileName}.{extension}";
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetFileName);
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = $"Servy.Resources.{fileName}.{extension}";

            bool shouldCopy = true;

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(asm);
                shouldCopy = embeddedResourceTime > existingFileTime;
            }

            if (shouldCopy)
            {
                if (extension.ToLower().Equals("exe"))
                    KillServyServiceIfRunning(targetFileName);

                using (Stream resourceStream = asm.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        MessageBox.Show("Embedded resource not found: " + resourceName);
                        return;
                    }

                    using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the last write time of the embedded resource using the assembly's timestamp.
        /// </summary>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <returns>The DateTime of the assembly's last write time in UTC, or current UTC time if unavailable.</returns>
        private DateTime GetEmbeddedResourceLastWriteTime(Assembly assembly)
        {
#pragma warning disable IL3000
            string assemblyPath = assembly.Location;
#pragma warning restore IL3000

            if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
            {
                return File.GetLastWriteTimeUtc(assemblyPath);
            }

            // Fallback: try to get the executable's last write time using AppContext.BaseDirectory + executable name
            try
            {
                var exeName = AppDomain.CurrentDomain.FriendlyName;
                var exePath = Path.Combine(AppContext.BaseDirectory, exeName);
                if (File.Exists(exePath))
                {
                    return File.GetLastWriteTimeUtc(exePath);
                }
            }
            catch
            {
                // Ignore exceptions, fallback to UtcNow below
            }

            return DateTime.UtcNow;
        }
    }
}
