using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using System;
using System.Linq;

namespace Servy.CLI.Validators
{
    /// <summary>
    /// Validates the options for installing a service.
    /// </summary>
    public class ServiceInstallValidator : IServiceInstallValidator
    {
        ///<inheritdoc/>
        public CommandResult Validate(InstallServiceOptions opts)
        {
            if (string.IsNullOrWhiteSpace(opts.ServiceName) || string.IsNullOrWhiteSpace(opts.ProcessPath))
                return CommandResult.Fail(Strings.Msg_ValidationError);

            if (opts.ServiceName.Length > AppConfig.MaxServiceNameLength)
            {
                return CommandResult.Fail(string.Format(Strings.Msg_ServiceNameLengthReached, AppConfig.MaxServiceNameLength));
            }

            if (!ProcessHelper.ValidatePath(opts.ProcessPath))
                return CommandResult.Fail(Strings.Msg_InvalidPath);

            if (opts.ServiceDisplayName?.Length > AppConfig.MaxDisplayNameLength)
            {
                return CommandResult.Fail(string.Format(Strings.Msg_DisplayNameLengthReached, AppConfig.MaxDisplayNameLength));
            }

            if (opts.ServiceDescription?.Length > AppConfig.MaxDescriptionLength)
            {
                return CommandResult.Fail(string.Format(Strings.Msg_DescriptionLengthReached, AppConfig.MaxDescriptionLength));
            }

            // Validate all 6 parameter fields
            var paramFields = new[]
            {
                opts.ProcessParameters,
                opts.PreLaunchParameters,
                opts.PostLaunchParameters,
                opts.PreStopParameters,
                opts.PostStopParameters,
                opts.FailureProgramParameters
            };

            if (paramFields.Any(p => p?.Length > AppConfig.MaxArgumentLength))
            {
                return CommandResult.Fail(string.Format(Strings.Msg_ArgumentsLengthReached, AppConfig.MaxArgumentLength));
            }

            if (!string.IsNullOrWhiteSpace(opts.StartupDirectory) && !ProcessHelper.ValidatePath(opts.StartupDirectory, false))
            {
                return CommandResult.Fail(Strings.Msg_InvalidStartupDirectory);
            }

            if (!string.IsNullOrWhiteSpace(opts.StdoutPath) && (!Helper.IsValidPath(opts.StdoutPath) || !Helper.CreateParentDirectory(opts.StdoutPath)))
            {
                return CommandResult.Fail(Strings.Msg_InvalidStdoutPath);
            }

            if (!string.IsNullOrWhiteSpace(opts.StderrPath) && (!Helper.IsValidPath(opts.StderrPath) || !Helper.CreateParentDirectory(opts.StderrPath)))
            {
                return CommandResult.Fail(Strings.Msg_InvalidStderrPath);
            }

            if (!ValidateEnumOption<ServiceStartType>(opts.ServiceStartType))
                return CommandResult.Fail(Strings.Msg_InvalidStartupType);

            if (!ValidateEnumOption<ProcessPriority>(opts.ProcessPriority))
                return CommandResult.Fail(Strings.Msg_InvalidProcessPriority);

            if (!string.IsNullOrWhiteSpace(opts.StartTimeout) && (!int.TryParse(opts.StartTimeout, out var startTimeout) || startTimeout < AppConfig.MinStartTimeout || startTimeout > AppConfig.MaxStartTimeout))
            {
                return CommandResult.Fail(Strings.Msg_InvalidStartTimeout);
            }

            if (!string.IsNullOrWhiteSpace(opts.StopTimeout) && (!int.TryParse(opts.StopTimeout, out var stopTimeout) || stopTimeout < AppConfig.MinStopTimeout || stopTimeout > AppConfig.MaxStopTimeout))
            {
                return CommandResult.Fail(Strings.Msg_InvalidStopTimeout);
            }

            if ((opts.EnableRotation || opts.EnableSizeRotation)
                && !string.IsNullOrWhiteSpace(opts.RotationSize)
                && (!int.TryParse(opts.RotationSize, out var rotation) || rotation < AppConfig.MinRotationSize || rotation > AppConfig.MaxRotationSize))
            {
                return CommandResult.Fail(Strings.Msg_InvalidRotationSize);
            }

            if (!ValidateEnumOption<DateRotationType>(opts.DateRotationType))
                return CommandResult.Fail(Strings.Msg_InvalidDateRotationType);

            if (!string.IsNullOrWhiteSpace(opts.MaxRotations) && (!int.TryParse(opts.MaxRotations, out var maxRotations) || maxRotations < 0 || maxRotations > AppConfig.MaxMaxRotations))
            {
                return CommandResult.Fail(Strings.Msg_InvalidMaxRotations);
            }

            if (opts.EnableHealthMonitoring)
            {
                if (!int.TryParse(opts.HeartbeatInterval, out var hb) || hb < AppConfig.MinHeartbeatInterval || hb > AppConfig.MaxHeartbeatInterval)
                    return CommandResult.Fail(Strings.Msg_InvalidHeartbeatInterval);

                if (!int.TryParse(opts.MaxFailedChecks, out var failed) || failed < AppConfig.MinMaxFailedChecks || failed > AppConfig.MaxMaxFailedChecks)
                    return CommandResult.Fail(Strings.Msg_InvalidMaxFailedChecks);

                if (!ValidateEnumOption<RecoveryAction>(opts.RecoveryAction))
                    return CommandResult.Fail(Strings.Msg_InvalidRecoveryAction);

                if (!string.IsNullOrWhiteSpace(opts.MaxRestartAttempts)
                   && (!int.TryParse(opts.MaxRestartAttempts, out var restart) || restart < AppConfig.MinMaxRestartAttempts || restart > AppConfig.MaxMaxRestartAttempts))
                    return CommandResult.Fail(Strings.Msg_InvalidMaxRestartAttempts);
            }

            if (!string.IsNullOrWhiteSpace(opts.FailureProgramPath) && (!ProcessHelper.ValidatePath(opts.FailureProgramPath)))
                return CommandResult.Fail(Strings.Msg_InvalidFailureProgramPath);

            if (!string.IsNullOrWhiteSpace(opts.FailureProgramStartupDir) && !ProcessHelper.ValidatePath(opts.FailureProgramStartupDir, false))
            {
                return CommandResult.Fail(Strings.Msg_InvalidFailureProgramStartupDirectory);
            }

            if (!string.IsNullOrWhiteSpace(opts.User))
            {
                try
                {
                    NativeMethods.ValidateCredentials(opts.User, opts.Password);
                }
                catch (Exception ex)
                {
                    Logger.Error("Credential validation failed.", ex);
                    return CommandResult.Fail(ex.Message);
                }
            }

            string envVarsErrorMessage;
            if (!EnvironmentVariablesValidator.Validate(opts.EnvironmentVariables, out envVarsErrorMessage))
                return CommandResult.Fail(envVarsErrorMessage);

            // PreLaunch
            if (!string.IsNullOrWhiteSpace(opts.PreLaunchPath) && (!ProcessHelper.ValidatePath(opts.PreLaunchPath)))
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchPath);

            if (!string.IsNullOrWhiteSpace(opts.PreLaunchStartupDir) && !ProcessHelper.ValidatePath(opts.PreLaunchStartupDir, false))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchStartupDirectory);
            }

            string preLaunchEnvVarsErrorMessage;
            if (!EnvironmentVariablesValidator.Validate(opts.PreLaunchEnvironmentVariables, out preLaunchEnvVarsErrorMessage))
                return CommandResult.Fail(preLaunchEnvVarsErrorMessage);

            if (!string.IsNullOrWhiteSpace(opts.PreLaunchStdoutPath) && (!Helper.IsValidPath(opts.PreLaunchStdoutPath) || !Helper.CreateParentDirectory(opts.PreLaunchStdoutPath)))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchStdoutPath);
            }

            if (!string.IsNullOrWhiteSpace(opts.PreLaunchStderrPath) && (!Helper.IsValidPath(opts.PreLaunchStderrPath) || !Helper.CreateParentDirectory(opts.PreLaunchStderrPath)))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchStderrPath);
            }

            if (!string.IsNullOrWhiteSpace(opts.PreLaunchTimeout) && (!int.TryParse(opts.PreLaunchTimeout, out int preLaunchTimeoutValue) || preLaunchTimeoutValue < AppConfig.MinPreLaunchTimeoutSeconds || preLaunchTimeoutValue > AppConfig.MaxPreLaunchTimeoutSeconds))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchTimeout);
            }

            if (!string.IsNullOrWhiteSpace(opts.PreLaunchRetryAttempts) && (!int.TryParse(opts.PreLaunchRetryAttempts, out int preLaunchRetryAttemptsValue) || preLaunchRetryAttemptsValue < AppConfig.MinPreLaunchRetryAttempts || preLaunchRetryAttemptsValue > AppConfig.MaxPreLaunchRetryAttempts))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchRetryAttempts);
            }

            // Post-Launch
            if (!string.IsNullOrWhiteSpace(opts.PostLaunchPath) && (!ProcessHelper.ValidatePath(opts.PostLaunchPath)))
                return CommandResult.Fail(Strings.Msg_InvalidPostLaunchPath);

            if (!string.IsNullOrWhiteSpace(opts.PostLaunchStartupDir) && !ProcessHelper.ValidatePath(opts.PostLaunchStartupDir, false))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPostLaunchStartupDirectory);
            }

            // Pre-Stop
            if (!string.IsNullOrWhiteSpace(opts.PreStopPath) && (!ProcessHelper.ValidatePath(opts.PreStopPath)))
                return CommandResult.Fail(Strings.Msg_InvalidPreStopPath);

            if (!string.IsNullOrWhiteSpace(opts.PreStopStartupDir) && !ProcessHelper.ValidatePath(opts.PreStopStartupDir, false))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreStopStartupDirectory);
            }

            if (!string.IsNullOrWhiteSpace(opts.PreStopTimeout) && (!int.TryParse(opts.PreStopTimeout, out int preStopTimeoutValue) || preStopTimeoutValue < AppConfig.MinPreStopTimeoutSeconds || preStopTimeoutValue > AppConfig.MaxPreStopTimeoutSeconds))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreStopTimeout);
            }

            // Post-Stop
            if (!string.IsNullOrWhiteSpace(opts.PostStopPath) && (!ProcessHelper.ValidatePath(opts.PostStopPath)))
                return CommandResult.Fail(Strings.Msg_InvalidPostStopPath);

            if (!string.IsNullOrWhiteSpace(opts.PostStopStartupDir) && !ProcessHelper.ValidatePath(opts.PostStopStartupDir, false))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPostStopStartupDirectory);
            }

            // Use the localized resource with the service name for clear confirmation
            var successMsg = string.Format(Strings.Msg_ValidationPassed, opts.ServiceName);

            Logger.Info(successMsg);
            return CommandResult.Ok(successMsg);
        }

        /// <summary>
        /// Validates whether a string option represents a valid value of the enum type <typeparamref name="T"/>.
        /// Null or whitespace values are considered valid.
        /// </summary>
        /// <typeparam name="T">The enum type to validate against.</typeparam>
        /// <param name="option">The string option value.</param>
        /// <returns>True if the option is null/empty or a valid enum value; otherwise, false.</returns>
        private static bool ValidateEnumOption<T>(string option) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(option))
                return true;

            return Enum.TryParse<T>(option, true, out _);
        }
    }
}
