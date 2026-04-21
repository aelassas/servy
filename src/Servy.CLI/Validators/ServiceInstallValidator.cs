using CommandLine;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Resources;
using Servy.Core.Validators;

namespace Servy.CLI.Validators
{
    /// <summary>
    /// Validates the installation options for a new service in the CLI environment.
    /// This class bridges CLI-specific options with the shared core validation logic.
    /// </summary>
    public class ServiceInstallValidator : IServiceInstallValidator
    {
        /// <summary>
        /// Validates the provided <see cref="InstallServiceOptions"/> by mapping them to a domain DTO 
        /// and executing centralized validation rules.
        /// </summary>
        /// <param name="opts">The command-line options provided for the install command.</param>
        /// <returns>
        /// A <see cref="CommandResult"/> indicating whether validation passed or detailing the first encountered issue.
        /// </returns>
        public CommandResult Validate(InstallServiceOptions opts)
        {
            // Note: Add a mapping step here (e.g., opts.ToServiceDto()) 
            // Any int.TryParse format failures during mapping should return an early CommandResult.Fail.
            if (!TryMapToDto(opts, out var dto, out var mappingError))
            {
                return CommandResult.Fail(mappingError);
            }

            var result = ServiceValidationRules.Validate(dto);

            if (!result.IsValid)
            {
                // CLI typically reports one error at a time for better readability
                var firstIssue = result.Warnings.Concat(result.Errors).First();
                return CommandResult.Fail(firstIssue);
            }

            var successMsg = string.Format(Strings.Msg_ValidationPassed, opts.ServiceName);
            Logger.Info(successMsg);

            return CommandResult.Ok(successMsg);
        }

        /// <summary>
        /// Attempts to map raw CLI string options into a structured <see cref="ServiceDto"/>.
        /// This method handles initial type conversion (parsing integers and enums).
        /// </summary>
        /// <param name="opts">The source CLI options.</param>
        /// <param name="dto">When this method returns, contains the mapped <see cref="ServiceDto"/> if parsing succeeded; otherwise, <see langword="null"/>.</param>
        /// <param name="error">When this method returns, contains an error message if parsing failed; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if all options were successfully parsed and mapped; otherwise, <see langword="false"/>.</returns>
        private bool TryMapToDto(InstallServiceOptions opts, out ServiceDto? dto, out string? error)
        {
            dto = null;
            string? internalError = null;

            // Local functions capture the local 'internalError' variable, which is allowed.
            // Note: We use 'int?' and 'where T : struct' for standard 4.8 compatibility.
            int? ParseInt(string? val, string propertyName)
            {
                if (internalError != null || string.IsNullOrWhiteSpace(val)) return null;

                int result;
                if (int.TryParse(val, out result))
                    return result;

                internalError = string.Format("Invalid integer format for {0}: '{1}'", GetOptionName(propertyName), val);
                return null;
            }

            int? ParseEnum<T>(string? val, string propertyName) where T : struct
            {
                if (internalError != null || string.IsNullOrWhiteSpace(val)) return null;

                T result;
                if (Enum.TryParse<T>(val, true, out result)) return (int)(object)result;

                internalError = string.Format("Invalid value for {0}: '{1}'. Valid options: {2}",
                    GetOptionName(propertyName), val, string.Join(", ", Enum.GetNames(typeof(T))));
                return null;
            }

            // 1. Map typed arguments using nameof() to fetch attribute names dynamically
            var startupType = ParseEnum<ServiceStartType>(opts.ServiceStartType, nameof(opts.ServiceStartType));
            var priority = ParseEnum<ProcessPriority>(opts.ProcessPriority, nameof(opts.ProcessPriority));
            var rotationSize = ParseInt(opts.RotationSize, nameof(opts.RotationSize));
            var dateRotationType = ParseEnum<DateRotationType>(opts.DateRotationType, nameof(opts.DateRotationType));
            var maxRotations = ParseInt(opts.MaxRotations, nameof(opts.MaxRotations));

            var heartbeatInterval = ParseInt(opts.HeartbeatInterval, nameof(opts.HeartbeatInterval));
            var maxFailedChecks = ParseInt(opts.MaxFailedChecks, nameof(opts.MaxFailedChecks));
            var recoveryAction = ParseEnum<RecoveryAction>(opts.RecoveryAction, nameof(opts.RecoveryAction));
            var maxRestartAttempts = ParseInt(opts.MaxRestartAttempts, nameof(opts.MaxRestartAttempts));

            var preLaunchTimeout = ParseInt(opts.PreLaunchTimeout, nameof(opts.PreLaunchTimeout));
            var preLaunchRetryAttempts = ParseInt(opts.PreLaunchRetryAttempts, nameof(opts.PreLaunchRetryAttempts));

            var startTimeout = ParseInt(opts.StartTimeout, nameof(opts.StartTimeout));
            var stopTimeout = ParseInt(opts.StopTimeout, nameof(opts.StopTimeout));
            var preStopTimeout = ParseInt(opts.PreStopTimeout, nameof(opts.PreStopTimeout));

            if (internalError != null)
            {
                error = internalError;
                return false;
            }

            // 2. Build the DTO
            dto = new ServiceDto
            {
                Name = opts.ServiceName ?? string.Empty,
                DisplayName = opts.ServiceDisplayName ?? string.Empty,
                Description = opts.ServiceDescription,
                ExecutablePath = opts.ProcessPath ?? string.Empty,
                StartupDirectory = opts.StartupDirectory,
                Parameters = opts.ProcessParameters,
                StartupType = startupType,
                Priority = priority,
                StdoutPath = opts.StdoutPath,
                StderrPath = opts.StderrPath,
                EnableSizeRotation = opts.EnableSizeRotation || opts.EnableRotation,
                RotationSize = rotationSize,
                EnableDateRotation = opts.EnableDateRotation,
                DateRotationType = dateRotationType,
                MaxRotations = maxRotations,
                UseLocalTimeForRotation = opts.UseLocalTimeForRotation,
                EnableHealthMonitoring = opts.EnableHealthMonitoring,
                HeartbeatInterval = heartbeatInterval,
                MaxFailedChecks = maxFailedChecks,
                RecoveryAction = recoveryAction,
                MaxRestartAttempts = maxRestartAttempts,
                FailureProgramPath = opts.FailureProgramPath,
                FailureProgramStartupDirectory = opts.FailureProgramStartupDir,
                FailureProgramParameters = opts.FailureProgramParameters,
                EnvironmentVariables = opts.EnvironmentVariables,
                ServiceDependencies = opts.ServiceDependencies,
                UserAccount = opts.User,
                Password = opts.Password,
                RunAsLocalSystem = string.IsNullOrWhiteSpace(opts.User),
                PreLaunchExecutablePath = opts.PreLaunchPath,
                PreLaunchStartupDirectory = opts.PreLaunchStartupDir,
                PreLaunchParameters = opts.PreLaunchParameters,
                PreLaunchEnvironmentVariables = opts.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = opts.PreLaunchStdoutPath,
                PreLaunchStderrPath = opts.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = preLaunchTimeout,
                PreLaunchRetryAttempts = preLaunchRetryAttempts,
                PreLaunchIgnoreFailure = opts.PreLaunchIgnoreFailure,
                PostLaunchExecutablePath = opts.PostLaunchPath,
                PostLaunchStartupDirectory = opts.PostLaunchStartupDir,
                PostLaunchParameters = opts.PostLaunchParameters,
                EnableDebugLogs = opts.EnableDebugLogs,
                StartTimeout = startTimeout,
                StopTimeout = stopTimeout,
                PreStopExecutablePath = opts.PreStopPath,
                PreStopStartupDirectory = opts.PreStopStartupDir,
                PreStopParameters = opts.PreStopParameters,
                PreStopTimeoutSeconds = preStopTimeout,
                PreStopLogAsError = opts.PreStopLogAsError,
                PostStopExecutablePath = opts.PostStopPath,
                PostStopStartupDirectory = opts.PostStopStartupDir,
                PostStopParameters = opts.PostStopParameters
            };

            error = null;
            return true;
        }

        /// <summary>
        /// Retrieves the CLI option name associated with a property using reflection on <see cref="OptionAttribute"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property in <see cref="InstallServiceOptions"/>.</param>
        /// <returns>The CLI flag name (e.g., "--name") or the property name if no attribute is found.</returns>
        private static string GetOptionName(string propertyName)
        {
            var prop = typeof(InstallServiceOptions).GetProperty(propertyName);
            if (prop == null) return propertyName;

            var attr = Attribute.GetCustomAttribute(prop, typeof(OptionAttribute)) as OptionAttribute;
            return attr != null ? "--" + attr.LongName : propertyName;
        }
    }
}