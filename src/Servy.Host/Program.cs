using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;

namespace Servy.Host
{
    /// <summary>
    /// Contains the application entry point for the Servy Host Windows service.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Main entry point of the Servy Host Windows service application.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the service.</param>
        [ExcludeFromCodeCoverage]
        internal static void Main(string[] args)
        {
            ServiceBase[] servicesToRun =
            {
                new Service()
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
