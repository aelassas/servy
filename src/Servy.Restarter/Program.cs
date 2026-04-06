using System;
using System.Configuration;

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
        /// The default timeout, in seconds, for service restart operations within the wrapper service.
        /// </summary>
        /// <remarks>
        /// Set to 120 seconds to ensure maximum resiliency for background operations. 
        /// This extended duration allows the restarter to wait out long 'Pending' transitions 
        /// (e.g., heavy I/O cleanup or database flushes) without triggering a timeout 
        /// exception in the host service.
        /// </remarks>
        public const int DefaultRestartTimeoutSeconds = 120;

        /// <summary>
        /// Main method. Expects a single argument: the service name to restart.
        /// </summary>
        /// <param name="args">Command line arguments. args[0] must be the service name.</param>
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Missing required argument: service name.");
                Environment.Exit(1);
                return;
            }

            var serviceName = args[0];

            if (string.IsNullOrWhiteSpace(serviceName))
            {
                Console.Error.WriteLine("Service name cannot be empty.");
                Environment.Exit(1);
                return;
            }

            IServiceRestarter restarter = new ServiceRestarter();

            try
            {
                var config = ConfigurationManager.AppSettings;
                var restartTimeout = int.TryParse(config["RestartTimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : DefaultRestartTimeoutSeconds;

                restarter.RestartService(serviceName, TimeSpan.FromSeconds(restartTimeout));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error restarting service '{serviceName}': {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
