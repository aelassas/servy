using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Domain;
using static Servy.Core.Config.AppConfig;
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
        /// <param name="service">The domain service object to map.</param>
        /// <param name="id">Service ID.</param>
        /// <returns>A <see cref="ServiceDto"/> representing the service for storage.</returns>
        public static ServiceDto ToDto(Service service, int? id = null)
        {
            return new ServiceDto
            {
                Id = id ?? 0, // 0 for insert, actual Id for update
                Name = service.Name,
                Description = service.Description,
                ExecutablePath = service.ExecutablePath,
                StartupDirectory = service.StartupDirectory,
                Parameters = service.Parameters,
                StartupType = (int)service.StartupType,
                Priority = (int)service.Priority,
                StdoutPath = service.StdoutPath,
                StderrPath = service.StderrPath,
                EnableRotation = service.EnableRotation,
                RotationSize = service.RotationSize,
                EnableHealthMonitoring = service.EnableHealthMonitoring,
                HeartbeatInterval = service.HeartbeatInterval,
                MaxFailedChecks = service.MaxFailedChecks,
                RecoveryAction = (int)service.RecoveryAction,
                MaxRestartAttempts = service.MaxRestartAttempts,
                EnvironmentVariables = service.EnvironmentVariables,
                ServiceDependencies = service.ServiceDependencies,
                RunAsLocalSystem = service.RunAsLocalSystem,
                UserAccount = service.UserAccount,
                Password = service.Password,
                PreLaunchExecutablePath = service.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = service.PreLaunchStartupDirectory,
                PreLaunchParameters = service.PreLaunchParameters,
                PreLaunchEnvironmentVariables = service.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = service.PreLaunchStdoutPath,
                PreLaunchStderrPath = service.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = service.PreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = service.PreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = service.PreLaunchIgnoreFailure
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
            return new Service(serviceManager)
            {
                Name = dto.Name,
                Description = dto.Description,
                ExecutablePath = dto.ExecutablePath,
                StartupDirectory = dto.StartupDirectory,
                Parameters = dto.Parameters,
                StartupType = dto.StartupType == null ? ServiceStartType.Automatic : (ServiceStartType)dto.StartupType,
                Priority = dto.Priority == null ? ProcessPriority.Normal : (ProcessPriority)dto.Priority,
                StdoutPath = dto.StdoutPath,
                StderrPath = dto.StderrPath,
                EnableRotation = dto.EnableRotation ?? false,
                RotationSize = dto.RotationSize ?? DefaultRotationSize,
                EnableHealthMonitoring = dto.EnableHealthMonitoring ?? false,
                HeartbeatInterval = dto.HeartbeatInterval ?? DefaultHeartbeatInterval,
                MaxFailedChecks = dto.MaxFailedChecks ?? DefaultMaxFailedChecks,
                RecoveryAction = dto.RecoveryAction == null ? RecoveryAction.RestartService : (RecoveryAction)dto.RecoveryAction,
                MaxRestartAttempts = dto.MaxRestartAttempts ?? DefaultMaxRestartAttempts,
                EnvironmentVariables = dto.EnvironmentVariables,
                ServiceDependencies = dto.ServiceDependencies,
                RunAsLocalSystem = dto.RunAsLocalSystem ?? true,
                UserAccount = dto.UserAccount,
                Password = dto.Password,
                PreLaunchExecutablePath = dto.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = dto.PreLaunchStartupDirectory,
                PreLaunchParameters = dto.PreLaunchParameters,
                PreLaunchEnvironmentVariables = dto.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = dto.PreLaunchStdoutPath,
                PreLaunchStderrPath = dto.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds ?? DefaultPreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts ?? DefaultPreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? false
            };
        }
    }
}
