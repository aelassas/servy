using Servy.Core.Domain;
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
                EnableRotation = config.EnableSizeRotation,
                RotationSize = ParseInt(config.RotationSize, Core.Config.AppConfig.DefaultRotationSize),
                EnableHealthMonitoring = config.EnableHealthMonitoring,
                HeartbeatInterval = ParseInt(config.HeartbeatInterval, 30),
                MaxFailedChecks = ParseInt(config.MaxFailedChecks, 3),
                RecoveryAction = config.RecoveryAction,
                MaxRestartAttempts = ParseInt(config.MaxRestartAttempts, 3),
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
                PreLaunchTimeoutSeconds = ParseInt(config.PreLaunchTimeoutSeconds, 30),
                PreLaunchRetryAttempts = ParseInt(config.PreLaunchRetryAttempts, 0),
                PreLaunchIgnoreFailure = config.PreLaunchIgnoreFailure,
                PostLaunchExecutablePath = config.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = config.PostLaunchStartupDirectory,
                PostLaunchParameters = config.PostLaunchParameters,
                MaxRotations = ParseInt(config.MaxRotations, Core.Config.AppConfig.DefaultMaxRotations),
                StartTimeout = ParseInt(config.StartTimeout, Core.Config.AppConfig.DefaultStartTimeout),
                StopTimeout = ParseInt(config.StopTimeout, Core.Config.AppConfig.DefaultStopTimeout),
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
                ExecutablePath = service.ExecutablePath,
                StartupDirectory = service.StartupDirectory,
                Parameters = service.Parameters,
                StartupType = service.StartupType,
                Priority = service.Priority,
                StdoutPath = service.StdoutPath,
                StderrPath = service.StderrPath,
                EnableSizeRotation = service.EnableRotation,
                RotationSize = service.RotationSize.ToString(),
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
            };
        }

        /// <summary>
        /// Attempts to parse a string into an integer, returning a default value if parsing fails.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <param name="defaultValue">The default value to return if parsing fails.</param>
        /// <returns>The parsed integer, or <paramref name="defaultValue"/> if parsing fails.</returns>
        private static int ParseInt(string value, int defaultValue)
        {
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}
