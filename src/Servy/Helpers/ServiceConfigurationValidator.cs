using Servy.Constants;
using Servy.Core.DTOs;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using Servy.Resources;
using Servy.Services;
using System.IO;
using CoreHelper = Servy.Core.Helpers.Helper;

namespace Servy.Helpers
{
    /// <summary>
    /// Validates all required parameters for service configuration.
    /// Can be reused in InstallService, Export XML/JSON, etc.
    /// </summary>
    public class ServiceConfigurationValidator : IServiceConfigurationValidator
    {
        #region Constants

        private const int MinRotationSize = 1 * 1024 * 1024;       // 1 MB
        private const int MinHeartbeatInterval = 5;                // 5 seconds
        private const int MinMaxFailedChecks = 1;                  // 1 attempt
        private const int MinMaxRestartAttempts = 1;               // 1 attempt
        private const int MinPreLaunchTimeoutSeconds = 5;          // 5 seconds
        private const int MinPreLaunchRetryAttempts = 0;           // 0 attempts

        #endregion

        private readonly IMessageBoxService _messageBoxService;

        public ServiceConfigurationValidator(IMessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
        }


        /// <inheritdoc/>
        public bool Validate(ServiceDto dto, string wrapperExePath = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.ExecutablePath))
            {
                _messageBoxService.ShowWarning(Strings.Msg_ValidationError, AppConstants.Caption);
                return false;
            }

            if (!CoreHelper.IsValidPath(dto.ExecutablePath) || !File.Exists(dto.ExecutablePath))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPath, AppConstants.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(wrapperExePath) && !File.Exists(wrapperExePath))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidWrapperExePath, AppConstants.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.StartupDirectory) &&
                (!CoreHelper.IsValidPath(dto.StartupDirectory) || !Directory.Exists(dto.StartupDirectory)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStartupDirectory, AppConstants.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.StdoutPath) &&
                (!CoreHelper.IsValidPath(dto.StdoutPath) || !CoreHelper.CreateParentDirectory(dto.StdoutPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStdoutPath, AppConstants.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.StderrPath) &&
                (!CoreHelper.IsValidPath(dto.StderrPath) || !CoreHelper.CreateParentDirectory(dto.StderrPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStderrPath, AppConstants.Caption);
                return false;
            }

            if (dto.EnableRotation.HasValue && dto.EnableRotation.Value && dto.RotationSize < MinRotationSize)
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidRotationSize, AppConstants.Caption);
                return false;
            }

            if (dto.EnableHealthMonitoring.HasValue && dto.EnableHealthMonitoring.Value)
            {
                if (dto.HeartbeatInterval < MinHeartbeatInterval)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidHeartbeatInterval, AppConstants.Caption);
                    return false;
                }

                if (dto.MaxFailedChecks < MinMaxFailedChecks)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidMaxFailedChecks, AppConstants.Caption);
                    return false;
                }

                if (dto.MaxRestartAttempts < MinMaxRestartAttempts)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidMaxRestartAttempts, AppConstants.Caption);
                    return false;
                }
            }

            string normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);
            if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out var envErrorMsg))
            {
                _messageBoxService.ShowError(envErrorMsg, AppConstants.Caption);
                return false;
            }

            string normalizedDeps = StringHelper.NormalizeString(dto.ServiceDependencies);
            if (!ServiceDependenciesValidator.Validate(normalizedDeps, out var depsErrors))
            {
                _messageBoxService.ShowError(string.Join("\n", depsErrors), AppConstants.Caption);
                return false;
            }

            if (!dto.RunAsLocalSystem.HasValue && !dto.RunAsLocalSystem.Value)
            {
                try
                {
                    if (!string.Equals(dto.Password, dto.Password, StringComparison.Ordinal))
                    {
                        _messageBoxService.ShowError(Strings.Msg_PasswordsDontMatch, AppConstants.Caption);
                        return false;
                    }

                    NativeMethods.ValidateCredentials(dto.UserAccount, dto.Password);
                }
                catch (Exception ex)
                {
                    _messageBoxService.ShowError(ex.Message, AppConstants.Caption);
                    return false;
                }
            }

            // Pre-launch validation
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchExecutablePath) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchExecutablePath) || !File.Exists(dto.PreLaunchExecutablePath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPreLaunchPath, AppConstants.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStartupDirectory) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchStartupDirectory) || !Directory.Exists(dto.PreLaunchStartupDirectory)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPreLaunchStartupDirectory, AppConstants.Caption);
                return false;
            }

            string normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);
            if (!EnvironmentVariablesValidator.Validate(normalizedPreLaunchEnvVars, out var preLaunchEnvErrorMsg))
            {
                _messageBoxService.ShowError(preLaunchEnvErrorMsg, AppConstants.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStdoutPath) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchStdoutPath) || !CoreHelper.CreateParentDirectory(dto.PreLaunchStdoutPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPreLaunchStdoutPath, AppConstants.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStderrPath) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchStderrPath) || !CoreHelper.CreateParentDirectory(dto.PreLaunchStderrPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPreLaunchStderrPath, AppConstants.Caption);
                return false;
            }

            if (dto.PreLaunchTimeoutSeconds < MinPreLaunchTimeoutSeconds)
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPreLaunchTimeout, AppConstants.Caption);
                return false;
            }

            if (dto.PreLaunchRetryAttempts < MinPreLaunchRetryAttempts)
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPreLaunchRetryAttempts, AppConstants.Caption);
                return false;
            }

            return true;
        }
    }
}
