using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Services;

namespace Servy.Host.Bootstrap
{
    /// <summary>
    /// Seam over the machine-touching and process-global calls that the production constructor of
    /// <see cref="Service"/> would otherwise make inline: the global logger, the Windows event source, the
    /// <c>appsettings.host.json</c> configuration, the core data-layer settings, the SQLite version check, the
    /// creation of the database and repository stack, and the
    /// Service Control Manager and pipe-client identification used to authorize callers.
    /// </summary>
    /// <remarks>
    /// The production implementation is <see cref="ServiceBootstrapEnvironment"/>. Every member forwards
    /// unchanged to the call it replaced, with the same arguments, in the same order and on the calling
    /// thread, and the public constructors wire that implementation, so a service constructed the way
    /// the Service Control Manager constructs it behaves exactly as it did when the calls were inline.
    /// <para>
    /// The pure logic of the constructor deliberately stays in <see cref="Service"/>: the
    /// <c>EnableEventLog</c> to <c>AutoLog</c> mapping, <c>CanShutdown</c> and the exit-code rules of the
    /// surrounding <c>catch</c>. Those are what a test asserts once this seam lets it construct the service at all.
    /// </para>
    /// </remarks>
    internal interface IServiceBootstrapEnvironment
    {
        /// <summary>
        /// Initializes the process-global <see cref="Logger"/> with the given log file name.
        /// </summary>
        /// <param name="logFileName">The name of the log file the service writes its own diagnostics to.</param>
        void InitializeLogger(string logFileName);

        /// <summary>
        /// Ensures the Windows event source the service logs under exists, creating it when it does not.
        /// </summary>
        /// <exception cref="System.Exception">
        /// Any exception the underlying event-log registration raises is allowed to propagate, exactly as
        /// the inline call did, so the constructor's <c>catch</c> handles it.
        /// </exception>
        void EnsureEventSourceExists();

        /// <summary>
        /// Builds the service configuration from <c>appsettings.host.json</c> in the application directory.
        /// </summary>
        /// <returns>The built configuration. The file is optional, so an absent file yields an empty configuration.</returns>
        IConfiguration BuildConfiguration();

        /// <summary>
        /// Loads the core data-layer settings: the connection string and the AES key and IV file paths.
        /// </summary>
        /// <returns>The resolved <see cref="CoreSettingsLoader.CoreSettings"/>.</returns>
        CoreSettingsLoader.CoreSettings LoadCoreSettings();

        /// <summary>
        /// Applies the logging configuration found in <paramref name="configuration"/> to the global logger
        /// and to the service's own instance logger.
        /// </summary>
        /// <param name="configuration">The configuration to read the logging settings from.</param>
        /// <param name="instanceLogger">The service's instance logger, or <see langword="null"/> when it has none.</param>
        void ConfigureLogging(IConfiguration configuration, IServyLogger? instanceLogger);

        /// <summary>
        /// Writes a multi-line report at <see cref="LogLevel.Debug"/> to the global logger.
        /// </summary>
        /// <param name="title">The first line of the report.</param>
        /// <param name="body">The body of the report, already formatted with its own line breaks.</param>
        void ReportDebug(string title, string body);

        /// <summary>
        /// Determines whether the SQLite version the process would open its database with is at or above
        /// the minimum required by the CVE-2025-6965 mitigation.
        /// </summary>
        /// <param name="detectedVersion">
        /// When this method returns, the version string that was detected, or <see langword="null"/> when
        /// it could not be determined.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the detected version is safe to use; otherwise <see langword="false"/>.
        /// </returns>
        bool IsSqliteVersionSafe(out string? detectedVersion);

        /// <summary>
        /// Creates the database context, initializes the database schema, and builds the protected key
        /// provider, the secure-data helper and the service repository on top of it.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to open the database with.</param>
        /// <param name="aesKeyFilePath">The file path of the AES key used to protect stored secrets.</param>
        /// <param name="aesIVFilePath">The file path of the AES IV used to protect stored secrets.</param>
        /// <returns>The four objects the service keeps for its lifetime.</returns>
        /// <exception cref="System.Exception">
        /// Any exception the database, key or repository creation raises is allowed to propagate, exactly
        /// as the inline block did, so the constructor's <c>catch</c> handles it.
        /// </exception>
        ServiceDataStack CreateDataStack(string connectionString, string aesKeyFilePath, string aesIVFilePath);

        /// <summary>
        /// Creates the Service Control Manager API the service uses to tell which process a service runs in.
        /// </summary>
        /// <returns>The API.</returns>
        IWindowsServiceApi CreateWindowsServiceApi();

        /// <summary>
        /// Creates the identifier of the process at the client end of a connection.
        /// </summary>
        /// <returns>The identifier.</returns>
        IPipeCallerIdentifier CreateCallerIdentifier();

        /// <summary>
        /// Writes an error to the global logger, optionally with the exception that caused it.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception to record with the message, or <see langword="null"/> when there is none.</param>
        void LogError(string? message, Exception? exception = null);
    }
}
