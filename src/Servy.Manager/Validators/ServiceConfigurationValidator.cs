using Servy.Core.DTOs;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using Servy.Manager.Config;
using Servy.Manager.Resources;
using Servy.UI.Services;
using System.IO;
using CoreHelper = Servy.Core.Helpers.Helper;

namespace Servy.Manager.Helpers
{
    /// <summary>
    /// Validates all required parameters for service configuration.
    /// Can be reused in InstallService, Export XML/JSON, etc.
    /// </summary>
    public class ServiceConfigurationValidator : IServiceConfigurationValidator
    {

        private readonly IMessageBoxService _messageBoxService;

        /// <summary>
        /// Creates a new service configuration validator.
        /// </summary>
        /// <param name="messageBoxService">MessageBox service.</param>
        public ServiceConfigurationValidator(IMessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
        }

        /// <inheritdoc/>
        public async Task<bool> Validate(ServiceDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.ExecutablePath))
            {
                await _messageBoxService.ShowWarningAsync(Strings.Msg_ValidationError, AppConfig.Caption);
                return false;
            }

            if (!ProcessHelper.ValidatePath(dto.ExecutablePath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.StartupDirectory) && !ProcessHelper.ValidatePath(dto.StartupDirectory, false))
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

            if (dto.StartTimeout.HasValue && dto.StartTimeout < Core.Config.AppConfig.MinStartTimeout)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidStartTimeout, AppConfig.Caption);
                return false;
            }

            if (dto.StopTimeout.HasValue && dto.StopTimeout < Core.Config.AppConfig.MinStopTimeout)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidStopTimeout, AppConfig.Caption);
                return false;
            }

            if (dto.EnableRotation.HasValue && dto.EnableRotation.Value && dto.RotationSize < Core.Config.AppConfig.MinRotationSize)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidRotationSize, AppConfig.Caption);
                return false;
            }

            if (dto.MaxRotations.HasValue && dto.MaxRotations < 0)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidMaxRotations, AppConfig.Caption);
                return false;
            }

            if (dto.EnableHealthMonitoring.HasValue && dto.EnableHealthMonitoring.Value)
            {
                if (dto.HeartbeatInterval < Core.Config.AppConfig.MinHeartbeatInterval)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidHeartbeatInterval, AppConfig.Caption);
                    return false;
                }

                if (dto.MaxFailedChecks < Core.Config.AppConfig.MinMaxFailedChecks)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidMaxFailedChecks, AppConfig.Caption);
                    return false;
                }

                if (dto.MaxRestartAttempts < Core.Config.AppConfig.MinMaxRestartAttempts)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidMaxRestartAttempts, AppConfig.Caption);
                    return false;
                }
            }

            // Failure Program
            if (!string.IsNullOrWhiteSpace(dto.FailureProgramPath) && (!ProcessHelper.ValidatePath(dto.FailureProgramPath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidFailureProgramPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.FailureProgramStartupDirectory) &&!ProcessHelper.ValidatePath(dto.FailureProgramStartupDirectory, false))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidFailureProgramStartupDirectory, AppConfig.Caption);
                return false;
            }

            if (!dto.RunAsLocalSystem.HasValue || !dto.RunAsLocalSystem.Value)
            {
                try
                {
                    NativeMethods.ValidateCredentials(dto.UserAccount, dto.Password);
                }
                catch (Exception ex)
                {
                    await _messageBoxService.ShowErrorAsync(ex.Message, AppConfig.Caption);
                    return false;
                }
            }

            string normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);
            if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out var envErrorMsg))
            {
                await _messageBoxService.ShowErrorAsync(envErrorMsg, AppConfig.Caption);
                return false;
            }

            string normalizedDeps = StringHelper.NormalizeString(dto.ServiceDependencies);
            if (!ServiceDependenciesValidator.Validate(normalizedDeps, out var depsErrors))
            {
                await _messageBoxService.ShowErrorAsync(string.Join("\n", depsErrors), AppConfig.Caption);
                return false;
            }

            // Pre-launch validation
            if (!string.IsNullOrWhiteSpace(dto.PreLaunchExecutablePath) &&
                (!ProcessHelper.ValidatePath(dto.PreLaunchExecutablePath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreLaunchStartupDirectory) && !ProcessHelper.ValidatePath(dto.PreLaunchStartupDirectory, false))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchStartupDirectory, AppConfig.Caption);
                return false;
            }

            string normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);
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

            if (dto.PreLaunchTimeoutSeconds < Core.Config.AppConfig.MinPreLaunchTimeoutSeconds)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchTimeout, AppConfig.Caption);
                return false;
            }

            if (dto.PreLaunchRetryAttempts < Core.Config.AppConfig.MinPreLaunchRetryAttempts)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreLaunchRetryAttempts, AppConfig.Caption);
                return false;
            }

            // Post-launch validation
            if (!string.IsNullOrWhiteSpace(dto.PostLaunchExecutablePath) &&
                (!ProcessHelper.ValidatePath(dto.PostLaunchExecutablePath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPostLaunchPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PostLaunchStartupDirectory) && !ProcessHelper.ValidatePath(dto.PostLaunchStartupDirectory, false))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPostLaunchStartupDirectory, AppConfig.Caption);
                return false;
            }

            // Pre-stop validation
            if (!string.IsNullOrWhiteSpace(dto.PreStopExecutablePath) &&
                (!ProcessHelper.ValidatePath(dto.PreStopExecutablePath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreStopPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PreStopStartupDirectory) && !ProcessHelper.ValidatePath(dto.PreStopStartupDirectory, false))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreStopStartupDirectory, AppConfig.Caption);
                return false;
            }

            if (dto.PreStopTimeoutSeconds < Core.Config.AppConfig.MinPreStopTimeoutSeconds)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPreStopTimeout, AppConfig.Caption);
                return false;
            }

            // Post-stop validation
            if (!string.IsNullOrWhiteSpace(dto.PostStopExecutablePath) &&
                (!ProcessHelper.ValidatePath(dto.PostStopExecutablePath)))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPostStopPath, AppConfig.Caption);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.PostStopStartupDirectory) && !ProcessHelper.ValidatePath(dto.PostStopStartupDirectory, false))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidPostStopStartupDirectory, AppConfig.Caption);
                return false;
            }

            return true;
        }
    }
}
