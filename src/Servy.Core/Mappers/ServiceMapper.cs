using Servy.Core.Config;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
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
        /// Maps a domain <see cref="Service"/> object to a <see cref="ServiceDto"/> for persistence.
        /// </summary>
        /// <param name="domain">The domain service object to map.</param>
        /// <param name="id">Service ID.</param>
        /// <returns>A <see cref="ServiceDto"/> representing the service for storage.</returns>
        public static ServiceDto ToDto(Service domain, int? id = null)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            return new ServiceDto
            {
                Id = id ?? 0,
                Name = domain.Name,
                Description = domain.Description,
                ExecutablePath = domain.ExecutablePath,
                StartupDirectory = domain.StartupDirectory,
                Parameters = domain.Parameters,
                StartupType = (int)domain.StartupType,
                Priority = (int)domain.Priority,
                EnableConsoleUI = domain.EnableConsoleUI,
                StdoutPath = domain.StdoutPath,
                StderrPath = domain.StderrPath,
                EnableSizeRotation = domain.EnableSizeRotation,
                RotationSize = domain.RotationSize,
                EnableDateRotation = domain.EnableDateRotation,
                DateRotationType = (int)domain.DateRotationType,
                MaxRotations = domain.MaxRotations,
                EnableHealthMonitoring = domain.EnableHealthMonitoring,
                UseLocalTimeForRotation = domain.UseLocalTimeForRotation,
                HeartbeatInterval = domain.HeartbeatInterval,
                MaxFailedChecks = domain.MaxFailedChecks,
                RecoveryAction = (int)domain.RecoveryAction,
                MaxRestartAttempts = domain.MaxRestartAttempts,
                FailureProgramPath = domain.FailureProgramPath,
                FailureProgramStartupDirectory = domain.FailureProgramStartupDirectory,
                FailureProgramParameters = domain.FailureProgramParameters,
                EnvironmentVariables = domain.EnvironmentVariables,
                ServiceDependencies = domain.ServiceDependencies,
                RunAsLocalSystem = domain.RunAsLocalSystem,
                UserAccount = domain.UserAccount,
                Password = domain.Password,
                PreLaunchExecutablePath = domain.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = domain.PreLaunchStartupDirectory,
                PreLaunchParameters = domain.PreLaunchParameters,
                PreLaunchEnvironmentVariables = domain.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = domain.PreLaunchStdoutPath,
                PreLaunchStderrPath = domain.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = domain.PreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = domain.PreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = domain.PreLaunchIgnoreFailure,

                PostLaunchExecutablePath = domain.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = domain.PostLaunchStartupDirectory,
                PostLaunchParameters = domain.PostLaunchParameters,

                EnableDebugLogs = domain.EnableDebugLogs,

                DisplayName = domain.DisplayName,

                StartTimeout = domain.StartTimeout,
                StopTimeout = domain.StopTimeout,

                PreStopExecutablePath = domain.PreStopExecutablePath,
                PreStopStartupDirectory = domain.PreStopStartupDirectory,
                PreStopParameters = domain.PreStopParameters,
                PreStopTimeoutSeconds = domain.PreStopTimeoutSeconds,
                PreStopLogAsError = domain.PreStopLogAsError,

                PostStopExecutablePath = domain.PostStopExecutablePath,
                PostStopStartupDirectory = domain.PostStopStartupDirectory,
                PostStopParameters = domain.PostStopParameters,
            };
        }

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
                RecoveryAction = ConfigParser.ParseEnum(dto.RecoveryAction, RecoveryAction.RestartService),

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