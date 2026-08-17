using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;

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
        /// Main method. Expects a single argument: the service name to restart.
        /// </summary>
        /// <param name="args">Command line arguments. args[0] must be the service name.</param>
        public static void Main(string[] args)
        {
            Logger.Initialize("Servy.Restarter.log");

            IServiceRestarter restarter = new ServiceRestarter();
            IServyLogger? rootLogger = null; // Declare as nullable for safe finally disposal
            IServyLogger? scopedLogger = null;
            AppDbContext? dbContext = null;
            SecureData? secureData = null;
            ProtectedKeyProvider? protectedKeyProvider = null;

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

                // 1. Event Log source is best-effort: the file logger is already up, and a restart
                //    must not be blocked by a reporting-channel failure.
                try
                {
                    Helper.EnsureEventSourceExists();
                    rootLogger = new EventLogLogger(AppConfig.EventSource);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Event Log source unavailable; continuing with file logging only.", ex);
                    rootLogger = new EventLogLogger(AppConfig.EventSource, isEventLogEnabled: false);
                }

                // 2. Load configuration
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppFoldersHelper.GetAppDirectory())
                    .AddJsonFile("appsettings.restarter.json", optional: true, reloadOnChange: false)
                    .Build();

                var connectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                // 3. Parse the restart timeout
                var restartTimeout = ConfigParser.GetConfigInt(config, "RestartTimeoutSeconds",
                                                                  AppConfig.DefaultRestarterTimeoutSeconds,
                                                                  min: 1, max: AppConfig.MaxRestarterTimeoutSeconds);

                // 4. PROMOTE / SCOPE the logger
                // Using the instance logger ensures that 'serviceName' is prepended
                // and events are mirrored to the Windows Event Log.
                scopedLogger = rootLogger.CreateScoped(serviceName);

                // 5. Configure the GLOBAL logging (centralized bootstrapper)
                LoggerConfigurator.ConfigureFromAppSettings(config, instanceLogger: scopedLogger);

                // CVE-2025-6965 Mitigation: Validate SQLite version before opening connection
                if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
                {
                    scopedLogger.Error($"[FATAL] Vulnerable SQLite version detected: {detectedVersion}. " +
                                          $"Minimum required: {AppConfig.MinRequiredSqliteVersion} (CVE-2025-6965 mitigation).");

                    Environment.ExitCode = 1;
                    return;
                }

                // 6. Initialize database and helpers
                dbContext = new AppDbContext(connectionString);

                var dapperExecutor = new DapperExecutor(dbContext);
                protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                secureData = new SecureData(protectedKeyProvider);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();

                var serviceRepository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                // 7. Validation
                if (serviceRepository.GetByName(serviceName, decrypt: false) == null)
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
                try { secureData?.Dispose(); } catch (Exception ex) { Logger.Warn("Failed to dispose SecureData.", ex); }
                try { protectedKeyProvider?.Dispose(); } catch (Exception ex) { Logger.Warn("Failed to dispose ProtectedKeyProvider.", ex); }
                try { dbContext?.Dispose(); } catch (Exception ex) { Logger.Warn("Failed to dispose AppDbContext.", ex); }
                try { scopedLogger?.Dispose(); } catch (Exception ex) { Logger.Warn("Failed to dispose scoped logger.", ex); }
                try { rootLogger?.Dispose(); } catch (Exception ex) { Logger.Warn("Failed to dispose root EventLogLogger.", ex); }
                try { Logger.Shutdown(); } catch { /* nothing left to log with */ }
            }
        }
    }
}
