using System;
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
        /// Extracts the embedded Servy.Service.exe to the application's base directory if it doesn't exist
        /// or if the embedded resource is newer than the existing file.
        /// </summary>
        /// <param name="e">Startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    MessageBox.Show("Unhandled: " + e.ExceptionObject.ToString());
                };

                CopyEmbeddedResource("Servy.Service");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to extract service exe: " + ex.Message);
            }
        }

        /// <summary>
        /// Copies resource file to current folder.
        /// </summary>
        /// <param name="fileName">Resource filename.</param>
        private void CopyEmbeddedResource(string fileName)
        {
            string targetFileName = $"{fileName}.exe";
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetFileName);
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = $"Servy.Resources.{fileName}.exe";

            bool shouldCopy = true;

            if (File.Exists(targetPath))
            {
                // Get last write time of existing file
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);

                // Get last write time of embedded resource from assembly metadata
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(asm, resourceName);

                // Only copy if embedded resource is newer
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
        /// Retrieves the last write time of the embedded resource based on assembly's last write time.
        /// Since embedded resources do not have independent timestamps, this method uses the assembly's timestamp.
        /// </summary>
        /// <param name="assembly">Assembly containing the resource.</param>
        /// <param name="resourceName">Name of the embedded resource.</param>
        /// <returns>DateTime of the assembly's last write time in UTC.</returns>
        private DateTime GetEmbeddedResourceLastWriteTime(Assembly assembly, string resourceName)
        {
            // Embedded resources don't have timestamps, so fallback to assembly last write time
            string assemblyPath = assembly.Location;

            if (File.Exists(assemblyPath))
            {
                return File.GetLastWriteTimeUtc(assemblyPath);
            }

            // If assembly file not found, fallback to current UTC time to force copy
            return DateTime.UtcNow;
        }
    }



}
