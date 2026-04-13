using Servy.Core.Native;
using System.ServiceProcess;

namespace Servy.Service
{
    /// <summary>
    /// Contains the application entry point for the Servy Windows service.
    /// </summary>
    internal static class Program
    {

        /// <summary>
        /// Main entry point of the Servy Windows service application.
        /// Extracts required embedded resources and starts the service host.
        /// </summary>
        internal static void Main(string[] args)
        {
            _ = NativeMethods.FreeConsole();
            _ = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);

            ServiceBase[] servicesToRun =
            {
                new Service()
            };
            ServiceBase.Run(servicesToRun);
        }

    }
}
