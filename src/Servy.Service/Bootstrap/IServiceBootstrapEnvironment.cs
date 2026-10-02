using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Specialized;

namespace Servy.Service.Bootstrap
{
    /// <summary>
    /// Seam over the machine-touching and process-global calls that the production constructor of
    /// <see cref="Servy.Service.Service"/> would otherwise make inline: the global logger, the Windows
    /// event source, the application settings, the core data-layer settings,
    /// the SQLite version check and the creation of the database and repository stack.
    /// </summary>
    /// <remarks>
    /// The production implementation is <see cref="ServiceBootstrapEnvironment"/>. Every member forwards
    /// unchanged to the call it replaced, with the same arguments, in the same order and on the calling
    /// thread, and the public constructors wire that implementation, so a service constructed the way
    /// the Service Control Manager constructs it behaves exactly as it did when the calls were inline.
    /// <para>
    /// The pure logic of the constructor deliberately stays in <see cref="Servy.Service.Service"/>: the
    /// <c>Timing:*</c> parsing, the <c>EnableEventLog</c> to <c>AutoLog</c> mapping, <c>CanShutdown</c>
    /// and the exit-code rules of the surrounding <c>catch</c>. Those are what a test asserts once this
    /// seam lets it construct the service at all.
    /// </para>
    /// </remarks>
    internal interface IServiceBootstrapEnvironment
    {
        /// <summary>
        /// Initializes the process-global <see cref="Logger"/> with the given log file name, in the given folder.
        /// </summary>
        /// <param name="logFileName">The name of the log file the service writes its own diagnostics to.</param>
        /// <param name="logDirectory">The folder the log file is written in: the service's own log folder,
        /// <c>logs\service\&lt;ServiceName&gt;\</c> (<see cref="Servy.Core.Logging.ServiceLogPaths.GetFolderPath"/>), the only log
        /// folder its service account can write.</param>
        void InitializeLogger(string logFileName, string logDirectory);

        /// <summary>
        /// Ensures the Windows event source the service logs under exists, creating it when it does not.
        /// </summary>
        /// <exception cref="System.Exception">
        /// Any exception the underlying event-log registration raises is allowed to propagate, exactly as
        /// the inline call did, so the constructor's <c>catch</c> handles it.
        /// </exception>
        void EnsureEventSourceExists();

        /// <summary>
        /// Returns the <c>appSettings</c> section of the application configuration file.
        /// </summary>
        /// <returns>The application settings. An absent section yields an empty collection.</returns>
        NameValueCollection BuildConfiguration();

        /// <summary>
        /// Loads the core data-layer settings: the connection string and the AES key and IV file paths.
        /// </summary>
        /// <returns>The resolved <see cref="CoreSettings"/>.</returns>
        CoreSettings LoadCoreSettings();

        /// <summary>
        /// Applies the logging configuration found in <paramref name="configuration"/> to the global logger
        /// and to the service's own instance logger.
        /// </summary>
        /// <param name="configuration">The configuration to read the logging settings from.</param>
        /// <param name="instanceLogger">The service's instance logger, or <see langword="null"/> when it has none.</param>
        void ConfigureLogging(NameValueCollection configuration, IServyLogger instanceLogger);

        /// <summary>
        /// Writes a multi-line report at <see cref="LogLevel.Debug"/> to the global logger.
        /// </summary>
        /// <param name="title">The first line of the report.</param>
        /// <param name="body">The body of the report, already formatted with its own line breaks.</param>
        void ReportDebug(string title, string body);

        /// <summary>
        /// Writes an error to the global logger, optionally with the exception that caused it.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception to record with the message, or <see langword="null"/> when there is none.</param>
        void LogError(string message, Exception exception = null);
    }
}
