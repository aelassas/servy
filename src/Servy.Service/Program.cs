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
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
