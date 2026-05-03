using Servy.Core.Config;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Services;

namespace Servy.Core.Mappers
{
    /// <summary>
    /// Provides mapping methods between the domain <see cref="Service"/> model
    /// and its corresponding data transfer object <see cref="ServiceDto"/>.
    /// </summary>
    public static class ServiceMapper
    {
        /// <summary>
        /// Maps a <see cref="ServiceDto"/> from the database back to the domain <see cref="Service"/> model.
        /// </summary>
        /// <param name="serviceManager">
        /// The <see cref="IServiceManager"/> instance used to manage and interact with the service.
        /// </param>
        /// <param name="dto">The data transfer object to map.</param>
        /// <returns>A <see cref="Service"/> domain object representing the stored service.</returns>
        public static Service ToDomain(IServiceManager serviceManager, ServiceDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new Service(serviceManager)
            {
                Name = dto.Name,
                Description = dto.Description,
                ExecutablePath = dto.ExecutablePath,
                StartupDirectory = dto.StartupDirectory,
                Parameters = dto.Parameters,

                // Validate Enum ranges before mapping from DTO using shared parser
                StartupType = ConfigParser.ParseEnum(dto.StartupType, AppConfig.DefaultStartupType),
                Priority = ConfigParser.ParseEnum(dto.Priority, AppConfig.DefaultPriority),

                EnableConsoleUI = dto.EnableConsoleUI ?? AppConfig.DefaultEnableConsoleUI,

                StdoutPath = dto.StdoutPath,
                StderrPath = dto.StderrPath,
                EnableSizeRotation = dto.EnableSizeRotation ?? AppConfig.DefaultEnableRotation,
                RotationSize = dto.RotationSize ?? AppConfig.DefaultRotationSizeMB,
                EnableDateRotation = dto.EnableDateRotation ?? AppConfig.DefaultEnableDateRotation,

                // Validate Enum ranges
                DateRotationType = ConfigParser.ParseEnum(dto.DateRotationType, AppConfig.DefaultDateRotationType),

                MaxRotations = dto.MaxRotations ?? AppConfig.DefaultMaxRotations,
                UseLocalTimeForRotation = dto.UseLocalTimeForRotation ?? AppConfig.DefaultUseLocalTimeForRotation,
                EnableHealthMonitoring = dto.EnableHealthMonitoring ?? AppConfig.DefaultEnableHealthMonitoring,
                HeartbeatInterval = dto.HeartbeatInterval ?? AppConfig.DefaultHeartbeatInterval,
                MaxFailedChecks = dto.MaxFailedChecks ?? AppConfig.DefaultMaxFailedChecks,

                // Validate Enum ranges
                RecoveryAction = ConfigParser.ParseEnum(dto.RecoveryAction, AppConfig.DefaultRecoveryAction),

                MaxRestartAttempts = dto.MaxRestartAttempts ?? AppConfig.DefaultMaxRestartAttempts,
                FailureProgramPath = dto.FailureProgramPath,
                FailureProgramStartupDirectory = dto.FailureProgramStartupDirectory,
                FailureProgramParameters = dto.FailureProgramParameters,
                EnvironmentVariables = dto.EnvironmentVariables,
                ServiceDependencies = dto.ServiceDependencies,
                RunAsLocalSystem = dto.RunAsLocalSystem ?? AppConfig.DefaultRunAsLocalSystem,
                UserAccount = dto.UserAccount,
                Password = dto.Password,
                PreLaunchExecutablePath = dto.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = dto.PreLaunchStartupDirectory,
                PreLaunchParameters = dto.PreLaunchParameters,
                PreLaunchEnvironmentVariables = dto.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = dto.PreLaunchStdoutPath,
                PreLaunchStderrPath = dto.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? AppConfig.DefaultPreLaunchIgnoreFailure,

                PostLaunchExecutablePath = dto.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = dto.PostLaunchStartupDirectory,
                PostLaunchParameters = dto.PostLaunchParameters,

                EnableDebugLogs = dto.EnableDebugLogs ?? AppConfig.DefaultEnableDebugLogs,

                DisplayName = dto.DisplayName ?? string.Empty,

                StartTimeout = dto.StartTimeout ?? AppConfig.DefaultStartTimeout,
                StopTimeout = dto.StopTimeout ?? AppConfig.DefaultStopTimeout,

                Pid = dto.Pid,
                ActiveStdoutPath = dto.ActiveStdoutPath,
                ActiveStderrPath = dto.ActiveStderrPath,

                PreStopExecutablePath = dto.PreStopExecutablePath,
                PreStopStartupDirectory = dto.PreStopStartupDirectory,
                PreStopParameters = dto.PreStopParameters,
                PreStopTimeoutSeconds = dto.PreStopTimeoutSeconds ?? AppConfig.DefaultPreStopTimeoutSeconds,
                PreStopLogAsError = dto.PreStopLogAsError ?? AppConfig.DefaultPreStopLogAsError,

                PostStopExecutablePath = dto.PostStopExecutablePath,
                PostStopStartupDirectory = dto.PostStopStartupDirectory,
                PostStopParameters = dto.PostStopParameters,
            };
        }

    }
}