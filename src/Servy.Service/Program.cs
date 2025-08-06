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

            if (!File.Exists(restarterPath))
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
                    }

                    using (var file = File.Create(restarterPath))
                    {
                        stream.CopyTo(file);
                    }
                }
            }

            ServiceBase[] ServicesToRun = new ServiceBase[]
            {
                new Service()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
