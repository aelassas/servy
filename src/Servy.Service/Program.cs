using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace Servy.Service
{
    /// <summary>
    /// Contains the program entry point.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Main entry point of the application.
        /// Initializes and runs the Windows service.
        /// </summary>
        static void Main()
        {
            var restarterPath = Path.Combine(AppContext.BaseDirectory, "Servy.Restarter.exe");
            var resourceName = "Servy.Service.Resources.Servy.Restarter.exe";

            var assembly = Assembly.GetExecutingAssembly();

            // Check if the embedded resource exists
            using (var embeddedStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (embeddedStream == null)
                    throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");

                bool copyFile = true;

                if (File.Exists(restarterPath))
                {
                    // Compare last write times
                    var fileLastWrite = File.GetLastWriteTimeUtc(restarterPath);

                    // For embedded resource last write, use assembly last write time as proxy
                    var assemblyLocation = assembly.Location;
                    var assemblyLastWrite = File.GetLastWriteTimeUtc(assemblyLocation);

                    if (fileLastWrite >= assemblyLastWrite)
                    {
                        // Existing file is up-to-date or newer, skip overwrite
                        copyFile = false;
                    }
                }

                if (copyFile)
                {
                    using (var fileStream = File.Create(restarterPath))
                    {
                        embeddedStream.CopyTo(fileStream);
                    }
                }
            }

            ServiceBase[] ServicesToRun =
            {
                new Service()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
