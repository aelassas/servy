using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Service.Bootstrap
{
    /// <summary>
    /// Production implementation of <see cref="IServiceBootstrapEnvironment"/>.
    /// </summary>
    /// <remarks>
    /// Every member is a forward to the call it replaced in the production constructor of
    /// <see cref="Servy.Service.Service"/>: same target, same arguments, same order, same thread and the
    /// same exception behaviour. The type holds no state, so an instance can be shared.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    internal sealed class ServiceBootstrapEnvironment : IServiceBootstrapEnvironment
    {
        /// <summary>
        /// Calls <see cref="Logger.Initialize(string, LogLevel, bool, int, Servy.Core.Enums.DateRotationType, bool, int, string)"/>
        /// with the given file name and log directory and the defaults for every other parameter.
        /// </summary>
        /// <param name="logFileName">The name of the log file the service writes its own diagnostics to.</param>
        /// <param name="logDirectory">The folder the log file is written in.</param>
        public void InitializeLogger(string logFileName, string logDirectory)
        {
            Logger.Initialize(logFileName, logDirectory: logDirectory);
        }

        /// <summary>
        /// Calls <see cref="Helper.EnsureEventSourceExists"/>.
        /// </summary>
        public void EnsureEventSourceExists()
        {
            Helper.EnsureEventSourceExists();
        }

        /// <summary>
        /// Builds the configuration from the optional <c>appsettings.service.json</c> in
        /// <see cref="AppFoldersHelper.GetAppDirectory"/>, without reload-on-change.
        /// </summary>
        /// <returns>The built configuration.</returns>
        public IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppFoldersHelper.GetAppDirectory())
                .AddJsonFile("appsettings.service.json", optional: true, reloadOnChange: false)
                .Build();
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
