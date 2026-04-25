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
        private readonly ServiceValidationRules _serviceValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceInstallValidator"/> class with the specified validation rules.
        /// </summary>
        /// <param name="serviceValidationRules">Shared validation rules for service installation.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceValidationRules"/> is null.</exception>
        public ServiceInstallValidator(ServiceValidationRules serviceValidationRules)
        {
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
        }

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

            var result = _serviceValidationRules.Validate(dto);

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

            // By passing 'internalError' by ref to a static method, 
            // the analyzer MUST assume it could be modified.
            var startupType = MapEnum<ServiceStartType>(opts.ServiceStartType, nameof(opts.ServiceStartType), ref internalError);
            var priority = MapEnum<ProcessPriority>(opts.ProcessPriority, nameof(opts.ProcessPriority), ref internalError);
            var rotationSize = MapInt(opts.RotationSize, nameof(opts.RotationSize), ref internalError);
            var dateRotationType = MapEnum<DateRotationType>(opts.DateRotationType, nameof(opts.DateRotationType), ref internalError);
            var maxRotations = MapInt(opts.MaxRotations, nameof(opts.MaxRotations), ref internalError);
            var heartbeatInterval = MapInt(opts.HeartbeatInterval, nameof(opts.HeartbeatInterval), ref internalError);
            var maxFailedChecks = MapInt(opts.MaxFailedChecks, nameof(opts.MaxFailedChecks), ref internalError);
            var recoveryAction = MapEnum<RecoveryAction>(opts.RecoveryAction, nameof(opts.RecoveryAction), ref internalError);
            var maxRestartAttempts = MapInt(opts.MaxRestartAttempts, nameof(opts.MaxRestartAttempts), ref internalError);
            var preLaunchTimeout = MapInt(opts.PreLaunchTimeout, nameof(opts.PreLaunchTimeout), ref internalError);
            var preLaunchRetryAttempts = MapInt(opts.PreLaunchRetryAttempts, nameof(opts.PreLaunchRetryAttempts), ref internalError);
            var startTimeout = MapInt(opts.StartTimeout, nameof(opts.StartTimeout), ref internalError);
            var stopTimeout = MapInt(opts.StopTimeout, nameof(opts.StopTimeout), ref internalError);
            var preStopTimeout = MapInt(opts.PreStopTimeout, nameof(opts.PreStopTimeout), ref internalError);

            // The analyzer now sees this as reachable code.
            if (internalError != null)
            {
                error = internalError;
                return false;
            }

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
        /// Attempts to parse a string value into a nullable integer.
        /// </summary>
        /// <param name="val">The string value to parse.</param>
        /// <param name="propertyName">The name of the property being mapped, used for error reporting.</param>
        /// <param name="error">A reference to an error string. If an error already exists, the method returns early. If parsing fails, this reference is updated with a formatted error message.</param>
        /// <returns> The parsed integer if successful; otherwise, <see langword="null"/>.</returns>
        private static int? MapInt(string? val, string propertyName, ref string? error)
        {
            if (error != null || string.IsNullOrWhiteSpace(val)) return null;

            int result;
            if (int.TryParse(val, out result)) return result;

            error = string.Format("Invalid integer format for {0}: '{1}'", GetOptionName(propertyName), val);
            return null;
        }

        /// <summary>
        /// Attempts to parse a string value into an enumeration of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The enumeration type. Must be a value type.</typeparam>
        /// <param name="val">The string value to parse.</param>
        /// <param name="propertyName">The name of the property being mapped, used for error reporting.</param>
        /// <param name="error">A reference to an error string. If an error already exists, the method returns early. If parsing fails, this reference is updated with a formatted error message containing valid options.</param>
        /// <returns>The integer representation of the enum value if successful; otherwise, <see langword="null"/>.</returns>
        private static int? MapEnum<T>(string? val, string propertyName, ref string? error) where T : struct
        {
            if (error != null || string.IsNullOrWhiteSpace(val)) return null;

            if (Enum.TryParse<T>(val, true, out T result))
            {
                // Use Convert.ToInt32 to avoid InvalidCastException during unboxing
                return Convert.ToInt32(result);
            }

            error = string.Format("Invalid value for {0}: '{1}'. Valid options: {2}",
                GetOptionName(propertyName), val, string.Join(", ", Enum.GetNames(typeof(T))));
            return null;
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