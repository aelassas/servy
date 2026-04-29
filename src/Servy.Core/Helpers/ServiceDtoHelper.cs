using Servy.Core.Config;
using Servy.Core.DTOs;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides utility methods for managing and augmenting <see cref="ServiceDto"/> objects.
    /// </summary>
    /// <remarks>
    /// This helper is primarily used during the deserialization of XML and JSON configurations 
    /// to ensure that all required service parameters are populated with production-grade 
    /// defaults defined in <see cref="AppConfig"/>.
    /// </remarks>
    public static class ServiceDtoHelper
    {
        /// <summary>
        /// Populates any null nullable properties with production defaults from AppConfig.
        /// </summary>
        /// <param name="dto">The service DTO to hydrate. If null, the method returns immediately.</param>
        public static void ApplyDefaults(ServiceDto dto)
        {
            if (dto == null) return;

            // Identity & Behavior
            dto.StartupType = dto.StartupType ?? (int)AppConfig.DefaultStartupType;
            dto.Priority = dto.Priority ?? (int)AppConfig.DefaultPriority;
            dto.RunAsLocalSystem = AppConfig.DefaultRunAsLocalSystem;
            dto.UserAccount = null;
            dto.Password = null;
            dto.EnableDebugLogs = dto.EnableDebugLogs ?? AppConfig.DefaultEnableDebugLogs;

            // Timeouts
            dto.StartTimeout = dto.StartTimeout ?? AppConfig.DefaultStartTimeout;
            dto.StopTimeout = dto.StopTimeout ?? AppConfig.DefaultStopTimeout;

            // Log Rotation
            dto.EnableSizeRotation = dto.EnableSizeRotation ?? AppConfig.DefaultEnableRotation;
            dto.RotationSize = dto.RotationSize ?? AppConfig.DefaultRotationSizeMB;
            dto.EnableDateRotation = dto.EnableDateRotation ?? AppConfig.DefaultEnableDateRotation;
            dto.DateRotationType = dto.DateRotationType ?? (int)AppConfig.DefaultDateRotationType;
            dto.MaxRotations = dto.MaxRotations ?? AppConfig.DefaultMaxRotations;
            dto.UseLocalTimeForRotation = dto.UseLocalTimeForRotation ?? AppConfig.DefaultUseLocalTimeForRotation;

            // Health Monitoring
            dto.EnableHealthMonitoring = dto.EnableHealthMonitoring ?? AppConfig.DefaultEnableHealthMonitoring;
            dto.HeartbeatInterval = dto.HeartbeatInterval ?? AppConfig.DefaultHeartbeatInterval;
            dto.MaxFailedChecks = dto.MaxFailedChecks ?? AppConfig.DefaultMaxFailedChecks;
            dto.MaxRestartAttempts = dto.MaxRestartAttempts ?? AppConfig.DefaultMaxRestartAttempts;
            dto.RecoveryAction = dto.RecoveryAction ?? (int)AppConfig.DefaultRecoveryAction;

            // Lifecycle Hooks (Pre-Launch)
            dto.PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds;
            dto.PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts;
            dto.PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? AppConfig.DefaultPreLaunchIgnoreFailure;

            // Lifecycle Hooks (Pre-Stop)
            dto.PreStopTimeoutSeconds = dto.PreStopTimeoutSeconds ?? AppConfig.DefaultPreStopTimeoutSeconds;
            dto.PreStopLogAsError = dto.PreStopLogAsError ?? AppConfig.DefaultPreStopLogAsError;
        }
    }
}