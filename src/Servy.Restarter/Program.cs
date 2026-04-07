using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Enums;
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
            Logger.Initialize("Servy.Restarter.log");

            if (args.Length == 0)
            {
                Logger.Error("Missing required argument: service name.");
                Environment.Exit(1);
                return;
            }

            var serviceName = args[0];

            if (string.IsNullOrWhiteSpace(serviceName))
            {
                Logger.Error("Service name cannot be empty.");
                Environment.Exit(1);
                return;
            }

            IServiceRestarter restarter = new ServiceRestarter();
            ILogger logger = new EventLogLogger(AppConfig.ServiceNameEventSource) { Prefix = serviceName };

            try
            {
                // Ensure event source exists
                Helper.EnsureEventSourceExists();

                // Load configuration from appsettings.restarter.json
                var config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!)
                    .AddJsonFile("appsettings.restarter.json", optional: true, reloadOnChange: true)
                    .Build();

                var restartTimeout = int.TryParse(config["RestartTimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : DefaultRestartTimeoutSeconds;

                if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel))
                {
                    logLevel = LogLevel.Info;
                }
                Logger.SetLogLevel(logLevel);
                logger.SetLogLevel(logLevel);

                if (!Enum.TryParse<DateRotationType>(config["LogRollingInterval"], true, out var dateRotationType))
                {
                    dateRotationType = DateRotationType.None;
                }
                Logger.SetDateRotationType(dateRotationType);

                var isEventLogEnabled = bool.TryParse(config["EnableEventLog"] ?? "true", out var elEnabled) && elEnabled;
                logger.SetIsEventLogEnabled(isEventLogEnabled);

                if (int.TryParse(config["LogRotationSizeMB"], out var size) && size > 0)
                {
                    Logger.SetLogRotationSize(size);
                }
                else
                {
                    Logger.SetLogRotationSize(Logger.DefaultLogRotationSizeMB);
                }

                string rawUseLocalTimeForRotationConfig = config["UseLocalTimeForRotation"] ?? AppConfig.DefaultUseLocalTimeForRotation.ToString();

                if (!bool.TryParse(rawUseLocalTimeForRotationConfig, out bool useLocalTimeForRotation))
                {
                    useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation;
                }
                Logger.SetUseLocalTimeForRotation(useLocalTimeForRotation);

                // Restart service
                Logger.Info($"Attempting to restart service '{serviceName}' using Servy.Restarter.exe.");
                restarter.RestartService(serviceName, TimeSpan.FromSeconds(restartTimeout));
                Logger.Info($"Successfully restarted service '{serviceName}'.");
            }
            catch (Exception ex)
            {
                logger.Error($"Servy.Restarter.exe failed to restart the service: {ex.Message}", ex);
            }
            finally
            {
                try
                {
                    // Dispose loggers
                    Logger.Shutdown();
                    logger.Dispose();
                }
                catch
                {
                    // Fail-silent
                }
            }

        }
    }
}
