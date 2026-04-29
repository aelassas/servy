using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Enums;
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

            IServiceRestarter restarter = new ServiceRestarter();
            IServyLogger rootLogger = new EventLogLogger(AppConfig.EventSource);
            IServyLogger? scopedLogger = null;
            AppDbContext? dbContext = null;
            SecureData? secureData = null;

            try
            {
                // 1. Ensure event source exists before doing anything else
                Helper.EnsureEventSourceExists();

                // 2. Load configuration
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppFoldersHelper.GetAppDirectory())
                    .AddJsonFile("appsettings.restarter.json", optional: true, reloadOnChange: true)
                    .Build();

                var connectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                // 3. Configure the GLOBAL logging
                var restartTimeout = int.TryParse(config["RestartTimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : DefaultRestartTimeoutSeconds;

                // Set Log Level
                if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel))
                {
                    logLevel = LogLevel.Info;
                }
                Logger.SetLogLevel(logLevel);

                // Set Rotation Type
                if (!Enum.TryParse<DateRotationType>(config["LogRollingInterval"], true, out var dateRotationType))
                {
                    dateRotationType = DateRotationType.None;
                }
                Logger.SetDateRotationType(dateRotationType);

                // Set Rotation Size
                if (int.TryParse(config["LogRotationSizeMB"], out var size) && size > 0) Logger.SetLogRotationSize(size);
                else Logger.SetLogRotationSize(AppConfig.DefaultRotationSizeMB);

                if (int.TryParse(config["MaxBackupLogFiles"], out var maxBackupFiles) && maxBackupFiles >= 0) Logger.SetMaxBackupLogFiles(maxBackupFiles);
                else Logger.SetMaxBackupLogFiles(Logger.DefaultMaxBackupLogFiles);

                // Set Local Time preference
                if (!bool.TryParse(config["UseLocalTimeForRotation"], out bool useLocalTimeForRotation))
                {
                    useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation;
                }
                Logger.SetUseLocalTimeForRotation(useLocalTimeForRotation);

                // 4. PROMOTE / SCOPE the logger after global config is set
                scopedLogger = rootLogger.CreateScoped(serviceName);

                // Set log level
                scopedLogger.SetLogLevel(logLevel);

                // Sync Event Log enablement to the instance
                var isEventLogEnabled = bool.TryParse(config["EnableEventLog"] ?? "true", out var elEnabled) && elEnabled;
                scopedLogger.SetIsEventLogEnabled(isEventLogEnabled);

                // 5. Initialize database and helpers
                dbContext = new AppDbContext(connectionString);

                var dapperExecutor = new DapperExecutor(dbContext);
                var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
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
                Logger.Info($"Attempting to restart service '{serviceName}' using Servy.Restarter.exe.");

                restarter.RestartService(serviceName, TimeSpan.FromSeconds(restartTimeout));

                Logger.Info($"Successfully restarted service '{serviceName}'.");
            }
            catch (Exception ex)
            {
                // Use the scoped logger if available, otherwise fallback to root
                (scopedLogger ?? rootLogger).Error($"Servy.Restarter.exe failed to restart the service: {ex.Message}", ex);
                Environment.ExitCode = 1;
            }
            finally
            {
                // Clean up
                secureData?.Dispose();
                scopedLogger?.Dispose();
                rootLogger.Dispose();
                dbContext?.Dispose();
                Logger.Shutdown();
            }

        }
    }
}
