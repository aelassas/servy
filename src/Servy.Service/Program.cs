using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Service.Native;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace Servy.Service
{
    /// <summary>
    /// Contains the application entry point for the Servy Windows service.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The namespace in the assembly where the embedded service resources are located.
        /// </summary>
        private const string ResourcesNamespace = "Servy.Service.Resources";

        /// <summary>
        /// The base file name (without extension) of the embedded Servy Restarter executable.
        /// </summary>
        private const string ServyRestarterExeFileName = "Servy.Restarter";

        /// <summary>
        /// Main entry point of the Servy Windows service application.
        /// Extracts required embedded resources and starts the service host.
        /// </summary>
        static void Main()
        {
            _ = NativeMethods.FreeConsole();
            _ = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);

            // Copy service executable from embedded resources
            var asm = Assembly.GetExecutingAssembly();
            string eventSource = AppConfig.ServiceNameEventSource;

            // Ensure event source exists
            Helper.EnsureEventSourceExists();

            if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, ServyRestarterExeFileName, "exe", false))
            {
                EventLog.WriteEntry(
                    eventSource,
                    $"Failed copying embedded resource: {ServyRestarterExeFileName}.exe",
                    EventLogEntryType.Error
                );
                return; // stop if critical resource missing
            }

#if DEBUG
            // Copy debug symbols from embedded resources (only in debug builds)
            if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, ServyRestarterExeFileName, "pdb", false))
            {
                EventLog.WriteEntry(
                    eventSource,
                    $"Failed copying embedded resource: {ServyRestarterExeFileName}.pdb",
                    EventLogEntryType.Warning
                );
            }
#endif
            
            ServiceBase[] servicesToRun =
            {
                new Service()
            };
            ServiceBase.Run(servicesToRun);
        }

    }
}
