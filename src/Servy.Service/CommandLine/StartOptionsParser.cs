using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System;
using System.Diagnostics;

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
                ExecutablePath = processHelper.ResolvePath(serviceDto.ExecutablePath ?? string.Empty),
                ExecutableArgs = Helper.EscapeBackslashes(serviceDto.Parameters ?? string.Empty),
                WorkingDirectory = processHelper.ResolvePath(serviceDto.StartupDirectory ?? string.Empty),

                // Use ConfigParser.ParseEnum to ensure the priority is a valid member of ProcessPriority.
                // This prevents undefined enum values from entering the process mapping logic.
                Priority = MapPriority(ConfigParser.ParseEnum(serviceDto.Priority, AppConfig.DefaultProcessPriority)),

                EnableConsoleUI = serviceDto.EnableConsoleUI ?? AppConfig.DefaultEnableConsoleUI,

                // Logging
                StdOutPath = serviceDto.StdoutPath,
                StdErrPath = serviceDto.StderrPath,
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
                EnvironmentVariables = EnvironmentVariableParser.Parse(serviceDto.EnvironmentVariables ?? string.Empty),

                // Pre-Launch settings
                PreLaunchExecutablePath = processHelper.ResolvePath(serviceDto.PreLaunchExecutablePath ?? string.Empty),
                PreLaunchWorkingDirectory = processHelper.ResolvePath(serviceDto.PreLaunchStartupDirectory ?? string.Empty),
                PreLaunchExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreLaunchParameters ?? string.Empty),
                PreLaunchEnvironmentVariables = EnvironmentVariableParser.Parse(serviceDto.PreLaunchEnvironmentVariables ?? string.Empty),
                PreLaunchStdoutPath = serviceDto.PreLaunchStdoutPath,
                PreLaunchStderrPath = serviceDto.PreLaunchStderrPath,
                PreLaunchTimeoutInSeconds = serviceDto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = serviceDto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = serviceDto.PreLaunchIgnoreFailure ?? AppConfig.DefaultPreLaunchIgnoreFailure,

                // Failure program settings
                FailureProgramPath = processHelper.ResolvePath(serviceDto.FailureProgramPath ?? string.Empty),
                FailureProgramWorkingDirectory = processHelper.ResolvePath(serviceDto.FailureProgramStartupDirectory ?? string.Empty),
                FailureProgramArgs = Helper.EscapeBackslashes(serviceDto.FailureProgramParameters ?? string.Empty),

                // Post-Launch settings
                PostLaunchExecutablePath = processHelper.ResolvePath(serviceDto.PostLaunchExecutablePath ?? string.Empty),
                PostLaunchWorkingDirectory = processHelper.ResolvePath(serviceDto.PostLaunchStartupDirectory ?? string.Empty),
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
                PreStopExecutablePath = processHelper.ResolvePath(serviceDto.PreStopExecutablePath ?? string.Empty),
                PreStopWorkingDirectory = processHelper.ResolvePath(serviceDto.PreStopStartupDirectory ?? string.Empty),
                PreStopExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreStopParameters ?? string.Empty),
                PreStopTimeoutInSeconds = serviceDto.PreStopTimeoutSeconds ?? AppConfig.DefaultPreStopTimeoutSeconds,
                PreStopLogAsError = serviceDto.PreStopLogAsError ?? AppConfig.DefaultPreStopLogAsError,

                // Post-Stop settings
                PostStopExecutablePath = processHelper.ResolvePath(serviceDto.PostStopExecutablePath ?? string.Empty),
                PostStopWorkingDirectory = processHelper.ResolvePath(serviceDto.PostStopStartupDirectory ?? string.Empty),
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
                    Logger.Warn($"Unknown ProcessPriority value '{p}' — defaulting to Normal.");
                    return ProcessPriorityClass.Normal;

            }
        }

    }
}
