using System.ServiceProcess;

namespace Servy.Service
{
    internal static class Program
    {
        /// <summary>
        /// Main entry point of the application.
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
