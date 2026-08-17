using Servy.Core.Config;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Services;
using System;

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

            // Hydrate raw property defaults on a copy to avoid mutating the caller's DTO instance
            var hydratedDto = ServiceDtoHelper.Clone(dto);
            ServiceDtoHelper.HydrateDefaults(hydratedDto);

            return new Service(serviceManager)
            {
                Name = hydratedDto.Name,
                Description = hydratedDto.Description,
                ExecutablePath = hydratedDto.ExecutablePath,
                StartupDirectory = hydratedDto.StartupDirectory,
                Parameters = hydratedDto.Parameters,

                // Validate Enum ranges before mapping from DTO using shared parser
                StartupType = ConfigParser.ParseEnum(hydratedDto.StartupType, AppConfig.DefaultStartupType),
                Priority = ConfigParser.ParseEnum(hydratedDto.Priority, AppConfig.DefaultProcessPriority),

                CpuAffinity = hydratedDto.CpuAffinity,

                EnableConsoleUI = hydratedDto.EnableConsoleUI.Value,

                StdoutPath = hydratedDto.StdoutPath,
                StderrPath = hydratedDto.StderrPath,
                EnableSizeRotation = hydratedDto.EnableSizeRotation.Value,
                RotationSize = hydratedDto.RotationSize.Value,
                EnableDateRotation = hydratedDto.EnableDateRotation.Value,

                // Validate Enum ranges
                DateRotationType = ConfigParser.ParseEnum(hydratedDto.DateRotationType, AppConfig.DefaultDateRotationType),

                MaxRotations = hydratedDto.MaxRotations.Value,
                UseLocalTimeForRotation = hydratedDto.UseLocalTimeForRotation.Value,
                EnableHealthMonitoring = hydratedDto.EnableHealthMonitoring.Value,
                HeartbeatInterval = hydratedDto.HeartbeatInterval.Value,
                MaxFailedChecks = hydratedDto.MaxFailedChecks.Value,

                // Validate Enum ranges
                RecoveryAction = ConfigParser.ParseEnum(hydratedDto.RecoveryAction, AppConfig.DefaultRecoveryAction),

                RecoveryOnCleanExit = hydratedDto.RecoveryOnCleanExit.Value,

                MaxRestartAttempts = hydratedDto.MaxRestartAttempts.Value,
                HeartbeatUrl = hydratedDto.HeartbeatUrl,
                HeartbeatUrlTimeoutSeconds = hydratedDto.HeartbeatUrlTimeoutSeconds.Value,
                EnableHeartbeatUrlFlags = hydratedDto.EnableHeartbeatUrlFlags.Value,
                FailureProgramPath = hydratedDto.FailureProgramPath,
                FailureProgramStartupDirectory = hydratedDto.FailureProgramStartupDirectory,
                FailureProgramParameters = hydratedDto.FailureProgramParameters,
                EnvironmentVariables = hydratedDto.EnvironmentVariables,
                ServiceDependencies = hydratedDto.ServiceDependencies,
                RunAsLocalSystem = hydratedDto.RunAsLocalSystem ?? AppConfig.DefaultRunAsLocalSystem,
                UserAccount = hydratedDto.UserAccount,
                Password = hydratedDto.Password,
                PreLaunchExecutablePath = hydratedDto.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = hydratedDto.PreLaunchStartupDirectory,
                PreLaunchParameters = hydratedDto.PreLaunchParameters,
                PreLaunchEnvironmentVariables = hydratedDto.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = hydratedDto.PreLaunchStdoutPath,
                PreLaunchStderrPath = hydratedDto.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = hydratedDto.PreLaunchTimeoutSeconds.Value,
                PreLaunchRetryAttempts = hydratedDto.PreLaunchRetryAttempts.Value,
                PreLaunchIgnoreFailure = hydratedDto.PreLaunchIgnoreFailure.Value,

                PostLaunchExecutablePath = hydratedDto.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = hydratedDto.PostLaunchStartupDirectory,
                PostLaunchParameters = hydratedDto.PostLaunchParameters,

                EnableDebugLogs = hydratedDto.EnableDebugLogs.Value,

                DisplayName = hydratedDto.DisplayName ?? string.Empty,

                StartTimeout = hydratedDto.StartTimeout.Value,
                StopTimeout = hydratedDto.StopTimeout.Value,

                Pid = hydratedDto.Pid,
                ActiveStdoutPath = hydratedDto.ActiveStdoutPath,
                ActiveStderrPath = hydratedDto.ActiveStderrPath,

                PreStopExecutablePath = hydratedDto.PreStopExecutablePath,
                PreStopStartupDirectory = hydratedDto.PreStopStartupDirectory,
                PreStopParameters = hydratedDto.PreStopParameters,
                PreStopTimeoutSeconds = hydratedDto.PreStopTimeoutSeconds.Value,
                PreStopLogAsError = hydratedDto.PreStopLogAsError.Value,

                PostStopExecutablePath = hydratedDto.PostStopExecutablePath,
                PostStopStartupDirectory = hydratedDto.PostStopStartupDirectory,
                PostStopParameters = hydratedDto.PostStopParameters,
            };
        }
    }
}
