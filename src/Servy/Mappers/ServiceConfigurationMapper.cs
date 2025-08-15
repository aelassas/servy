using Servy.Core.Models;
using Servy.Models;

namespace Servy.Mappers
{
    public static class ServiceConfigurationMapper
    {
        public static Service ToDomain(ServiceConfiguration config)
        {
            return new Service
            {
                Name = config.Name,
                Description = config.Description,
                ExecutablePath = config.ExecutablePath,
                StartupDirectory = config.StartupDirectory,
                Parameters = config.Parameters,
                StartupType = config.StartupType,
                Priority = config.Priority,
                StdoutPath = config.StdoutPath,
                StderrPath = config.StderrPath,
                EnableRotation = config.EnableRotation,
                RotationSize = ParseInt(config.RotationSize, 1048576),
                EnableHealthMonitoring = config.EnableHealthMonitoring,
                HeartbeatInterval = ParseInt(config.HeartbeatInterval, 30),
                MaxFailedChecks = ParseInt(config.MaxFailedChecks, 3),
                RecoveryAction = config.RecoveryAction,
                MaxRestartAttempts = ParseInt(config.MaxRestartAttempts, 3),
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
                PreLaunchIgnoreFailure = config.PreLaunchIgnoreFailure
            };
        }

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
                EnableRotation = service.EnableRotation,
                RotationSize = service.RotationSize.ToString(),
                EnableHealthMonitoring = service.EnableHealthMonitoring,
                HeartbeatInterval = service.HeartbeatInterval.ToString(),
                MaxFailedChecks = service.MaxFailedChecks.ToString(),
                RecoveryAction = service.RecoveryAction,
                MaxRestartAttempts = service.MaxRestartAttempts.ToString(),
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
                PreLaunchIgnoreFailure = service.PreLaunchIgnoreFailure
            };
        }

        private static int ParseInt(string value, int defaultValue)
        {
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}
