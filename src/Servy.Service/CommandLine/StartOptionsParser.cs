using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
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
        /// <returns>
        /// A <see cref="StartOptions"/> object populated with values parsed from the input arguments.
        /// Missing or invalid values will be set to default values.
        /// </returns>
        public static StartOptions Parse(IServiceRepository serviceRepository, string[] fullArgs)
        {
            if (fullArgs == null || fullArgs.Length == 0)
                return new StartOptions();

            var serviceName = fullArgs.Length > 11 ? fullArgs[11] : string.Empty;

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
                // Process path is no longer passed from binary path and is retrived from DB instead
                ExecutablePath = ProcessHelper.ResolvePath(serviceDto.ExecutablePath ?? string.Empty),
                // Process parameters are no longer passed from binary path and are retrived from DB instead
                ExecutableArgs = Helper.EscapeBackslashes(serviceDto.Parameters ?? string.Empty),
                // Working directory is no longer passed from binary path and is retrived from DB instead
                WorkingDirectory = ProcessHelper.ResolvePath(serviceDto.StartupDirectory ?? string.Empty),
                Priority = fullArgs.Length > 4 && Enum.TryParse(fullArgs[4], true, out ProcessPriorityClass p) ? p : ProcessPriorityClass.Normal,
                StdOutPath = fullArgs.Length > 5 ? fullArgs[5] : string.Empty,
                StdErrPath = fullArgs.Length > 6 ? fullArgs[6] : string.Empty,
                RotationSizeInBytes = fullArgs.Length > 7 && int.TryParse(fullArgs[7], out int rsb) ? rsb : 0,
                HeartbeatInterval = fullArgs.Length > 8 && int.TryParse(fullArgs[8], out int hbi) ? hbi : 0,
                MaxFailedChecks = fullArgs.Length > 9 && int.TryParse(fullArgs[9], out int mfc) ? mfc : 0,
                RecoveryAction = fullArgs.Length > 10 && Enum.TryParse(fullArgs[10], true, out RecoveryAction ra) ? ra : RecoveryAction.None,
                ServiceName = serviceName,
                MaxRestartAttempts = fullArgs.Length > 12 && int.TryParse(fullArgs[12], out int mra) ? mra : 3,
                // Environment variables are no longer passed from binary path and are retrived from DB instead
                EnvironmentVariables = EnvironmentVariableParser.Parse(serviceDto.EnvironmentVariables ?? string.Empty),

                // Pre-Launch args
                // Process path is no longer passed from binary path and is retrived from DB instead
                PreLaunchExecutablePath = ProcessHelper.ResolvePath(serviceDto.PreLaunchExecutablePath ?? string.Empty),
                // Working directory is no longer passed from binary path and is retrived from DB instead
                PreLaunchWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PreLaunchStartupDirectory ?? string.Empty),
                // Process parameters are no longer passed from binary path and are retrived from DB instead
                PreLaunchExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreLaunchParameters ?? string.Empty),
                // Environment variables are no longer passed from binary path and are retrived from DB instead
                PreLaunchEnvironmentVariables = EnvironmentVariableParser.Parse(serviceDto.PreLaunchEnvironmentVariables ?? string.Empty),
                PreLaunchStdOutPath = fullArgs.Length > 18 ? fullArgs[18] : string.Empty,
                PreLaunchStdErrPath = fullArgs.Length > 19 ? fullArgs[19] : string.Empty,
                PreLaunchTimeout = fullArgs.Length > 20 && int.TryParse(fullArgs[20], out int preLaunchTimeout) ? preLaunchTimeout : 30,
                PreLaunchRetryAttempts = fullArgs.Length > 21 && int.TryParse(fullArgs[21], out int preLaunchRetryAttempts) ? preLaunchRetryAttempts : 0,
                PreLaunchIgnoreFailure = fullArgs.Length > 22 && bool.TryParse(fullArgs[22], out bool preLaunchIgnoreFailure) && preLaunchIgnoreFailure,

                // Failure program
                // Process path is no longer passed from binary path and is retrived from DB instead
                FailureProgramPath = ProcessHelper.ResolvePath(serviceDto.FailureProgramPath ?? string.Empty),
                // Working directory is no longer passed from binary path and is retrived from DB instead
                FailureProgramWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.FailureProgramStartupDirectory ?? string.Empty),
                // Process parameters are no longer passed from binary path and are retrived from DB instead
                FailureProgramArgs = Helper.EscapeBackslashes(serviceDto.FailureProgramParameters ?? string.Empty),

                // Post-Launch args
                // Process path is no longer passed from binary path and is retrived from DB instead
                PostLaunchExecutablePath = ProcessHelper.ResolvePath(serviceDto.PostLaunchExecutablePath ?? string.Empty),
                // Working directory is no longer passed from binary path and is retrived from DB instead
                PostLaunchWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PostLaunchStartupDirectory ?? string.Empty),
                // Process parameters are no longer passed from binary path and are retrived from DB instead
                PostLaunchExecutableArgs = Helper.EscapeBackslashes(serviceDto.PostLaunchParameters ?? string.Empty),

                // Debug Logs
                EnableDebugLogs = fullArgs.Length > 29 && bool.TryParse(fullArgs[29], out bool enableDebugLogs) && enableDebugLogs,

                // Max Rotations
                MaxRotations = fullArgs.Length > 30 && int.TryParse(fullArgs[30], out int maxRotations) ? maxRotations : AppConfig.DefaultMaxRotations,

                // Date & Size Rotation
                EnableSizeRotation = fullArgs.Length > 31 && bool.TryParse(fullArgs[31], out bool enableSizeRotation) && enableSizeRotation,
                EnableDateRotation = fullArgs.Length > 32 && bool.TryParse(fullArgs[32], out bool enableDateRotation) && enableDateRotation,
                DateRotationType = fullArgs.Length > 33 && Enum.TryParse(fullArgs[33], true, out DateRotationType dateRotationType) ? dateRotationType : DateRotationType.Daily,

                // Start and Stop timeouts
                StartTimeout = fullArgs.Length > 34 && int.TryParse(fullArgs[34], out int startTimeout) ? startTimeout : AppConfig.DefaultStartTimeout,
                StopTimeout = fullArgs.Length > 35 && int.TryParse(fullArgs[35], out int stopTimeout) ? stopTimeout : AppConfig.DefaultStopTimeout,

                // Pre-Stop
                PreStopExecutablePath = ProcessHelper.ResolvePath(serviceDto.PreStopExecutablePath ?? string.Empty),
                PreStopWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PreStopStartupDirectory ?? string.Empty),
                PreStopExecutableArgs = Helper.EscapeBackslashes(serviceDto.PreStopParameters ?? string.Empty),
                PreStopTimeout = fullArgs.Length > 36 && int.TryParse(fullArgs[36], out int preStopTimeout) ? preStopTimeout : AppConfig.DefaultPreStopTimeoutSeconds,
                PreStopLogAsError = fullArgs.Length > 37 && bool.TryParse(fullArgs[37], out bool preStopLogAsError) && preStopLogAsError,

                // Post-Stop
                PostStopExecutablePath = ProcessHelper.ResolvePath(serviceDto.PostStopExecutablePath ?? string.Empty),
                PostStopWorkingDirectory = ProcessHelper.ResolvePath(serviceDto.PostStopStartupDirectory ?? string.Empty),
                PostStopExecutableArgs = Helper.EscapeBackslashes(serviceDto.PostStopParameters ?? string.Empty),

            };
        }
    }
}
