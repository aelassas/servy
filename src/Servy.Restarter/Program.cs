using System;
using System.ServiceProcess;

/// <summary>
/// A simple console application to restart a Windows service.
/// </summary>
/// <remarks>
/// This application is intended to be used as a recovery action for services that need to be restarted.
/// </remarks>
namespace Servy.Restarter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) return;

            var serviceName = args[0];

            try
            {
                using (var controller = new ServiceController(serviceName))
                {
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restarting service '{serviceName}': {ex.Message}");
            }
        }
    }
}
