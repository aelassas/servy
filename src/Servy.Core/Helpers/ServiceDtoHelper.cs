using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using System;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides utility methods for managing and augmenting <see cref="ServiceDto"/> objects.
    /// </summary>
    /// <remarks>
    /// This helper is used during the deserialization of XML and JSON configurations
    /// to ensure that all required service parameters are populated with production-grade
    /// defaults defined in <see cref="AppConfig"/>.
    /// </remarks>
    public static class ServiceDtoHelper
    {
        /// <summary>
        /// Creates a shallow copy of a <see cref="ServiceDto"/>.
        /// Delegates to <see cref="ICloneable.Clone"/> to perform a memberwise clone, preventing property drift when new properties are added.
        /// </summary>
        /// <param name="dto">The service data transfer object to copy.</param>
        /// <returns>A new <see cref="ServiceDto"/> instance with identical property values.</returns>
        public static ServiceDto Clone(ServiceDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return (ServiceDto)dto.Clone();
        }

        /// <summary>
        /// Populates null nullable properties with their matching system configurations sourced from <see cref="AppConfig"/>,
        /// normalizes string configuration fields (environment variables and dependencies), and normalizes identity fields
        /// by trimming surrounding whitespace from <c>UserAccount</c>.
        /// Unlike <see cref="ApplyDefaultsAndResetIdentity"/>, it never resets or clears identity values.
        /// </summary>
        /// <param name="dto">The service data transfer object layout to populate. The instance is modified in place.</param>
        /// <remarks>
        /// ServiceMapper.ToDomain dereferences each property set here with '!.Value'.
        /// Removing a line from this method, or adding a nullable ServiceDto property without one,
        /// turns that read into a NullReferenceException on the database load path.
        /// </remarks>
        public static void HydrateDefaults(ServiceDto dto)
        {
            if (dto == null) return;

            // Identity & Behavior
            dto.StartupType = dto.StartupType ?? (int)AppConfig.DefaultStartupType;
            dto.Priority = dto.Priority ?? (int)AppConfig.DefaultProcessPriority;
            dto.RunAsLocalSystem = dto.RunAsLocalSystem ?? AppConfig.DefaultRunAsLocalSystem;
            dto.EnableDebugLogs = dto.EnableDebugLogs ?? AppConfig.DefaultEnableDebugLogs;

            // Timeouts
            dto.StartTimeout = dto.StartTimeout ?? AppConfig.DefaultStartTimeout;
            dto.StopTimeout = dto.StopTimeout ?? AppConfig.DefaultStopTimeout;

            // EnableConsoleUI
            dto.EnableConsoleUI = dto.EnableConsoleUI ?? AppConfig.DefaultEnableConsoleUI;

            // Log Rotation
            dto.EnableSizeRotation = dto.EnableSizeRotation ?? AppConfig.DefaultEnableSizeRotation;
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
            dto.RecoveryOnCleanExit = dto.RecoveryOnCleanExit ?? AppConfig.DefaultRecoveryOnCleanExit;
            dto.HeartbeatUrlTimeoutSeconds = dto.HeartbeatUrlTimeoutSeconds ?? AppConfig.DefaultHeartbeatUrlTimeoutSeconds;
            dto.EnableHeartbeatUrlFlags = dto.EnableHeartbeatUrlFlags ?? AppConfig.DefaultEnableHeartbeatUrlFlags;

            // Identity Normalization
            dto.UserAccount = dto.UserAccount?.Trim();

            // Environment & Dependencies Normalization
            dto.EnvironmentVariables = StringHelper.NormalizeString(dto.EnvironmentVariables);
            dto.AllowOverriddenRuntimeVars = dto.AllowOverriddenRuntimeVars ?? AppConfig.DefaultAllowOverriddenRuntimeVars;
            dto.ServiceDependencies = StringHelper.NormalizeString(dto.ServiceDependencies);
            dto.PreLaunchEnvironmentVariables = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);

            // Lifecycle Hooks (Pre-Launch)
            dto.PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds;
            dto.PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts;
            dto.PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? AppConfig.DefaultPreLaunchIgnoreFailure;

            // Lifecycle Hooks (Pre-Stop)
            dto.PreStopTimeoutSeconds = dto.PreStopTimeoutSeconds ?? AppConfig.DefaultPreStopTimeoutSeconds;
            dto.PreStopLogAsError = dto.PreStopLogAsError ?? AppConfig.DefaultPreStopLogAsError;
        }

        /// <summary>
        /// Populates null nullable properties that have production defaults in <see cref="AppConfig"/>
        /// (startup, priority, timeouts, log rotation, health monitoring, pre-launch/pre-stop options).
        /// Optional free-form fields (paths, parameters, hooks, dependencies) without defaults remain null.
        /// Additionally, applies the Global Identity Reset on Import policy: Password is always cleared,
        /// a passwordless UserAccount (see <see cref="ServiceAccounts.IsPasswordlessAccount"/>), or a local user
        /// account that can log on with a blank password on this machine (see
        /// <see cref="ServiceAccounts.IsBlankPasswordLocalAccount"/>), is kept with RunAsLocalSystem set to
        /// <c>false</c>, and any other identity is reset to the password-less LocalSystem baseline.
        /// </summary>
        /// <param name="dto">The service DTO to hydrate. The instance is modified in place. If null, the method returns immediately.</param>
        public static void ApplyDefaultsAndResetIdentity(ServiceDto dto)
        {
            if (dto == null) return;

            // Hydrate structural parameters via our unified local template method
            HydrateDefaults(dto);

            // POLICY: Global Identity Reset on Import, except for passwordless accounts
            // A password is never read from an import file, so an account that needs one cannot be
            // applied: it is reset to the password-less LocalSystem baseline and must be set manually
            // after import. A passwordless account (LocalService, NetworkService, NT SERVICE\..., IIS
            // APPPOOL\..., a gMSA) carries no secret, so it is kept as the file names it. So is a local
            // user account with a blank password, but only when this machine allows a service to log on
            // with one (LimitBlankPasswordUse = 0) and a blank-password logon for it succeeds.
            dto.Password = null;

            var account = dto.UserAccount?.Trim();
            if (ServiceAccounts.IsPasswordlessAccount(account))
            {
                dto.RunAsLocalSystem = false;
                dto.UserAccount = account;

                Logger.Info($"Import: Service configuration applied for '{dto.Name}'. The passwordless account '{account}' " +
                            "named in the imported manifest is kept.");
                return;
            }

            if (ServiceAccounts.IsBlankPasswordLocalAccount(account))
            {
                dto.RunAsLocalSystem = false;
                dto.UserAccount = account;

                Logger.Info($"Import: Service configuration applied for '{dto.Name}'. The local account '{account}' named in the " +
                            "imported manifest has a blank password and LimitBlankPasswordUse is 0 on this machine, so it is kept.");
                return;
            }

            dto.RunAsLocalSystem = AppConfig.DefaultRunAsLocalSystem;
            dto.UserAccount = null;

            // Log an informational notice to satisfy operator visibility requirements.
            // An account that needs a password is never exported, so this notice alerts
            // administrators that the service defaults safely to LocalSystem.
            Logger.Info($"Import: Service configuration applied for '{dto.Name}'. The Global Identity Reset policy enforces " +
                        "the LocalSystem identity on imported manifests that do not name a passwordless account. A custom account can be configured manually if required.");
        }
    }
}
