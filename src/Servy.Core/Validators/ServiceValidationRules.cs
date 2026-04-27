using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using Servy.Core.ServiceDependencies;

namespace Servy.Core.Validators
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

        /// <inheritdoc/>
        public ValidationResult Validate(ServiceDto? dto, string? wrapperExePath = null, string? confirmPassword = "")
        {
            var result = new ValidationResult();

            // Basic Requirements
            if (dto == null)
            {
                result.Errors.Add(Strings.Msg_ValidationError);
                return result; // Stop early for missing vital fields
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.ExecutablePath))
            {
                result.Errors.Add(Strings.Msg_ValidationError);
                return result;
            }

            if (dto.Name.IndexOfAny(new[] { '\\', '/' }) >= 0)
            {
                result.Errors.Add(Strings.Msg_InvalidServiceName);
                return result;
            }

            // Length Bounds
            if (dto.Name.Length > AppConfig.MaxServiceNameLength)
                result.Warnings.Add(string.Format(Strings.Msg_ServiceNameLengthReached, AppConfig.MaxServiceNameLength));
            if (dto.DisplayName?.Length > AppConfig.MaxDisplayNameLength)
                result.Warnings.Add(string.Format(Strings.Msg_DisplayNameLengthReached, AppConfig.MaxDisplayNameLength));
            if (dto.Description?.Length > AppConfig.MaxDescriptionLength)
                result.Warnings.Add(string.Format(Strings.Msg_DescriptionLengthReached, AppConfig.MaxDescriptionLength));

            var paramFields = new[] { dto.Parameters, dto.PreLaunchParameters, dto.PostLaunchParameters, dto.PreStopParameters, dto.PostStopParameters, dto.FailureProgramParameters };
            if (paramFields.Any(p => p?.Length > AppConfig.MaxArgumentLength))
                result.Warnings.Add(string.Format(Strings.Msg_ArgumentsLengthReached, AppConfig.MaxArgumentLength));

            // Paths
            if (!_processHelper.ValidatePath(dto.ExecutablePath))
                result.Errors.Add(Strings.Msg_InvalidPath);
            if (!string.IsNullOrWhiteSpace(wrapperExePath) && !File.Exists(wrapperExePath))
                result.Errors.Add(Strings.Msg_InvalidWrapperExePath);
            if (!string.IsNullOrWhiteSpace(dto.StartupDirectory) && !_processHelper.ValidatePath(dto.StartupDirectory, false))
                result.Errors.Add(Strings.Msg_InvalidStartupDirectory);
            if (!string.IsNullOrWhiteSpace(dto.StdoutPath) && (!Helper.IsValidPath(dto.StdoutPath) || !Helper.CreateParentDirectory(dto.StdoutPath)))
                result.Errors.Add(Strings.Msg_InvalidStdoutPath);
            if (!string.IsNullOrWhiteSpace(dto.StderrPath) && (!Helper.IsValidPath(dto.StderrPath) || !Helper.CreateParentDirectory(dto.StderrPath)))
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

            // Health & Recovery
            if (dto.HeartbeatInterval.HasValue && (dto.HeartbeatInterval < AppConfig.MinHeartbeatInterval || dto.HeartbeatInterval > AppConfig.MaxHeartbeatInterval))
                result.Errors.Add(string.Format(Strings.Msg_InvalidHeartbeatInterval, AppConfig.MinHeartbeatInterval, AppConfig.MaxHeartbeatInterval));
            if (dto.MaxFailedChecks.HasValue && (dto.MaxFailedChecks < AppConfig.MinMaxFailedChecks || dto.MaxFailedChecks > AppConfig.MaxMaxFailedChecks))
                result.Errors.Add(string.Format(Strings.Msg_InvalidMaxFailedChecks, AppConfig.MinMaxFailedChecks, AppConfig.MaxMaxFailedChecks));
            if (dto.MaxRestartAttempts.HasValue && (dto.MaxRestartAttempts < AppConfig.MinMaxRestartAttempts || dto.MaxRestartAttempts > AppConfig.MaxMaxRestartAttempts))
                result.Errors.Add(string.Format(Strings.Msg_InvalidMaxRestartAttempts, AppConfig.MinMaxRestartAttempts, AppConfig.MaxMaxRestartAttempts));

            // Failure Program
            if (!string.IsNullOrWhiteSpace(dto.FailureProgramPath) && !_processHelper.ValidatePath(dto.FailureProgramPath))
                result.Errors.Add(Strings.Msg_InvalidFailureProgramPath);
            if (!string.IsNullOrWhiteSpace(dto.FailureProgramStartupDirectory) && !_processHelper.ValidatePath(dto.FailureProgramStartupDirectory, false))
                result.Errors.Add(Strings.Msg_InvalidFailureProgramStartupDirectory);

            // Credentials
            if ((!dto.RunAsLocalSystem.HasValue || !dto.RunAsLocalSystem.Value) && !string.IsNullOrWhiteSpace(dto.UserAccount))
            {
                try
                {
                    if (!string.IsNullOrEmpty(confirmPassword) && !string.Equals(dto.Password ?? "", confirmPassword, StringComparison.Ordinal))
                        result.Errors.Add(Strings.Msg_PasswordsDontMatch);
                    else
                        NativeMethods.ValidateCredentials(dto.UserAccount, dto.Password);
                }
                catch (Exception ex)
                {
                    Logger.Error("Credential validation failed", ex);
                    result.Errors.Add(ex.Message);
                }
            }

            // Environment & Dependencies
            if (!EnvironmentVariablesValidator.Validate(StringHelper.NormalizeString(dto.EnvironmentVariables), out var envErrorMsg))
                result.Errors.Add(envErrorMsg);
            if (!ServiceDependenciesValidator.Validate(StringHelper.NormalizeString(dto.ServiceDependencies), out var depsErrors))
                result.Errors.Add(string.Join("\n", depsErrors));

            // Pre-Launch
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchExecutablePath) && !_processHelper.ValidatePath(dto.PreLaunchExecutablePath))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchPath);
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStartupDirectory) && !_processHelper.ValidatePath(dto.PreLaunchStartupDirectory, false))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchStartupDirectory);
            if (!EnvironmentVariablesValidator.Validate(StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables), out var preLaunchEnvErrorMsg))
                result.Errors.Add(preLaunchEnvErrorMsg);
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStdoutPath) && (!Helper.IsValidPath(dto.PreLaunchStdoutPath) || !Helper.CreateParentDirectory(dto.PreLaunchStdoutPath)))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchStdoutPath);
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStderrPath) && (!Helper.IsValidPath(dto.PreLaunchStderrPath) || !Helper.CreateParentDirectory(dto.PreLaunchStderrPath)))
                result.Errors.Add(Strings.Msg_InvalidPreLaunchStderrPath);
            if (dto.PreLaunchTimeoutSeconds.HasValue && (dto.PreLaunchTimeoutSeconds < AppConfig.MinPreLaunchTimeoutSeconds || dto.PreLaunchTimeoutSeconds > AppConfig.MaxPreLaunchTimeoutSeconds))
                result.Errors.Add(string.Format(Strings.Msg_InvalidPreLaunchTimeout, AppConfig.MinPreLaunchTimeoutSeconds, AppConfig.MaxPreLaunchTimeoutSeconds));
            if (dto.PreLaunchRetryAttempts.HasValue && (dto.PreLaunchRetryAttempts < AppConfig.MinPreLaunchRetryAttempts || dto.PreLaunchRetryAttempts > AppConfig.MaxPreLaunchRetryAttempts))
                result.Errors.Add(string.Format(Strings.Msg_InvalidPreLaunchRetryAttempts, AppConfig.MinPreLaunchRetryAttempts, AppConfig.MaxPreLaunchRetryAttempts));

            // Post-Launch
            if (!string.IsNullOrWhiteSpace(dto.PostLaunchExecutablePath) && !_processHelper.ValidatePath(dto.PostLaunchExecutablePath))
                result.Errors.Add(Strings.Msg_InvalidPostLaunchPath);
            if (!string.IsNullOrWhiteSpace(dto.PostLaunchStartupDirectory) && !_processHelper.ValidatePath(dto.PostLaunchStartupDirectory, false))
                result.Errors.Add(Strings.Msg_InvalidPostLaunchStartupDirectory);

            // Pre-Stop
            if (!string.IsNullOrWhiteSpace(dto.PreStopExecutablePath) && !_processHelper.ValidatePath(dto.PreStopExecutablePath))
                result.Errors.Add(Strings.Msg_InvalidPreStopPath);
            if (!string.IsNullOrWhiteSpace(dto.PreStopStartupDirectory) && !_processHelper.ValidatePath(dto.PreStopStartupDirectory, false))
                result.Errors.Add(Strings.Msg_InvalidPreStopStartupDirectory);
            if (dto.PreStopTimeoutSeconds.HasValue && (dto.PreStopTimeoutSeconds < AppConfig.MinPreStopTimeoutSeconds || dto.PreStopTimeoutSeconds > AppConfig.MaxPreStopTimeoutSeconds))
                result.Errors.Add(string.Format(Strings.Msg_InvalidPreStopTimeout, AppConfig.MinPreStopTimeoutSeconds, AppConfig.MaxPreStopTimeoutSeconds));

            // Post-Stop
            if (!string.IsNullOrWhiteSpace(dto.PostStopExecutablePath) && !_processHelper.ValidatePath(dto.PostStopExecutablePath))
                result.Errors.Add(Strings.Msg_InvalidPostStopPath);
            if (!string.IsNullOrWhiteSpace(dto.PostStopStartupDirectory) && !_processHelper.ValidatePath(dto.PostStopStartupDirectory, false))
                result.Errors.Add(Strings.Msg_InvalidPostStopStartupDirectory);

            return result;
        }
    }
}