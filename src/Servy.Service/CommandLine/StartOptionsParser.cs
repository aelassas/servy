using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
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
        /// <returns>
        /// A <see cref="StartOptions"/> object populated with values parsed from the input arguments.
        /// Missing or invalid values will be set to default values.
        /// </returns>
        public static StartOptions Parse(IServiceRepository serviceRepository, string[] fullArgs)
        {
            if (fullArgs == null || fullArgs.Length == 0)
            {
                return new StartOptions();
            }

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
                ExecutablePath = ProcessHelper.ResolvePath(serviceDto.ExecutablePath ?? string.Empty),
                ExecutableArgs = Helper.EscapeBackslashes(serviceDto.Parameters ?? string.Empty),
                WorkingDirectory = ProcessHelper.ResolvePath(serviceDto.StartupDirectory ?? string.Empty),
                Priority = Enum.TryParse(((ProcessPriority)(serviceDto.Priority ?? (int)ProcessPriority.Normal)).ToString(), true, out ProcessPriorityClass p) ? p : ProcessPriorityClass.Normal,
                StdOutPath = serviceDto.StdoutPath,
                StdErrPath = serviceDto.StderrPath,
                RotationSizeInBytes = (serviceDto.RotationSize ?? 0) * 1024L * 1024L, // Convert from MB to Bytes
                UseLocalTimeForRotation = serviceDto.UseLocalTimeForRotation ?? AppConfig.DefaultUseLocalTimeForRotation,
                EnableHealthMonitoring = serviceDto.EnableHealthMonitoring ?? false,
                HeartbeatInterval = serviceDto.HeartbeatInterval ?? 0,
                MaxFailedChecks = serviceDto.MaxFailedChecks ?? 0,
                RecoveryAction = (serviceDto.EnableHealthMonitoring ?? false) ? (RecoveryAction)(serviceDto.RecoveryAction ?? (int)RecoveryAction.None) : RecoveryAction.None,
                ServiceName = serviceName,
                MaxRestartAttempts = serviceDto.MaxRestartAttempts ?? AppConfig.DefaultMaxRestartAttempts,
                EnvironmentVariables = EnvironmentVariableParser.Parse(serviceDto.EnvironmentVariables ?? string.Empty),

                // Pre-Launch args
                PreLaunchExecutablePath = ProcessHelper.ResolvePath(serviceDto.PreLaunchExecutablePath ?? string.Empty),
                PreLaunchWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PreLaunchStartupDirectory ?? string.Empty),
                PreLaunchExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreLaunchParameters ?? string.Empty),
                PreLaunchEnvironmentVariables = EnvironmentVariableParser.Parse(serviceDto.PreLaunchEnvironmentVariables ?? string.Empty),
                PreLaunchStdoutPath = serviceDto.PreLaunchStdoutPath,
                PreLaunchStderrPath = serviceDto.PreLaunchStderrPath,
                PreLaunchTimeout = serviceDto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = serviceDto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = serviceDto.PreLaunchIgnoreFailure ?? false,

                // Failure program
                FailureProgramPath = ProcessHelper.ResolvePath(serviceDto.FailureProgramPath ?? string.Empty),
                FailureProgramWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.FailureProgramStartupDirectory ?? string.Empty),
                FailureProgramArgs = Helper.EscapeBackslashes(serviceDto.FailureProgramParameters ?? string.Empty),

                // Post-Launch args
                PostLaunchExecutablePath = ProcessHelper.ResolvePath(serviceDto.PostLaunchExecutablePath ?? string.Empty),
                PostLaunchWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PostLaunchStartupDirectory ?? string.Empty),
                PostLaunchExecutableArgs = Helper.EscapeBackslashes(serviceDto.PostLaunchParameters ?? string.Empty),

                // Debug Logs
                EnableDebugLogs = serviceDto.EnableDebugLogs ?? false,

                // Max Rotations
                MaxRotations = serviceDto.MaxRotations ?? AppConfig.DefaultMaxRotations,

                // Date & Size Rotation
                EnableSizeRotation = serviceDto.EnableRotation ?? false,
                EnableDateRotation = serviceDto.EnableDateRotation ?? false,
                DateRotationType = (DateRotationType)(serviceDto.DateRotationType ?? (int)DateRotationType.Daily),

                // Start and Stop timeouts
                StartTimeout = serviceDto.StartTimeout ?? AppConfig.DefaultStartTimeout,
                StopTimeout = serviceDto.StopTimeout ?? AppConfig.DefaultStopTimeout,

                // Pre-Stop
                PreStopExecutablePath = ProcessHelper.ResolvePath(serviceDto.PreStopExecutablePath ?? string.Empty),
                PreStopWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PreStopStartupDirectory ?? string.Empty),
                PreStopExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreStopParameters ?? string.Empty),
                PreStopTimeout = serviceDto.PreStopTimeoutSeconds ?? AppConfig.DefaultPreStopTimeoutSeconds,
                PreStopLogAsError = serviceDto.PreStopLogAsError ?? false,

                // Post-Stop
                PostStopExecutablePath = ProcessHelper.ResolvePath(serviceDto.PostStopExecutablePath ?? string.Empty),
                PostStopWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PostStopStartupDirectory ?? string.Empty),
                PostStopExecutableArgs = Helper.EscapeBackslashes(serviceDto.PostStopParameters ?? string.Empty),

            };
        }
    }
}
