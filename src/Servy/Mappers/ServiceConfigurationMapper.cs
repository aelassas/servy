using Servy.Core.Domain;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Models;

namespace Servy.Mappers
{
    /// <summary>
    /// Provides mapping methods between <see cref="ServiceConfiguration"/> and domain <see cref="Service"/> objects.
    /// </summary>
    public static class ServiceConfigurationMapper
    {
        /// <summary>
        /// Maps a <see cref="ServiceConfiguration"/> object to a domain <see cref="Service"/> object.
        /// </summary>
        /// <param name="serviceManager">The <see cref="IServiceManager"/> used by the domain service.</param>
        /// <param name="config">The service configuration object to map from.</param>
        /// <returns>A new <see cref="Service"/> instance populated with values from <paramref name="config"/>.</returns>
        public static Service ToDomain(IServiceManager serviceManager, ServiceConfiguration config)
        {
            return new Service(serviceManager)
            {
                Name = config.Name ?? string.Empty,
                DisplayName = config.DisplayName ?? string.Empty,
                Description = config.Description ?? string.Empty,
                ExecutablePath = config.ExecutablePath ?? string.Empty,
                StartupDirectory = config.StartupDirectory ?? string.Empty,
                Parameters = config.Parameters ?? string.Empty,
                StartupType = config.StartupType,
                Priority = config.Priority,
                StdoutPath = config.StdoutPath ?? string.Empty,
                StderrPath = config.StderrPath ?? string.Empty,
                EnableSizeRotation = config.EnableSizeRotation,
                EnableDateRotation = config.EnableDateRotation,
                RotationSize = ConfigParser.ParseInt(config.RotationSize ?? string.Empty, Core.Config.AppConfig.DefaultRotationSizeMB),
                UseLocalTimeForRotation = config.UseLocalTimeForRotation,
                EnableHealthMonitoring = config.EnableHealthMonitoring,
                HeartbeatInterval = ConfigParser.ParseInt(config.HeartbeatInterval ?? string.Empty, Core.Config.AppConfig.DefaultHeartbeatInterval),
                MaxFailedChecks = ConfigParser.ParseInt(config.MaxFailedChecks ?? string.Empty, Core.Config.AppConfig.DefaultMaxFailedChecks),
                RecoveryAction = config.RecoveryAction,
                MaxRestartAttempts = ConfigParser.ParseInt(config.MaxRestartAttempts ?? string.Empty, Core.Config.AppConfig.DefaultMaxRestartAttempts),
                FailureProgramPath = config.FailureProgramPath ?? string.Empty,
                FailureProgramStartupDirectory = config.FailureProgramStartupDirectory ?? string.Empty,
                FailureProgramParameters = config.FailureProgramParameters ?? string.Empty,
                EnvironmentVariables = config.EnvironmentVariables,
                ServiceDependencies = config.ServiceDependencies,
                RunAsLocalSystem = config.RunAsLocalSystem,
                UserAccount = config.UserAccount,
                Password = config.Password,
                PreLaunchExecutablePath = config.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = config.PreLaunchStartupDirectory,
                PreLaunchParameters = config.PreLaunchParameters,
                PreLaunchEnvironmentVariables = config.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = config.PreLaunchStdoutPath ?? string.Empty,
                PreLaunchStderrPath = config.PreLaunchStderrPath ?? string.Empty,
                PreLaunchTimeoutSeconds = ConfigParser.ParseInt(config.PreLaunchTimeoutSeconds ?? string.Empty, Core.Config.AppConfig.DefaultPreLaunchTimeoutSeconds),
                PreLaunchRetryAttempts = ConfigParser.ParseInt(config.PreLaunchRetryAttempts ?? string.Empty, Core.Config.AppConfig.DefaultPreLaunchRetryAttempts),
                PreLaunchIgnoreFailure = config.PreLaunchIgnoreFailure,
                PostLaunchExecutablePath = config.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = config.PostLaunchStartupDirectory,
                PostLaunchParameters = config.PostLaunchParameters,
                MaxRotations = ConfigParser.ParseInt(config.MaxRotations ?? string.Empty, Core.Config.AppConfig.DefaultMaxRotations),
                StartTimeout = ConfigParser.ParseInt(config.StartTimeout ?? string.Empty, Core.Config.AppConfig.DefaultStartTimeout),
                StopTimeout = ConfigParser.ParseInt(config.StopTimeout ?? string.Empty, Core.Config.AppConfig.DefaultStopTimeout),
                PreStopExecutablePath = config.PreStopExecutablePath ?? string.Empty,
                PreStopStartupDirectory = config.PreStopStartupDirectory ?? string.Empty,
                PreStopParameters = config.PreStopParameters ?? string.Empty,
                PreStopTimeoutSeconds = ConfigParser.ParseInt(config.PreStopTimeoutSeconds ?? string.Empty, Core.Config.AppConfig.DefaultPreStopTimeoutSeconds),
                PreStopLogAsError = config.PreStopLogAsError,
                PostStopExecutablePath = config.PostStopExecutablePath,
                PostStopStartupDirectory = config.PostStopStartupDirectory,
                PostStopParameters = config.PostStopParameters
            };
        }

        /// <summary>
        /// Maps a domain <see cref="Service"/> object to a <see cref="ServiceConfiguration"/> object.
        /// </summary>
        /// <param name="service">The domain service to map from.</param>
        /// <returns>A new <see cref="ServiceConfiguration"/> instance populated with values from <paramref name="service"/>.</returns>
        public static ServiceConfiguration FromDomain(Service service)
        {
            return new ServiceConfiguration
            {
                Name = service.Name,
                Description = service.Description ?? string.Empty,
                DisplayName = service.DisplayName,
                ExecutablePath = service.ExecutablePath,
                StartupDirectory = service.StartupDirectory ?? string.Empty,
                Parameters = service.Parameters ?? string.Empty,
                StartupType = service.StartupType,
                Priority = service.Priority,
                StdoutPath = service.StdoutPath ?? string.Empty,
                StderrPath = service.StderrPath ?? string.Empty,
                EnableSizeRotation = service.EnableSizeRotation,
                EnableDateRotation = service.EnableDateRotation,
                RotationSize = service.RotationSize.ToString(),
                UseLocalTimeForRotation = service.UseLocalTimeForRotation,
                EnableHealthMonitoring = service.EnableHealthMonitoring,
                HeartbeatInterval = service.HeartbeatInterval.ToString(),
                MaxFailedChecks = service.MaxFailedChecks.ToString(),
                RecoveryAction = service.RecoveryAction,
                MaxRestartAttempts = service.MaxRestartAttempts.ToString(),
                FailureProgramPath = service.FailureProgramPath ?? string.Empty,
                FailureProgramStartupDirectory = service.FailureProgramStartupDirectory ?? string.Empty,
                FailureProgramParameters = service.FailureProgramParameters ?? string.Empty,
                EnvironmentVariables = service.EnvironmentVariables ?? string.Empty,
                ServiceDependencies = service.ServiceDependencies ?? string.Empty,
                RunAsLocalSystem = service.RunAsLocalSystem,
                UserAccount = service.UserAccount ?? string.Empty,
                Password = service.Password ?? string.Empty,
                ConfirmPassword = service.Password ?? string.Empty, // For UI prefill
                PreLaunchExecutablePath = service.PreLaunchExecutablePath ?? string.Empty,
                PreLaunchStartupDirectory = service.PreLaunchStartupDirectory ?? string.Empty,
                PreLaunchParameters = service.PreLaunchParameters ?? string.Empty,
                PreLaunchEnvironmentVariables = service.PreLaunchEnvironmentVariables ?? string.Empty,
                PreLaunchStdoutPath = service.PreLaunchStdoutPath ?? string.Empty,
                PreLaunchStderrPath = service.PreLaunchStderrPath ?? string.Empty,
                PreLaunchTimeoutSeconds = service.PreLaunchTimeoutSeconds.ToString(),
                PreLaunchRetryAttempts = service.PreLaunchRetryAttempts.ToString(),
                PreLaunchIgnoreFailure = service.PreLaunchIgnoreFailure,
                PostLaunchExecutablePath = service.PostLaunchExecutablePath ?? string.Empty,
                PostLaunchStartupDirectory = service.PostLaunchStartupDirectory ?? string.Empty,
                PostLaunchParameters = service.PostLaunchParameters ?? string.Empty,
                MaxRotations = service.MaxRotations.ToString(),
                StartTimeout = service.StartTimeout.ToString(),
                StopTimeout = service.StopTimeout.ToString(),

                PreStopExecutablePath = service.PreStopExecutablePath ?? string.Empty,
                PreStopStartupDirectory = service.PreStopStartupDirectory ?? string.Empty,
                PreStopParameters = service.PreStopParameters ?? string.Empty,
                PreStopTimeoutSeconds = service.PreStopTimeoutSeconds.ToString(),
                PreStopLogAsError = service.PreStopLogAsError,

                PostStopExecutablePath = service.PostStopExecutablePath ?? string.Empty,
                PostStopStartupDirectory = service.PostStopStartupDirectory ?? string.Empty,
                PostStopParameters = service.PostStopParameters ?? string.Empty,
            };
        }

    }
}
