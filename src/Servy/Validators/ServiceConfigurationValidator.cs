using Servy.Config;
using Servy.Core.DTOs;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using Servy.Resources;
using Servy.UI.Services;
using System.IO;
using CoreHelper = Servy.Core.Helpers.Helper;

namespace Servy.Validators
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

        private readonly IServiceManager _serviceManager;
        private readonly IMessageBoxService _messageBoxService;

        /// <summary>
        /// Creates a new service configuration validator.
        /// </summary>
        /// <param name="serviceManager">Service manager.</param>
        /// <param name="messageBoxService">MessageBox service.</param>
        public ServiceConfigurationValidator(IServiceManager serviceManager, IMessageBoxService messageBoxService)
        {
            _serviceManager = serviceManager;
            _messageBoxService = messageBoxService;
        }

        /// <inheritdoc/>
        public async Task<bool> Validate(ServiceDto dto, string wrapperExePath = null, bool checkServiceStatus = true)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.ExecutablePath))
            {
                await _messageBoxService.ShowWarningAsync(Strings.Msg_ValidationError, AppConfig.Caption);
                return false;
            }

            if (checkServiceStatus)
            {
                var serviceNameExists = _serviceManager.IsServiceInstalled(dto.Name);
                if (serviceNameExists)
                {
                    var startupType = _serviceManager.GetServiceStartupType(dto.Name);

                    if (startupType == Core.Enums.ServiceStartType.Disabled)
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceDisabled, AppConfig.Caption);
                        return false;
                    }
                }
            }

            if (!CoreHelper.IsValidPath(dto.ExecutablePath) || !File.Exists(dto.ExecutablePath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(wrapperExePath) && !File.Exists(wrapperExePath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidWrapperExePath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.StartupDirectory) &&
                (!CoreHelper.IsValidPath(dto.StartupDirectory) || !Directory.Exists(dto.StartupDirectory)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidStartupDirectory, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.StdoutPath) &&
                (!CoreHelper.IsValidPath(dto.StdoutPath) || !CoreHelper.CreateParentDirectory(dto.StdoutPath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidStdoutPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.StderrPath) &&
                (!CoreHelper.IsValidPath(dto.StderrPath) || !CoreHelper.CreateParentDirectory(dto.StderrPath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidStderrPath, AppConfig.Caption);
                return false;
            }

            if (dto.EnableRotation.HasValue && dto.EnableRotation.Value && dto.RotationSize < MinRotationSize)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidRotationSize, AppConfig.Caption);
                return false;
            }

            if (dto.EnableHealthMonitoring.HasValue && dto.EnableHealthMonitoring.Value)
            {
                if (dto.HeartbeatInterval < MinHeartbeatInterval)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidHeartbeatInterval, AppConfig.Caption);
                    return false;
                }

                if (dto.MaxFailedChecks < MinMaxFailedChecks)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidMaxFailedChecks, AppConfig.Caption);
                    return false;
                }

                if (dto.MaxRestartAttempts < MinMaxRestartAttempts)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidMaxRestartAttempts, AppConfig.Caption);
                    return false;
                }
            }

            var normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);
            if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out var envErrorMsg))
            {
                await _messageBoxService.ShowErrorAsync(envErrorMsg, AppConfig.Caption);
                return false;
            }

            var normalizedDeps = StringHelper.NormalizeString(dto.ServiceDependencies);
            if (!ServiceDependenciesValidator.Validate(normalizedDeps, out var depsErrors))
            {
                await _messageBoxService.ShowErrorAsync(string.Join("\n", depsErrors), AppConfig.Caption);
                return false;
            }

            if (!dto.RunAsLocalSystem.HasValue && !dto.RunAsLocalSystem.Value)
            {
                try
                {
                    if (!string.Equals(dto.Password, dto.Password, StringComparison.Ordinal))
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_PasswordsDontMatch, AppConfig.Caption);
                        return false;
                    }

                    NativeMethods.ValidateCredentials(dto.UserAccount, dto.Password);
                }
                catch (Exception ex)
                {
                    await _messageBoxService.ShowErrorAsync(ex.Message, AppConfig.Caption);
                    return false;
                }
            }

            // Pre-launch validation
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchExecutablePath) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchExecutablePath) || !File.Exists(dto.PreLaunchExecutablePath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStartupDirectory) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchStartupDirectory) || !Directory.Exists(dto.PreLaunchStartupDirectory)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchStartupDirectory, AppConfig.Caption);
                return false;
            }

            var normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);
            if (!EnvironmentVariablesValidator.Validate(normalizedPreLaunchEnvVars, out var preLaunchEnvErrorMsg))
            {
                await _messageBoxService.ShowErrorAsync(preLaunchEnvErrorMsg, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStdoutPath) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchStdoutPath) || !CoreHelper.CreateParentDirectory(dto.PreLaunchStdoutPath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchStdoutPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStderrPath) &&
                (!CoreHelper.IsValidPath(dto.PreLaunchStderrPath) || !CoreHelper.CreateParentDirectory(dto.PreLaunchStderrPath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchStderrPath, AppConfig.Caption);
                return false;
            }

            if (dto.PreLaunchTimeoutSeconds < MinPreLaunchTimeoutSeconds)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchTimeout, AppConfig.Caption);
                return false;
            }

            if (dto.PreLaunchRetryAttempts < MinPreLaunchRetryAttempts)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchRetryAttempts, AppConfig.Caption);
                return false;
            }

            return true;
        }
    }
}
