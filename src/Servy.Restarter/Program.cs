using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Restarter.Bootstrap;
using System;
using System.Configuration;
using System.IO;

namespace Servy.Restarter
{
    /// <summary>
    /// Program entry point for the service restarter console app: a simple console
    /// application that restarts a Servy Windows service.
    /// </summary>
    /// <remarks>
    /// Intended to be used as an SCM recovery action for services that need to be restarted.
    /// Expects the service name as <c>args[0]</c> and sets a non-zero exit code on failure.
    /// </remarks>
    public static class Program
    {
        /// <summary>
        /// Main method. Expects one required argument: the service name to restart.
        /// An optional second argument can specify a custom logging directory (primarily for testing isolation).
        /// </summary>
        /// <param name="args">Command line arguments. args[0] must be the service name, optional args[1] specifies custom log directory.</param>
        public static void Main(string[] args)
        {
            Run(args, restarter: null);
        }

        /// <summary>
        /// Implementation of <see cref="Main(string[])"/> with an injectable restarter, used as the test seam.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <param name="restarter">Optional restarter instance for dependency injection testing.</param>
        /// <param name="environment">
        /// Optional seam over the machine-touching start-up calls (the Windows event source, the event-log
        /// logger and the Service Control Manager lookup of the service's executable), used for testing. When <see langword="null"/> the
        /// production <see cref="RestarterBootstrapEnvironment"/> is created, and it forwards every call
        /// unchanged to what this method ran inline before the seam existed.
        /// </param>
        internal static void Run(string[] args, IServiceRestarter restarter, IRestarterBootstrapEnvironment environment = null)
        {
            string customLogDir = args.Length > 1 ? args[1] : null;
            // The wrapper passes logs\service\<ServiceName>\, the only log folder its service account can write
            Logger.Initialize(AppConfig.ServyRestarterLogFileName, logDirectory: customLogDir);

            IServyLogger rootLogger = null; // Declare as nullable for safe finally disposal
            IServyLogger scopedLogger = null;

            try
            {
                if (args.Length == 0)
                {
                    Logger.Error("Missing required argument: service name.");
                    Environment.ExitCode = 1;
                    return;
                }

                var serviceName = args[0];

                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    Logger.Error("Service name cannot be empty.");
                    Environment.ExitCode = 1;
                    return;
                }

                environment = environment ?? new RestarterBootstrapEnvironment();

                // 1. Event Log source is best-effort: the file logger is already up, and a restart
                //    must not be blocked by a reporting-channel failure.
                try
                {
                    environment.EnsureEventSourceExists();
                    rootLogger = environment.CreateEventLogLogger(isEventLogEnabled: true);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Event Log source unavailable; continuing with file logging only.", ex);
                    rootLogger = environment.CreateEventLogLogger(isEventLogEnabled: false);
                }

                // 2. Load configuration
                var config = ConfigurationManager.AppSettings;

                // 3. Parse the restart timeout
                var restartTimeout = ConfigParser.GetConfigInt(config, "RestartTimeoutSeconds",
                                                  AppConfig.DefaultRestarterTimeoutSeconds,
                                                  min: 1, max: AppConfig.MaxRestarterTimeoutSeconds);

                // 4. PROMOTE / SCOPE the logger
                // Using the instance logger ensures that 'serviceName' is prepended
                // and events are mirrored to the Windows Event Log.
                scopedLogger = rootLogger.CreateScoped(serviceName);

                // 5. Create the service restarter
                restarter = restarter ?? new ServiceRestarter(logger: scopedLogger);

                // 6. Configure the GLOBAL logging (centralized bootstrapper)
                LoggerConfigurator.ConfigureFromAppSettings(config, instanceLogger: scopedLogger);
                CoreSettingsLoader.WarnAboutIgnoredSettings(config, "Servy.Restarter.Net48.exe.config", scopedLogger);

                var maxHostWaitSeconds = AppConfig.RestarterExeMaxWaitMs / AppConfig.MillisecondsPerSecond;
                if (restartTimeout > maxHostWaitSeconds)
                {
                    scopedLogger.Warn($"Configured RestartTimeoutSeconds ({restartTimeout}s) exceeds the host service execution wait limit ({maxHostWaitSeconds}s). " +
                                      $"If this restart is driven by the Servy host service recovery path, it will be force-killed after {maxHostWaitSeconds} seconds.");
                }

                // 7. Validation. The restarter runs under the service account, which has no access to Servy.db, so the
                // Service Control Manager decides: the service must run one of Servy's wrappers.
                if (!IsServyWrapperImagePath(environment.GetServiceImagePath(serviceName)))
                {
                    scopedLogger.Error($"Service '{serviceName}' is not managed by Servy.");
                    Environment.ExitCode = 1;
                    return;
                }

                // 8. Execution
                scopedLogger.Info($"Attempting to restart service '{serviceName}' using Servy.Restarter.exe.");

                var result = restarter.RestartService(serviceName, TimeSpan.FromSeconds(restartTimeout));

                if (result == RestartResult.ServiceNotFound)
                {
                    scopedLogger.Warn($"Service '{serviceName}' no longer exists in the SCM; nothing to restart.");
                    Environment.ExitCode = 1;
                }
                else
                {
                    scopedLogger.Info($"Successfully restarted service '{serviceName}'.");
                }
            }
            catch (Exception ex)
            {
                // Resilient fallback: scoped > root > static
                var finalLogger = scopedLogger ?? rootLogger;
                if (finalLogger != null)
                {
                    finalLogger.Error("Servy.Restarter.exe failed to restart the service.", ex);
                }
                else
                {
                    Logger.Error("Servy.Restarter.exe failed to initialize or execute.", ex);
                }
                Environment.ExitCode = 1;
            }
            finally
            {
                try { scopedLogger?.Dispose(); } catch (Exception ex) { Logger.Warn("Failed to dispose scoped logger.", ex); }
                try { rootLogger?.Dispose(); } catch (Exception ex) { Logger.Warn("Failed to dispose root EventLogLogger.", ex); }
                try { Logger.Shutdown(); } catch { /* nothing left to log with */ }
            }
        }
        /// <summary>
        /// Determines whether a service's executable command line runs one of Servy's wrappers,
        /// <c>Servy.Service.exe</c> or <c>Servy.Service.CLI.exe</c>.
        /// </summary>
        /// <param name="imagePath">The service's <c>ImagePath</c>, as the Service Control Manager stores it; <see langword="null"/> when the service is not installed.</param>
        /// <returns><see langword="true"/> when the executable's file name is one of the wrappers.</returns>
        internal static bool IsServyWrapperImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return false;

            var commandLine = imagePath.Trim();
            string executable;
            if (commandLine.StartsWith("\"", StringComparison.Ordinal))
            {
                var closing = commandLine.IndexOf('"', 1);
                if (closing < 0)
                    return false;
                executable = commandLine.Substring(1, closing - 1);
            }
            else
            {
                var space = commandLine.IndexOf(' ');
                executable = space < 0 ? commandLine : commandLine.Substring(0, space);
            }

            string fileName;
            try
            {
                fileName = Path.GetFileName(executable);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return string.Equals(fileName, AppConfig.ServyServiceUIExe, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, AppConfig.ServyServiceCLIExe, StringComparison.OrdinalIgnoreCase);
        }
    }
}
