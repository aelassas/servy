using Microsoft.Extensions.Configuration;
using Servy.Core.Helpers;
using Servy.Core.Logging;

namespace Servy.Core.Config
{
    /// <summary>
    /// Centralizes the initialization and configuration of the Servy logging subsystem
    /// to prevent configuration drift across the CLI, Restarter, Service, and Manager entry points.
    /// </summary>
    public static class LoggerConfigurator
    {
        /// <summary>
        /// Configures the static Logger and an optional instance logger from an IConfiguration source.
        /// </summary>
        /// <param name="config">The application configuration source.</param>
        /// <param name="logFileName">Optional. If provided, initializes the static logger with this filename before applying settings.</param>
        /// <param name="instanceLogger">Optional. An instance logger (e.g., EventLogLogger) to sync settings like LogLevel and EnableEventLog.</param>
        public static void ConfigureFromAppSettings(IConfiguration config, string? logFileName = null, IServyLogger? instanceLogger = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var logLevel = ConfigParser.ParseEnum(config["LogLevel"], AppConfig.DefaultLogLevel, "LogLevel");
            instanceLogger?.SetLogLevel(logLevel);

            var dateRotationType = ConfigParser.ParseEnum(config["LogRollingInterval"], AppConfig.DefaultLogRollingInterval, "LogRollingInterval");

            var isEventLogEnabled = ConfigParser.ParseBool(config["EnableEventLog"], AppConfig.DefaultEnableEventLog, "EnableEventLog");
            instanceLogger?.SetIsEventLogEnabled(isEventLogEnabled);

            var enableSizeRotation = ConfigParser.ParseBool(config["EnableSizeRotation"], AppConfig.DefaultEnableInternalLogSizeRotation, "EnableSizeRotation");

            var logRotationSizeMB = ConfigParser.ParseInt(config["LogRotationSizeMB"], AppConfig.DefaultRotationSizeMB, "LogRotationSizeMB");

            var maxBackupLogFiles = ConfigParser.ParseInt(config["MaxBackupLogFiles"], AppConfig.LoggerDefaultMaxBackupLogFiles, "MaxBackupLogFiles");

            var useLocalTimeForRotation = ConfigParser.ParseBool(config["UseLocalTimeForRotation"], AppConfig.DefaultUseLocalTimeForRotation, "UseLocalTimeForRotation");

            // Apply logger settings
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                Logger.Initialize(
                    fileName: logFileName,
                    logLevel: logLevel,
                    enableSizeRotation: enableSizeRotation,
                    logRotationSizeMB: logRotationSizeMB,
                    dateRotationType: dateRotationType,
                    useLocalTimeForRotation: useLocalTimeForRotation,
                    maxBackupLogFiles: maxBackupLogFiles
                );
            }
            else
            {
                Logger.Initialize(
                    logLevel: logLevel,
                    enableSizeRotation: enableSizeRotation,
                    logRotationSizeMB: logRotationSizeMB,
                    dateRotationType: dateRotationType,
                    useLocalTimeForRotation: useLocalTimeForRotation,
                    maxBackupLogFiles: maxBackupLogFiles
                );
            }

            string eventLogStatus = instanceLogger != null ? isEventLogEnabled.ToString() : "n/a (no event-log sink in this process)";

            // Centralized debug logging prevents asymmetric log outputs
            Logger.Debug("Servy Logger Configuration Loaded:" + Environment.NewLine +
                $"  LogLevel: {logLevel}" + Environment.NewLine +
                $"  EnableSizeRotation: {enableSizeRotation}" + Environment.NewLine +
                $"  LogRotationSizeMB: {logRotationSizeMB}" + Environment.NewLine +
                $"  LogRollingInterval: {dateRotationType.ToString("D")} ({dateRotationType})" + Environment.NewLine +
                $"  MaxBackupLogFiles: {maxBackupLogFiles}" + Environment.NewLine +
                $"  UseLocalTimeForRotation: {useLocalTimeForRotation}" + Environment.NewLine +
                $"  EnableEventLog: {eventLogStatus}"
                );
        }
    }
}
