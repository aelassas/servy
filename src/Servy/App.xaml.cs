using System;
using System.Diagnostics;
using System.IO;
using System.Management;
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
        /// Servy Service executable filename.
        /// </summary>
        public const string ServyServiceExeFileName = "Servy.Service";

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

                CopyEmbeddedResource(ServyServiceExeFileName);
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
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(asm);
                shouldCopy = embeddedResourceTime > existingFileTime;
            }

            if (shouldCopy)
            {
                KillServyServiceIfRunning(fileName);

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
            string assemblyPath = assembly.Location;

            if (File.Exists(assemblyPath))
            {
                return File.GetLastWriteTimeUtc(assemblyPath);
            }

            return DateTime.UtcNow;
        }
    }
}
