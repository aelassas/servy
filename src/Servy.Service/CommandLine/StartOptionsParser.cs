using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Diagnostics;
using System.Security;

namespace Servy.Service.CommandLine
{
    /// <summary>
    /// Provides functionality to parse command-line arguments into a <see cref="StartOptions"/> object.
    /// </summary>
    public static class StartOptionsParser
    {
        /// <summary>
        /// Parses the specified array of command-line arguments into a <see cref="StartOptions"/> instance.
        /// </summary>
        /// <param name="serviceRepository">An instance of <see cref="IServiceRepository"/> used to retrieve service configuration from the database.</param>
        /// <param name="fullArgs">An array of strings representing the command-line arguments.</param>
        /// <param name="processHelper">The process helper used to format process commands.</param>
        /// <returns>
        /// A <see cref="StartOptions"/> object populated with values parsed from the input arguments.
        /// Missing or invalid values will be set to default values.
        /// </returns>
        public static StartOptions Parse(IServiceRepository serviceRepository, IProcessHelper processHelper, string[] fullArgs)
        {
            if (fullArgs == null || fullArgs.Length == 0)
            {
                throw new ArgumentException("No arguments provided");
            }

            // The service name is expected as the second argument (e.g., Servy.Service.exe "MyService")
            var serviceName = fullArgs.Length > 1 ? fullArgs[1] : string.Empty;

            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("Service name is empty!");
            }

            var serviceDto = serviceRepository.GetByName(serviceName);

            if (serviceDto == null)
            {
                throw new InvalidOperationException($"Service {serviceName} not found in the database!");
            }

            return new StartOptions
            {
                // Main
                ServiceName = serviceName,
                ExecutablePath = SafeResolvePath(processHelper, serviceDto.ExecutablePath, nameof(serviceDto.ExecutablePath), serviceName),
                ExecutableArgs = Helper.EscapeBackslashes(serviceDto.Parameters ?? string.Empty),
                WorkingDirectory = SafeResolvePath(processHelper, serviceDto.StartupDirectory, nameof(serviceDto.StartupDirectory), serviceName),

                // Use ConfigParser.ParseEnum to ensure the priority is a valid member of ProcessPriority.
                // This prevents undefined enum values from entering the process mapping logic.
                Priority = MapPriority(ConfigParser.ParseEnum(serviceDto.Priority, AppConfig.DefaultProcessPriority)),

                EnableConsoleUI = serviceDto.EnableConsoleUI ?? AppConfig.DefaultEnableConsoleUI,

                // Logging
                StdoutPath = serviceDto.StdoutPath,
                StderrPath = serviceDto.StderrPath,
                RotationSizeInBytes = AppConfig.ToBytes(serviceDto.RotationSize ?? AppConfig.DefaultRotationSizeMB), // Convert from MB to Bytes
                UseLocalTimeForRotation = serviceDto.UseLocalTimeForRotation ?? AppConfig.DefaultUseLocalTimeForRotation,

                // Health Monitoring
                EnableHealthMonitoring = serviceDto.EnableHealthMonitoring ?? AppConfig.DefaultEnableHealthMonitoring,
                HeartbeatInterval = serviceDto.HeartbeatInterval ?? AppConfig.DefaultHeartbeatInterval,
                MaxFailedChecks = serviceDto.MaxFailedChecks ?? AppConfig.DefaultMaxFailedChecks,

                // Validate RecoveryAction to ensure pattern matching in health handlers behaves predictably.
                RecoveryAction = (serviceDto.EnableHealthMonitoring ?? AppConfig.DefaultEnableHealthMonitoring)
                    ? ConfigParser.ParseEnum(serviceDto.RecoveryAction, AppConfig.DefaultRecoveryAction)
                    : RecoveryAction.None,
                RecoveryOnCleanExit = serviceDto.RecoveryOnCleanExit ?? AppConfig.DefaultRecoveryOnCleanExit,

                MaxRestartAttempts = serviceDto.MaxRestartAttempts ?? AppConfig.DefaultMaxRestartAttempts,
                EnvironmentVariables = SafeParseEnvVars(serviceDto.EnvironmentVariables, nameof(serviceDto.EnvironmentVariables), serviceName),

                // Pre-Launch settings
                PreLaunchExecutablePath = SafeResolvePath(processHelper, serviceDto.PreLaunchExecutablePath, nameof(serviceDto.PreLaunchExecutablePath), serviceName),
                PreLaunchWorkingDirectory = SafeResolvePath(processHelper, serviceDto.PreLaunchStartupDirectory, nameof(serviceDto.PreLaunchStartupDirectory), serviceName),
                PreLaunchExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreLaunchParameters ?? string.Empty),
                PreLaunchEnvironmentVariables = SafeParseEnvVars(serviceDto.PreLaunchEnvironmentVariables, nameof(serviceDto.PreLaunchEnvironmentVariables), serviceName),
                PreLaunchStdoutPath = serviceDto.PreLaunchStdoutPath,
                PreLaunchStderrPath = serviceDto.PreLaunchStderrPath,
                PreLaunchTimeoutInSeconds = serviceDto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = serviceDto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = serviceDto.PreLaunchIgnoreFailure ?? AppConfig.DefaultPreLaunchIgnoreFailure,

                // Failure program settings
                FailureProgramPath = SafeResolvePath(processHelper, serviceDto.FailureProgramPath, nameof(serviceDto.FailureProgramPath), serviceName),
                FailureProgramWorkingDirectory = SafeResolvePath(processHelper, serviceDto.FailureProgramStartupDirectory, nameof(serviceDto.FailureProgramStartupDirectory), serviceName),
                FailureProgramArgs = Helper.EscapeBackslashes(serviceDto.FailureProgramParameters ?? string.Empty),

                // Post-Launch settings
                PostLaunchExecutablePath = SafeResolvePath(processHelper, serviceDto.PostLaunchExecutablePath, nameof(serviceDto.PostLaunchExecutablePath), serviceName),
                PostLaunchWorkingDirectory = SafeResolvePath(processHelper, serviceDto.PostLaunchStartupDirectory, nameof(serviceDto.PostLaunchStartupDirectory), serviceName),
                PostLaunchExecutableArgs = Helper.EscapeBackslashes(serviceDto.PostLaunchParameters ?? string.Empty),

                // Operational toggles
                EnableDebugLogs = serviceDto.EnableDebugLogs ?? AppConfig.DefaultEnableDebugLogs,
                MaxRotations = serviceDto.MaxRotations ?? AppConfig.DefaultMaxRotations,
                EnableSizeRotation = serviceDto.EnableSizeRotation ?? AppConfig.DefaultEnableSizeRotation,
                EnableDateRotation = serviceDto.EnableDateRotation ?? AppConfig.DefaultEnableDateRotation,

                // Validate DateRotationType to prevent disabling rotation logic due to out-of-range integer values.
                DateRotationType = ConfigParser.ParseEnum(serviceDto.DateRotationType, AppConfig.DefaultDateRotationType),

                // Lifespan timeouts
                StartTimeoutInSeconds = serviceDto.StartTimeout ?? AppConfig.DefaultStartTimeout,
                StopTimeoutInSeconds = serviceDto.StopTimeout ?? AppConfig.DefaultStopTimeout,

                // Pre-Stop settings
                PreStopExecutablePath = SafeResolvePath(processHelper, serviceDto.PreStopExecutablePath, nameof(serviceDto.PreStopExecutablePath), serviceName),
                PreStopWorkingDirectory = SafeResolvePath(processHelper, serviceDto.PreStopStartupDirectory, nameof(serviceDto.PreStopStartupDirectory), serviceName),
                PreStopExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreStopParameters ?? string.Empty),
                PreStopTimeoutInSeconds = serviceDto.PreStopTimeoutSeconds ?? AppConfig.DefaultPreStopTimeoutSeconds,
                PreStopLogAsError = serviceDto.PreStopLogAsError ?? AppConfig.DefaultPreStopLogAsError,

                // Post-Stop settings
                PostStopExecutablePath = SafeResolvePath(processHelper, serviceDto.PostStopExecutablePath, nameof(serviceDto.PostStopExecutablePath), serviceName),
                PostStopWorkingDirectory = SafeResolvePath(processHelper, serviceDto.PostStopStartupDirectory, nameof(serviceDto.PostStopStartupDirectory), serviceName),
                PostStopExecutableArgs = Helper.EscapeBackslashes(serviceDto.PostStopParameters ?? string.Empty),
            };
        }

        /// <summary>
        /// Maps the custom <see cref="ProcessPriority"/> domain enum to the 
        /// standard <see cref="ProcessPriorityClass"/> used by the system.
        /// </summary>
        /// <param name="p">The process priority level defined in the service configuration.</param>
        /// <returns>
        /// The corresponding <see cref="ProcessPriorityClass"/>. 
        /// Defaults to <see cref="ProcessPriorityClass.Normal"/> if the input is unrecognized.
        /// </returns>
        /// <remarks>
        /// This manual mapping is used instead of <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> 
        /// to eliminate string allocations and reflection overhead during service startup.
        /// </remarks>
        public static ProcessPriorityClass MapPriority(ProcessPriority p)
        {
            switch (p)
            {
                case ProcessPriority.Idle: return ProcessPriorityClass.Idle;
                case ProcessPriority.BelowNormal: return ProcessPriorityClass.BelowNormal;
                case ProcessPriority.Normal: return ProcessPriorityClass.Normal;
                case ProcessPriority.AboveNormal: return ProcessPriorityClass.AboveNormal;
                case ProcessPriority.High: return ProcessPriorityClass.High;
                case ProcessPriority.RealTime: return ProcessPriorityClass.RealTime;
                default:
                    Logger.Warn($"Unknown ProcessPriority value '{p}' - defaulting to Normal.");
                    return ProcessPriorityClass.Normal;
            }
        }

        /// <summary>
        /// Intercepts formatting violations inside stored configuration strings, ensuring malformed records do not torpedo service launches.
        /// </summary>
        private static List<EnvironmentVariable> SafeParseEnvVars(string? raw, string fieldName, string serviceName)
        {
            try
            {
                return EnvironmentVariableParser.Parse(raw ?? string.Empty);
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is ArgumentOutOfRangeException)
            {
                Logger.Error(
                    $"Service '{serviceName}': stored {fieldName} value is malformed and could not be parsed " +
                    $"({ex.Message}). Continuing startup with an empty environment for this scope.");
                return new List<EnvironmentVariable>();
            }
        }

        /// <summary>
        /// Safely intercepts exceptional expansion and path-mapping scenarios. On failure it falls back to the
        /// original unresolved path string (rather than aborting) to preserve SCM initialization contracts.
        /// </summary>
        private static string SafeResolvePath(IProcessHelper processHelper, string? rawPath, string fieldName, string serviceName)
        {
            try
            {
                return processHelper.ResolvePath(rawPath ?? string.Empty) ?? string.Empty;
            }
            catch (Exception ex) when (ex is InvalidOperationException
            || ex is ArgumentException
            || ex is NotSupportedException
            || ex is PathTooLongException
            || ex is SecurityException)
            {
                Logger.Error(
                    $"Service '{serviceName}': stored {fieldName} location resolution failed or is invalid " +
                    $"({ex.Message}). Proceeding with an unmapped string token path to ensure startup execution.");
                return rawPath ?? string.Empty;
            }
        }
    }
}