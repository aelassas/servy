using System;
using System.Diagnostics;
using System.IO;
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
        /// Called when the WPF application starts.
        /// Subscribes to unhandled exceptions, stops the background service if it's running,
        /// and extracts the embedded Servy.Service.exe to the application's base directory if needed.
        /// </summary>
        /// <param name="e">Startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    MessageBox.Show("Unhandled: " + args.ExceptionObject.ToString());
                };

                KillServyServiceIfRunning();
                CopyEmbeddedResource("Servy.Service");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message);
            }
        }

        /// <summary>
        /// Kills all running processes with the name Servy.Service.exe and the entire process tree
        /// This is necessary when replacing the embedded service executable.
        /// </summary>
        private void KillServyServiceIfRunning()
        {
            const string processName = "Servy.Service";

            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    process.Kill(true);
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                // Handle cases where the process may have already exited or other exceptions
                MessageBox.Show("Failed to terminate running service: " + ex.Message);
            }
        }

        /// <summary>
        /// Copies the embedded executable resource to the application's base directory
        /// if the file does not exist or if the embedded version is newer.
        /// </summary>
        /// <param name="fileName">The name of the embedded resource without extension.</param>
        private void CopyEmbeddedResource(string fileName)
        {
            string targetFileName = $"{fileName}.exe";
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetFileName);
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = $"Servy.Resources.{fileName}.exe";

            bool shouldCopy = true;

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(asm, resourceName);
                shouldCopy = embeddedResourceTime > existingFileTime;
            }

            if (shouldCopy)
            {
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
        /// <param name="resourceName">The name of the embedded resource.</param>
        /// <returns>The DateTime of the assembly's last write time in UTC, or current UTC time if unavailable.</returns>
        private DateTime GetEmbeddedResourceLastWriteTime(Assembly assembly, string resourceName)
        {
            string assemblyPath = assembly.Location;

            if (File.Exists(assemblyPath))
            {
                return File.GetLastWriteTimeUtc(assemblyPath);
            }

            return DateTime.UtcNow;
        }
    }
}
