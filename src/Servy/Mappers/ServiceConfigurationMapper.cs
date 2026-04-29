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
                Name = config.Name,
                DisplayName = config.DisplayName,
                Description = config.Description,
                ExecutablePath = config.ExecutablePath,
                StartupDirectory = config.StartupDirectory,
                Parameters = config.Parameters,
                StartupType = config.StartupType,
                Priority = config.Priority,
                StdoutPath = config.StdoutPath,
                StderrPath = config.StderrPath,
                EnableSizeRotation = config.EnableSizeRotation,
                EnableDateRotation = config.EnableDateRotation,
                RotationSize = ConfigParser.ParseInt(config.RotationSize, Core.Config.AppConfig.DefaultRotationSizeMB),
                UseLocalTimeForRotation = config.UseLocalTimeForRotation,
                EnableHealthMonitoring = config.EnableHealthMonitoring,
                HeartbeatInterval = ConfigParser.ParseInt(config.HeartbeatInterval, Core.Config.AppConfig.DefaultHeartbeatInterval),
                MaxFailedChecks = ConfigParser.ParseInt(config.MaxFailedChecks, Core.Config.AppConfig.DefaultMaxFailedChecks),
                RecoveryAction = config.RecoveryAction,
                MaxRestartAttempts = ConfigParser.ParseInt(config.MaxRestartAttempts, Core.Config.AppConfig.DefaultMaxRestartAttempts),
                FailureProgramPath = config.FailureProgramPath,
                FailureProgramStartupDirectory = config.FailureProgramStartupDirectory,
                FailureProgramParameters = config.FailureProgramParameters,
                EnvironmentVariables = config.EnvironmentVariables,
                ServiceDependencies = config.ServiceDependencies,
                RunAsLocalSystem = config.RunAsLocalSystem,
                UserAccount = config.UserAccount,
                Password = config.Password,
                PreLaunchExecutablePath = config.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = config.PreLaunchStartupDirectory,
                PreLaunchParameters = config.PreLaunchParameters,
                PreLaunchEnvironmentVariables = config.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = config.PreLaunchStdoutPath,
                PreLaunchStderrPath = config.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = ConfigParser.ParseInt(config.PreLaunchTimeoutSeconds, Core.Config.AppConfig.DefaultPreLaunchTimeoutSeconds),
                PreLaunchRetryAttempts = ConfigParser.ParseInt(config.PreLaunchRetryAttempts, Core.Config.AppConfig.DefaultPreLaunchRetryAttempts),
                PreLaunchIgnoreFailure = config.PreLaunchIgnoreFailure,
                PostLaunchExecutablePath = config.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = config.PostLaunchStartupDirectory,
                PostLaunchParameters = config.PostLaunchParameters,
                MaxRotations = ConfigParser.ParseInt(config.MaxRotations, Core.Config.AppConfig.DefaultMaxRotations),
                StartTimeout = ConfigParser.ParseInt(config.StartTimeout, Core.Config.AppConfig.DefaultStartTimeout),
                StopTimeout = ConfigParser.ParseInt(config.StopTimeout, Core.Config.AppConfig.DefaultStopTimeout),
                PreStopExecutablePath = config.PreStopExecutablePath,
                PreStopStartupDirectory = config.PreStopStartupDirectory,
                PreStopParameters = config.PreStopParameters,
                PreStopTimeoutSeconds = ConfigParser.ParseInt(config.PreStopTimeoutSeconds, Core.Config.AppConfig.DefaultPreStopTimeoutSeconds),
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
                Description = service.Description,
                DisplayName = service.DisplayName,
                ExecutablePath = service.ExecutablePath,
                StartupDirectory = service.StartupDirectory,
                Parameters = service.Parameters,
                StartupType = service.StartupType,
                Priority = service.Priority,
                StdoutPath = service.StdoutPath,
                StderrPath = service.StderrPath,
                EnableSizeRotation = service.EnableSizeRotation,
                EnableDateRotation = service.EnableDateRotation,
                RotationSize = service.RotationSize.ToString(),
                UseLocalTimeForRotation = service.UseLocalTimeForRotation,
                EnableHealthMonitoring = service.EnableHealthMonitoring,
                HeartbeatInterval = service.HeartbeatInterval.ToString(),
                MaxFailedChecks = service.MaxFailedChecks.ToString(),
                RecoveryAction = service.RecoveryAction,
                MaxRestartAttempts = service.MaxRestartAttempts.ToString(),
                FailureProgramPath = service.FailureProgramPath,
                FailureProgramStartupDirectory = service.FailureProgramStartupDirectory,
                FailureProgramParameters = service.FailureProgramParameters,
                EnvironmentVariables = service.EnvironmentVariables,
                ServiceDependencies = service.ServiceDependencies,
                RunAsLocalSystem = service.RunAsLocalSystem,
                UserAccount = service.UserAccount,
                Password = service.Password,
                ConfirmPassword = service.Password, // For UI prefill
                PreLaunchExecutablePath = service.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = service.PreLaunchStartupDirectory,
                PreLaunchParameters = service.PreLaunchParameters,
                PreLaunchEnvironmentVariables = service.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = service.PreLaunchStdoutPath,
                PreLaunchStderrPath = service.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = service.PreLaunchTimeoutSeconds.ToString(),
                PreLaunchRetryAttempts = service.PreLaunchRetryAttempts.ToString(),
                PreLaunchIgnoreFailure = service.PreLaunchIgnoreFailure,
                PostLaunchExecutablePath = service.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = service.PostLaunchStartupDirectory,
                PostLaunchParameters = service.PostLaunchParameters,
                MaxRotations = service.MaxRotations.ToString(),
                StartTimeout = service.StartTimeout.ToString(),
                StopTimeout = service.StopTimeout.ToString(),

                PreStopExecutablePath = service.PreStopExecutablePath,
                PreStopStartupDirectory = service.PreStopStartupDirectory,
                PreStopParameters = service.PreStopParameters,
                PreStopTimeoutSeconds = service.PreStopTimeoutSeconds.ToString(),
                PreStopLogAsError = service.PreStopLogAsError,

                PostStopExecutablePath = service.PostStopExecutablePath,
                PostStopStartupDirectory = service.PostStopStartupDirectory,
                PostStopParameters = service.PostStopParameters
            };
        }

    }
}
