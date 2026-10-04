using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Host.Bootstrap
{
    /// <summary>
    /// Production implementation of <see cref="IServiceBootstrapEnvironment"/>.
    /// </summary>
    /// <remarks>
    /// Every member is a forward to the call it replaced in the production constructor of
    /// <see cref="Service"/>: same target, same arguments, same order, same thread and the
    /// same exception behaviour. The type holds no state, so an instance can be shared.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    internal sealed class ServiceBootstrapEnvironment : IServiceBootstrapEnvironment
    {
        /// <summary>
        /// Calls <see cref="Logger.Initialize(string, LogLevel, bool, int, Servy.Core.Enums.DateRotationType, bool, int, string)"/>
        /// with the given file name and the defaults for every other parameter.
        /// </summary>
        /// <param name="logFileName">The name of the log file the service writes its own diagnostics to.</param>
        public void InitializeLogger(string logFileName)
        {
            Logger.Initialize(logFileName);
        }

        /// <summary>
        /// Calls <see cref="Helper.EnsureEventSourceExists"/>.
        /// </summary>
        public void EnsureEventSourceExists()
        {
            Helper.EnsureEventSourceExists();
        }

        /// <summary>
        /// Builds the configuration from the optional <c>appsettings.host.json</c> in
        /// <see cref="AppFoldersHelper.GetAppDirectory"/>, without reload-on-change.
        /// </summary>
        /// <returns>The built configuration.</returns>
        public IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppFoldersHelper.GetAppDirectory())
                .AddJsonFile(AppConfig.ServyHostSettingsFileName, optional: true, reloadOnChange: false)
                .Build();
        }

        /// <summary>
        /// Calls <see cref="CoreSettingsLoader.Load"/>.
        /// </summary>
        /// <returns>The resolved <see cref="CoreSettingsLoader.CoreSettings"/>.</returns>
        public CoreSettingsLoader.CoreSettings LoadCoreSettings()
        {
            return CoreSettingsLoader.Load();
        }

        /// <summary>
        /// Calls <see cref="LoggerConfigurator.ConfigureFromAppSettings"/> with the given configuration
        /// and instance logger.
        /// </summary>
        /// <param name="configuration">The configuration to read the logging settings from.</param>
        /// <param name="instanceLogger">The service's instance logger, or <see langword="null"/> when it has none.</param>
        public void ConfigureLogging(IConfiguration configuration, IServyLogger? instanceLogger)
        {
            LoggerConfigurator.ConfigureFromAppSettings(configuration, instanceLogger: instanceLogger);
        }

        /// <summary>
        /// Calls <see cref="Logger.Report"/> at <see cref="LogLevel.Debug"/>.
        /// </summary>
        /// <param name="title">The first line of the report.</param>
        /// <param name="body">The body of the report, already formatted with its own line breaks.</param>
        public void ReportDebug(string title, string body)
        {
            Logger.Report(LogLevel.Debug, title, body);
        }

        /// <summary>
        /// Calls <see cref="DatabaseValidator.IsSqliteVersionSafe"/>.
        /// </summary>
        /// <param name="detectedVersion">
        /// When this method returns, the version string that was detected, or <see langword="null"/> when
        /// it could not be determined.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the detected version is safe to use; otherwise <see langword="false"/>.
        /// </returns>
        public bool IsSqliteVersionSafe(out string? detectedVersion)
        {
            return DatabaseValidator.IsSqliteVersionSafe(out detectedVersion);
        }

        /// <summary>
        /// Creates the <see cref="AppDbContext"/>, initializes the schema through
        /// <see cref="DatabaseInitializer.InitializeDatabase"/> with <see cref="SQLiteDbInitializer.Initialize"/>,
        /// and builds the <see cref="ProtectedKeyProvider"/>, the <see cref="SecureData"/> helper and the
        /// <see cref="ServiceRepository"/> on top of it, in that order.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to open the database with.</param>
        /// <param name="aesKeyFilePath">The file path of the AES key used to protect stored secrets.</param>
        /// <param name="aesIVFilePath">The file path of the AES IV used to protect stored secrets.</param>
        /// <returns>The four objects the service keeps for its lifetime.</returns>
        public ServiceDataStack CreateDataStack(string connectionString, string aesKeyFilePath, string aesIVFilePath)
        {
            var dbContext = new AppDbContext(connectionString);
            DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

            var dapperExecutor = new DapperExecutor(dbContext);
            var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
            var secureData = new SecureData(protectedKeyProvider);
            var xmlSerializer = new XmlServiceSerializer();
            var jsonSerializer = new JsonServiceSerializer();

            var serviceRepository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

            return new ServiceDataStack(dbContext, protectedKeyProvider, secureData, serviceRepository);
        }

        /// <summary>
        /// Creates a <see cref="WindowsServiceApi"/>.
        /// </summary>
        /// <returns>The API.</returns>
        public IWindowsServiceApi CreateWindowsServiceApi()
        {
            return new WindowsServiceApi();
        }

        /// <summary>
        /// Creates a <see cref="PipeCallerIdentifier"/>.
        /// </summary>
        /// <returns>The identifier.</returns>
        public IPipeCallerIdentifier CreateCallerIdentifier()
        {
            return new PipeCallerIdentifier();
        }

        /// <summary>
        /// Calls <see cref="Logger.Error"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception to record with the message, or <see langword="null"/> when there is none.</param>
        public void LogError(string? message, Exception? exception = null)
        {
            Logger.Error(message, exception);
        }
    }
}
