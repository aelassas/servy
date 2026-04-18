using Servy.Core.Config;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
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

                // Validate Enum ranges before mapping from DTO
                StartupType = MapEnum(dto.StartupType, AppConfig.DefaultStartupType),
                Priority = MapEnum(dto.Priority, AppConfig.DefaultPriority),

                StdoutPath = dto.StdoutPath,
                StderrPath = dto.StderrPath,
                EnableSizeRotation = dto.EnableSizeRotation ?? AppConfig.DefaultEnableRotation,
                RotationSize = dto.RotationSize ?? AppConfig.DefaultRotationSize,
                EnableDateRotation = dto.EnableDateRotation ?? AppConfig.DefaultEnableDateRotation,

                // Validate Enum ranges
                DateRotationType = MapEnum(dto.DateRotationType, AppConfig.DefaultDateRotationType),

                MaxRotations = dto.MaxRotations ?? AppConfig.DefaultMaxRotations,
                UseLocalTimeForRotation = dto.UseLocalTimeForRotation ?? AppConfig.DefaultUseLocalTimeForRotation,
                EnableHealthMonitoring = dto.EnableHealthMonitoring ?? AppConfig.DefaultEnableHealthMonitoring,
                HeartbeatInterval = dto.HeartbeatInterval ?? AppConfig.DefaultHeartbeatInterval,
                MaxFailedChecks = dto.MaxFailedChecks ?? AppConfig.DefaultMaxFailedChecks,

                // Validate Enum ranges
                RecoveryAction = MapEnum(dto.RecoveryAction, RecoveryAction.RestartService),

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

        /// <summary>
        /// Validates that an integer value exists within the specified enum range before mapping.
        /// Prevents invalid values from flowing into Win32 API calls.
        /// </summary>
        /// <typeparam name="TEnum">The target enum type.</typeparam>
        /// <param name="value">The raw integer value from the DTO.</param>
        /// <param name="defaultValue">The fallback value if the input is null or invalid.</param>
        /// <returns>A validated enum member.</returns>
        private static TEnum MapEnum<TEnum>(int? value, TEnum defaultValue) where TEnum : struct, Enum
        {
            if (!value.HasValue)
            {
                return defaultValue;
            }

            // Fix for #309 / Test Failure: 
            // Enum.IsDefined requires the value type to match the underlying enum type (e.g., int vs uint).
            // We convert the int to the underlying type of TEnum to ensure compatibility.
            var underlyingType = Enum.GetUnderlyingType(typeof(TEnum));
            var convertedValue = Convert.ChangeType(value.Value, underlyingType);

            if (Enum.IsDefined(typeof(TEnum), convertedValue))
            {
                return (TEnum)convertedValue;
            }

            return defaultValue;
        }
    }
}