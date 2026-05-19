using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;

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

                // 1. Ensure event source exists before doing anything else
                Helper.EnsureEventSourceExists();
                rootLogger = new EventLogLogger(AppConfig.EventSource);

                // 2. Load configuration
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppFoldersHelper.GetAppDirectory())
                    .AddJsonFile("appsettings.restarter.json", optional: true, reloadOnChange: false)
                    .Build();

                var connectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                // 3. Configure the GLOBAL logging
                var restartTimeout = int.TryParse(config["RestartTimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : AppConfig.DefaultRestarterTimeoutSeconds;

                // 4. PROMOTE / SCOPE the logger
                // Using the instance logger ensures that 'serviceName' is prepended 
                // and events are mirrored to the Windows Event Log.
                scopedLogger = rootLogger.CreateScoped(serviceName);

                // Centralized logging bootstrapper
                LoggerConfigurator.ConfigureFromAppSettings(config, instanceLogger: scopedLogger);

                // Sync Event Log enablement to the instance
                bool isEventLogEnabled;
                if (!bool.TryParse(config["EnableEventLog"], out isEventLogEnabled))
                {
                    isEventLogEnabled = AppConfig.DefaultEnableEventLog;
                }
                scopedLogger.SetIsEventLogEnabled(isEventLogEnabled);

                // 5. Initialize database and helpers
                dbContext = new AppDbContext(connectionString);

                var dapperExecutor = new DapperExecutor(dbContext);
                protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                secureData = new SecureData(protectedKeyProvider);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();

                var serviceRepository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                // 6. Validation
                if (serviceRepository.GetByName(serviceName) == null)
                {
                    scopedLogger.Error($"Service '{serviceName}' is not managed by Servy.");
                    Environment.ExitCode = 1;
                    return;
                }

                // 7. Execution
                scopedLogger.Info($"Attempting to restart service '{serviceName}' using Servy.Restarter.exe.");

                restarter.RestartService(serviceName, TimeSpan.FromSeconds(restartTimeout));

                scopedLogger.Info($"Successfully restarted service '{serviceName}'.");
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
                // Standard teardown
                secureData?.Dispose();              // dispose consumer first
                protectedKeyProvider?.Dispose();    // then the provider it built on

                dbContext?.Dispose();

                // ScopedLogger (proxy) disposal is a no-op, but included for pattern consistency.
                scopedLogger?.Dispose();

                // rootLogger actually disposes the unmanaged EventLog handle.
                rootLogger?.Dispose();

                
                Logger.Shutdown();
            }

        }
    }
}
