using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using Servy.Core.ServiceDependencies;
using System.Reflection;

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

            var paramFieldsNamed = new (string Name, string? Value)[]
            {
                (nameof(dto.Parameters),               dto.Parameters),
                (nameof(dto.PreLaunchParameters),      dto.PreLaunchParameters),
                (nameof(dto.PostLaunchParameters),     dto.PostLaunchParameters),
                (nameof(dto.PreStopParameters),        dto.PreStopParameters),
                (nameof(dto.PostStopParameters),       dto.PostStopParameters),
                (nameof(dto.FailureProgramParameters), dto.FailureProgramParameters),
            };
            foreach (var (name, value) in paramFieldsNamed)
            {
                if (value?.Length > AppConfig.MaxArgumentLength)
                    result.Errors.Add(string.Format(Strings.Msg_ArgumentsLengthReachedForField, name, AppConfig.MaxArgumentLength));
            }

            // CpuAffinity
            if (!AffinityHelper.ValidateAffinity(dto.CpuAffinity, out string? errorMessage) && errorMessage != null)
                result.Errors.Add(errorMessage);

            // Reflected [ServicePath] Validation
            var pathViolations = ServicePathValidator.FindAllViolations(dto, _processHelper.ValidatePath);
            foreach (var pathViolation in pathViolations)
            {
                string? errorString = null;
                if (!string.IsNullOrEmpty(pathViolation.Attribute.ErrorResourceKey))
                {
                    var property = typeof(Strings).GetProperty(
                        pathViolation.Attribute.ErrorResourceKey,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                    errorString = property?.GetValue(null) as string;
                }

                result.Errors.Add(errorString ?? string.Format(Strings.Msg_InvalidPathInConfig, pathViolation.Attribute.Label));
            }

            // External Wrapper Executable Path
            if (!string.IsNullOrWhiteSpace(wrapperExePath) && !_processHelper.ValidatePath(wrapperExePath))
                result.Errors.Add(Strings.Msg_InvalidWrapperExePath);

            // Output Log Destination Paths (not evaluated via [ServicePath] as target files may not exist prior to service creation)
            if (!string.IsNullOrWhiteSpace(dto.StdoutPath) && !Helper.IsValidPath(dto.StdoutPath))
                result.Errors.Add(Strings.Msg_InvalidStdoutPath);
            if (!string.IsNullOrWhiteSpace(dto.StderrPath) && !Helper.IsValidPath(dto.StderrPath))
                result.Errors.Add(Strings.Msg_InvalidStderrPath);

            // Timeouts & Rotation Bounds
            if (dto.StartTimeout.HasValue && (dto.StartTimeout < AppConfig.MinStartTimeout || dto.StartTimeout > AppConfig.MaxStartTimeout))
                result.Errors.Add(string.Format(Strings.Msg_InvalidStartTimeout, AppConfig.MinStartTimeout, AppConfig.MaxStartTimeout));
            if (dto.StopTimeout.HasValue && (dto.StopTimeout < AppConfig.MinStopTimeout || dto.StopTimeout > AppConfig.MaxStopTimeout))
                result.Errors.Add(string.Format(Strings.Msg_InvalidStopTimeout, AppConfig.MinStopTimeout, AppConfig.MaxStopTimeout));
            if (dto.RotationSize.HasValue && (dto.RotationSize < AppConfig.MinRotationSize || dto.RotationSize > AppConfig.MaxRotationSize))
                result.Errors.Add(string.Format(Strings.Msg_InvalidRotationSize, AppConfig.MinRotationSize, AppConfig.MaxRotationSize));
            if (dto.MaxRotations.HasValue && (dto.MaxRotations < AppConfig.MinMaxRotations || dto.MaxRotations > AppConfig.MaxMaxRotations))
                result.Errors.Add(string.Format(Strings.Msg_InvalidMaxRotations, AppConfig.MinMaxRotations, AppConfig.MaxMaxRotations));
            if (dto.HeartbeatUrlTimeoutSeconds.HasValue && (dto.HeartbeatUrlTimeoutSeconds < AppConfig.MinHeartbeatUrlTimeoutSeconds || dto.HeartbeatUrlTimeoutSeconds > AppConfig.MaxHeartbeatUrlTimeoutSeconds))
                result.Errors.Add(string.Format(Strings.Msg_InvalidHeartbeatUrlTimeout, AppConfig.MinHeartbeatUrlTimeoutSeconds, AppConfig.MaxHeartbeatUrlTimeoutSeconds));

            // Health & Recovery
            if (dto.HeartbeatInterval.HasValue && (dto.HeartbeatInterval < AppConfig.MinHeartbeatInterval || dto.HeartbeatInterval > AppConfig.MaxHeartbeatInterval))
                result.Errors.Add(string.Format(Strings.Msg_InvalidHeartbeatInterval, AppConfig.MinHeartbeatInterval, AppConfig.MaxHeartbeatInterval));
            if (dto.MaxFailedChecks.HasValue && (dto.MaxFailedChecks < AppConfig.MinMaxFailedChecks || dto.MaxFailedChecks > AppConfig.MaxMaxFailedChecks))
                result.Errors.Add(string.Format(Strings.Msg_InvalidMaxFailedChecks, AppConfig.MinMaxFailedChecks, AppConfig.MaxMaxFailedChecks));
            if (dto.MaxRestartAttempts.HasValue && (dto.MaxRestartAttempts < AppConfig.MinMaxRestartAttempts || dto.MaxRestartAttempts > AppConfig.MaxMaxRestartAttempts))
                result.Errors.Add(string.Format(Strings.Msg_InvalidMaxRestartAttempts, AppConfig.MinMaxRestartAttempts, AppConfig.MaxMaxRestartAttempts));

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
            if (!EnvironmentVariablesValidator.Validate(StringHelper.NormalizeString(dto.EnvironmentVariables), out var envErrors))
                result.Errors.AddRange(envErrors);
            if (!ServiceDependenciesValidator.Validate(StringHelper.NormalizeString(dto.ServiceDependencies), out var depsErrors))
                result.Errors.AddRange(depsErrors);

            // Pre-Launch Environment & Destination Output Paths
            if (!EnvironmentVariablesValidator.Validate(StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables), out var preLaunchEnvErrors))
                result.Errors.AddRange(preLaunchEnvErrors);
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStdoutPath) && !Helper.IsValidPath(dto.PreLaunchStdoutPath))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchStdoutPath);
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStderrPath) && !Helper.IsValidPath(dto.PreLaunchStderrPath))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchStderrPath);
            if (dto.PreLaunchTimeoutSeconds.HasValue && (dto.PreLaunchTimeoutSeconds < AppConfig.MinPreLaunchTimeoutSeconds || dto.PreLaunchTimeoutSeconds > AppConfig.MaxPreLaunchTimeoutSeconds))
                result.Errors.Add(string.Format(Strings.Msg_InvalidPreLaunchTimeout, AppConfig.MinPreLaunchTimeoutSeconds, AppConfig.MaxPreLaunchTimeoutSeconds));
            if (dto.PreLaunchRetryAttempts.HasValue && (dto.PreLaunchRetryAttempts < AppConfig.MinPreLaunchRetryAttempts || dto.PreLaunchRetryAttempts > AppConfig.MaxPreLaunchRetryAttempts))
                result.Errors.Add(string.Format(Strings.Msg_InvalidPreLaunchRetryAttempts, AppConfig.MinPreLaunchRetryAttempts, AppConfig.MaxPreLaunchRetryAttempts));

            // Pre-Stop Timeout
            if (dto.PreStopTimeoutSeconds.HasValue && (dto.PreStopTimeoutSeconds < AppConfig.MinPreStopTimeoutSeconds || dto.PreStopTimeoutSeconds > AppConfig.MaxPreStopTimeoutSeconds))
                result.Errors.Add(string.Format(Strings.Msg_InvalidPreStopTimeout, AppConfig.MinPreStopTimeoutSeconds, AppConfig.MaxPreStopTimeoutSeconds));

            return result;
        }
    }
}