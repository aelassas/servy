using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Diagnostics;

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

            Logger.Initialize("Servy.Restarter.log");

            var serviceName = args[0];
            IServiceRestarter restarter = new ServiceRestarter();
            ILogger logger = new EventLogLogger(AppConfig.ServiceNameEventSource) { Prefix = serviceName };

            try
            {
                // Load configuration from appsettings.restarter.json
                var config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!)
                    .AddJsonFile("appsettings.restarter.json", optional: true, reloadOnChange: true)
                    .Build();

                if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel))
                {
                    logLevel = LogLevel.Info;
                }
                Logger.SetLogLevel(logLevel);
                logger.SetLogLevel(logLevel);

                if (int.TryParse(config["LogRotationSizeMB"], out var size) && size > 0)
                {
                    Logger.SetLogRotationSize(size);
                }
                else
                {
                    Logger.SetLogRotationSize(Logger.DefaultLogRotationSizeMB);
                }

                // Restart service
                logger.Info($"Attempting to restart service '{serviceName}' using Servy.Restarter.exe.");

                // Ensure event source exists
                Helper.EnsureEventSourceExists();

                restarter.RestartService(serviceName);
                logger.Info($"Successfully restarted service '{serviceName}'.");

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                logger.Error($"Servy.Restarter.exe failed to restart the service: {ex.Message}", ex);
                Environment.Exit(1);
            }
            finally
            {
                Logger.Shutdown();
            }

        }
    }
}
