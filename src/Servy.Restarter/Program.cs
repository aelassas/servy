using System;

/// <summary>
/// A simple console application to restart a Windows service.
/// </summary>
/// <remarks>
/// This application is intended to be used as a recovery action for services that need to be restarted.
/// </remarks>
namespace Servy.Restarter
{
    /// <summary>
    /// Program entry point for the service restarter console app.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main method. Expects a single argument: the service name to restart.
        /// </summary>
        /// <param name="args">Command line arguments. args[0] must be the service name.</param>
        public static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            var serviceName = args[0];
            IServiceRestarter restarter = new ServiceRestarter();

            try
            {
                restarter.RestartService(serviceName);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restarting service '{serviceName}': {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
