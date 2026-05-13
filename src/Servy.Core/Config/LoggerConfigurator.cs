using Servy.Core.Enums;
using Servy.Core.Logging;
using System;
using System.Collections.Specialized;

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
        public static void ConfigureFromAppSettings(NameValueCollection config, string logFileName = null, IServyLogger instanceLogger = null)
        {
            var rawLogLevel = config["LogLevel"];
            if (!Enum.TryParse<LogLevel>(rawLogLevel, true, out var logLevel) || !Enum.IsDefined(typeof(LogLevel), logLevel))
            {
                if (!string.IsNullOrEmpty(rawLogLevel))
                    Logger.Warn($"Invalid configuration entry '{rawLogLevel}' for 'LogLevel'. Using default: {AppConfig.DefaultLogLevel}.");
                logLevel = AppConfig.DefaultLogLevel;
            }
            instanceLogger?.SetLogLevel(logLevel);

            if (!Enum.TryParse<DateRotationType>(config["LogRollingInterval"], true, out var dateRotationType) || !Enum.IsDefined(typeof(DateRotationType), dateRotationType))
            {
                dateRotationType = DateRotationType.None;
            }

            bool isEventLogEnabled;
            if (!bool.TryParse(config["EnableEventLog"], out isEventLogEnabled))
            {
                isEventLogEnabled = AppConfig.DefaultEnableEventLog;
            }
            instanceLogger?.SetIsEventLogEnabled(isEventLogEnabled);

            int logRotationSizeMB;
            if (!int.TryParse(config["LogRotationSizeMB"], out logRotationSizeMB) || logRotationSizeMB <= 0)
            {
                logRotationSizeMB = AppConfig.DefaultRotationSizeMB;
            }

            int maxBackupLogFiles;
            if (!int.TryParse(config["MaxBackupLogFiles"], out maxBackupLogFiles) || maxBackupLogFiles < 0)
            {
                maxBackupLogFiles = Logger.DefaultMaxBackupLogFiles;
            }

            string rawUseLocalTime = config["UseLocalTimeForRotation"] ?? AppConfig.DefaultUseLocalTimeForRotation.ToString();
            if (!bool.TryParse(rawUseLocalTime, out bool useLocalTimeForRotation))
            {
                useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation;
            }

            // Apply logger settings
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                Logger.Initialize(
                    fileName: logFileName,
                    logLevel: logLevel,
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
                    logRotationSizeMB: logRotationSizeMB,
                    dateRotationType: dateRotationType,
                    useLocalTimeForRotation: useLocalTimeForRotation,
                    maxBackupLogFiles: maxBackupLogFiles
               );
            }

            // Centralized debug logging prevents asymmetric log outputs
            Logger.Debug("Servy Logger Configuration Loaded:" + Environment.NewLine +
                $"  LogLevel: {logLevel}" + Environment.NewLine +
                $"  LogRollingInterval: {dateRotationType.ToString("D")} ({dateRotationType})" + Environment.NewLine +
                $"  EnableEventLog: {isEventLogEnabled}" + Environment.NewLine +
                $"  LogRotationSizeMB: {logRotationSizeMB}" + Environment.NewLine +
                $"  MaxBackupLogFiles: {maxBackupLogFiles}" + Environment.NewLine +
                $"  UseLocalTimeForRotation: {useLocalTimeForRotation}");
        }
    }
}