using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using Servy.Core.ServiceDependencies;

namespace Servy.Core.Validation
{
    /// <summary>
    /// Provides centralized validation logic for service configurations across all Servy components.
    /// </summary>
    public class ServiceValidationRules : IServiceValidationRules
    {
        private readonly IProcessHelper _processHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceValidationRules"/> class with the specified process helper.
        /// </summary>
        /// <param name="processHelper">Provides methods to validate executable paths and gather process metrics.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="processHelper"/> is null.</exception>
        public ServiceValidationRules(IProcessHelper processHelper)
        {
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        }

        /// <inheritdoc />
        public ValidationResult Validate(ServiceDto? dto, string? wrapperExePath = null, string? confirmPassword = null, bool importMode = false)
        {
            var result = new ValidationResult();

            // Basic Requirements
            if (dto == null)
            {
                result.Errors.Add(Strings.Msg_ValidationError);
                return result; // Stop early for completely missing payload configurations
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                result.Errors.Add(Strings.Msg_ServiceNameRequired);
                return result; // Stop early for missing vital fields
            }

            if (string.IsNullOrWhiteSpace(dto.ExecutablePath))
            {
                result.Errors.Add(Strings.Msg_ExecutablePathRequired);
                return result; // Stop early for missing vital fields
            }

            var (isValidName, errorMsg) = Helper.IsServiceNameValid(dto.Name);

            if (!isValidName)
            {
                result.Errors.Add(errorMsg);
                return result;
            }

            // Length Bounds
            if (dto.DisplayName?.Length > AppConfig.MaxDisplayNameLength)
                result.Errors.Add(string.Format(Strings.Msg_DisplayNameLengthReached, AppConfig.MaxDisplayNameLength));
            if (dto.Description?.Length > AppConfig.MaxDescriptionLength)
                result.Errors.Add(string.Format(Strings.Msg_DescriptionLengthReached, AppConfig.MaxDescriptionLength));

            var paramFieldsNamed = new (string Label, string? Value)[]
            {
                (Strings.Label_Parameters,               dto.Parameters),
                (Strings.Label_PreLaunchParameters,      dto.PreLaunchParameters),
                (Strings.Label_PostLaunchParameters,     dto.PostLaunchParameters),
                (Strings.Label_PreStopParameters,        dto.PreStopParameters),
                (Strings.Label_PostStopParameters,       dto.PostStopParameters),
                (Strings.Label_FailureProgramParameters, dto.FailureProgramParameters),
            };
            foreach (var (label, value) in paramFieldsNamed)
            {
                if (value?.Length > AppConfig.MaxArgumentLength)
                    result.Errors.Add(string.Format(Strings.Msg_ArgumentsLengthReachedForField, label, AppConfig.MaxArgumentLength));
            }

            // CpuAffinity
            if (!AffinityHelper.ValidateAffinity(dto.CpuAffinity, out string? errorMessage))
                result.Errors.Add(errorMessage ?? string.Format(Strings.Msg_InvalidConfig, nameof(dto.CpuAffinity)));

            // Reflected [ServicePath] Validation
            var pathViolations = ServicePathValidator.FindAllViolations(dto, _processHelper.ValidatePath);
            foreach (var pathViolation in pathViolations)
            {
                result.Errors.Add(pathViolation.ResolveErrorMessage());
            }

            // External Wrapper Executable Path
            if (!string.IsNullOrWhiteSpace(wrapperExePath) && !_processHelper.ValidatePath(wrapperExePath))
                result.Errors.Add(Strings.Msg_InvalidWrapperExePath);

            // Output Log Destination Paths (not evaluated via [ServicePath] as target files may not exist prior to service creation)
            if (!string.IsNullOrWhiteSpace(dto.StdoutPath) && !Helper.IsValidPath(dto.StdoutPath))
                result.Errors.Add(Strings.Msg_InvalidStdoutPath);
            if (!string.IsNullOrWhiteSpace(dto.StderrPath) && !Helper.IsValidPath(dto.StderrPath))
                result.Errors.Add(Strings.Msg_InvalidStderrPath);

            // One shape for every bounded numeric field: the value, its two bounds and the message
            // that reports them are stated once, at the single line where they meet.
            void CheckRange(int? value, int min, int max, string message)
            {
                if (value.HasValue && (value < min || value > max))
                    result.Errors.Add(string.Format(message, min, max));
            }

            // Timeouts & Rotation Bounds
            CheckRange(dto.StartTimeout, AppConfig.MinStartTimeout, AppConfig.MaxStartTimeout, Strings.Msg_InvalidStartTimeout);
            CheckRange(dto.StopTimeout, AppConfig.MinStopTimeout, AppConfig.MaxStopTimeout, Strings.Msg_InvalidStopTimeout);
            CheckRange(dto.RotationSize, AppConfig.MinRotationSize, AppConfig.MaxRotationSize, Strings.Msg_InvalidRotationSize);
            CheckRange(dto.MaxRotations, AppConfig.MinMaxRotations, AppConfig.MaxMaxRotations, Strings.Msg_InvalidMaxRotations);
            CheckRange(dto.HeartbeatUrlTimeoutSeconds, AppConfig.MinHeartbeatUrlTimeoutSeconds, AppConfig.MaxHeartbeatUrlTimeoutSeconds, Strings.Msg_InvalidHeartbeatUrlTimeout);

            // Health & Recovery
            CheckRange(dto.HeartbeatInterval, AppConfig.MinHeartbeatInterval, AppConfig.MaxHeartbeatInterval, Strings.Msg_InvalidHeartbeatInterval);
            CheckRange(dto.MaxFailedChecks, AppConfig.MinMaxFailedChecks, AppConfig.MaxMaxFailedChecks, Strings.Msg_InvalidMaxFailedChecks);
            CheckRange(dto.MaxRestartAttempts, AppConfig.MinMaxRestartAttempts, AppConfig.MaxMaxRestartAttempts, Strings.Msg_InvalidMaxRestartAttempts);

            // Heartbeat URL Validation
            if (!string.IsNullOrWhiteSpace(dto.HeartbeatUrl))
            {
                if (!Uri.TryCreate(dto.HeartbeatUrl, UriKind.Absolute, out var validatedUri) ||
                    (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps))
                {
                    result.Errors.Add(Strings.Msg_InvalidHeartbeatUrl);
                }
            }

            // Credentials
            if (
                !importMode
                && (!dto.RunAsLocalSystem.HasValue || !dto.RunAsLocalSystem.Value)
                )
            {
                try
                {
                    if (confirmPassword != null && !string.Equals(dto.Password ?? "", confirmPassword, StringComparison.Ordinal))
                        result.Errors.Add(Strings.Msg_PasswordsDontMatch);
                    else
                        NativeMethodsHelpers.ValidateCredentials(dto.UserAccount, dto.Password);
                }
                catch (Exception ex)
                {
                    Logger.Error("Credential validation failed", ex);
                    result.Errors.Add(ex.Message);
                }
            }

            // Environment & Dependencies
            if (!EnvironmentVariablesValidator.Validate(dto.EnvironmentVariables, out var envErrors))
                result.Errors.AddRange(envErrors);
            if (!ServiceDependenciesValidator.Validate(dto.ServiceDependencies, out var depsErrors))
                result.Errors.AddRange(depsErrors);

            // Pre-Launch Environment & Destination Output Paths
            if (!EnvironmentVariablesValidator.Validate(dto.PreLaunchEnvironmentVariables, out var preLaunchEnvErrors))
                result.Errors.AddRange(preLaunchEnvErrors);
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStdoutPath) && !Helper.IsValidPath(dto.PreLaunchStdoutPath))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchStdoutPath);
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStderrPath) && !Helper.IsValidPath(dto.PreLaunchStderrPath))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchStderrPath);
            CheckRange(dto.PreLaunchTimeoutSeconds, AppConfig.MinPreLaunchTimeoutSeconds, AppConfig.MaxPreLaunchTimeoutSeconds, Strings.Msg_InvalidPreLaunchTimeout);
            CheckRange(dto.PreLaunchRetryAttempts, AppConfig.MinPreLaunchRetryAttempts, AppConfig.MaxPreLaunchRetryAttempts, Strings.Msg_InvalidPreLaunchRetryAttempts);

            // Pre-Stop Timeout
            CheckRange(dto.PreStopTimeoutSeconds, AppConfig.MinPreStopTimeoutSeconds, AppConfig.MaxPreStopTimeoutSeconds, Strings.Msg_InvalidPreStopTimeout);

            return result;
        }
    }
}
