using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Native;
using System;
using System.IO;

namespace Servy.CLI.Validators
{
    /// <summary>
    /// Validates the options for installing a service.
    /// </summary>
    public class ServiceInstallValidator : IServiceInstallValidator
    {
        private const int MinRotationSize = 1; // 1 MB
        private const int MinHeartbeatInterval = 5;
        private const int MinMaxFailedChecks = 1;
        private const int MinMaxRestartAttempts = 1;
        private const int MinPreLaunchTimeoutSeconds = 5;
        private const int MinPreLaunchRetryAttempts = 0;

        /// <summary>
        /// Validates the install service options.
        /// </summary>
        /// <param name="opts">The install service options to validate.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure with a message.</returns>
        public CommandResult Validate(InstallServiceOptions opts)
        {
            if (string.IsNullOrWhiteSpace(opts.ServiceName) || string.IsNullOrWhiteSpace(opts.ProcessPath))
                return CommandResult.Fail(Strings.Msg_ValidationError);

            //var serviceNameExists = _serviceManager.IsServiceInstalled(opts.ServiceName);
            //if (serviceNameExists)
            //{
            //    var startupType = _serviceManager.GetServiceStartupType(opts.ServiceName);

            //    if (startupType == ServiceStartType.Disabled)
            //    {
            //        return CommandResult.Fail(Strings.Msg_ServiceDisabled);
            //    }
            //}

            if (!Helper.IsValidPath(opts.ProcessPath) || !File.Exists(opts.ProcessPath))
                return CommandResult.Fail(Strings.Msg_InvalidPath);

            if (!string.IsNullOrWhiteSpace(opts.StartupDirectory) && (!Helper.IsValidPath(opts.StartupDirectory) || !Directory.Exists(opts.StartupDirectory)))
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

            if (!string.IsNullOrWhiteSpace(opts.StartTimeout) && (!int.TryParse(opts.StartTimeout, out var startTimeout) || startTimeout < AppConfig.MinStartTimeout))
            {
                return CommandResult.Fail(Strings.Msg_InvalidStartTimeout);
            }

            if (!string.IsNullOrWhiteSpace(opts.StopTimeout) && (!int.TryParse(opts.StopTimeout, out var stopTimeout) || stopTimeout < AppConfig.MinStopTimeout))
            {
                return CommandResult.Fail(Strings.Msg_InvalidStopTimeout);
            }

            if (opts.EnableRotation
                && (!int.TryParse(opts.RotationSize, out var rotation) || rotation < MinRotationSize)
                )
            {
                return CommandResult.Fail(Strings.Msg_InvalidRotationSize);
            }

            if (!ValidateEnumOption<DateRotationType>(opts.DateRotationType))
                return CommandResult.Fail(Strings.Msg_InvalidDateRotationType);

            if (!string.IsNullOrWhiteSpace(opts.MaxRotations) && (!int.TryParse(opts.MaxRotations, out var maxRotations) || maxRotations < 0))
            {
                return CommandResult.Fail(Strings.Msg_InvalidMaxRotations);
            }

            if (opts.EnableHealthMonitoring)
            {
                if (!int.TryParse(opts.HeartbeatInterval, out var hb) || hb < MinHeartbeatInterval)
                    return CommandResult.Fail(Strings.Msg_InvalidHeartbeatInterval);

                if (!int.TryParse(opts.MaxFailedChecks, out var failed) || failed < MinMaxFailedChecks)
                    return CommandResult.Fail(Strings.Msg_InvalidMaxFailedChecks);

                if (!ValidateEnumOption<RecoveryAction>(opts.RecoveryAction))
                    return CommandResult.Fail(Strings.Msg_InvalidRecoveryAction);

                if (!int.TryParse(opts.MaxRestartAttempts, out var restart) || restart < MinMaxRestartAttempts)
                    return CommandResult.Fail(Strings.Msg_InvalidMaxRestartAttempts);
            }

            if (!string.IsNullOrWhiteSpace(opts.FailureProgramPath) && (!Helper.IsValidPath(opts.FailureProgramPath) || !File.Exists(opts.FailureProgramPath)))
                return CommandResult.Fail(Strings.Msg_InvalidFailureProgramPath);

            if (!string.IsNullOrWhiteSpace(opts.FailureProgramStartupDir) && (!Helper.IsValidPath(opts.FailureProgramStartupDir) || !Directory.Exists(opts.FailureProgramStartupDir)))
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
                    return CommandResult.Fail(ex.Message);
                }
            }

            string envVarsErrorMessage;
            if (!EnvironmentVariablesValidator.Validate(opts.EnvironmentVariables, out envVarsErrorMessage))
                return CommandResult.Fail(envVarsErrorMessage);

            // PreLaunch
            if (!string.IsNullOrWhiteSpace(opts.PreLaunchPath) && (!Helper.IsValidPath(opts.PreLaunchPath) || !File.Exists(opts.PreLaunchPath)))
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchPath);

            if (!string.IsNullOrWhiteSpace(opts.PreLaunchStartupDir) && (!Helper.IsValidPath(opts.PreLaunchStartupDir) || !Directory.Exists(opts.PreLaunchStartupDir)))
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

            int preLaunchTimeoutValue = 30;
            if (!string.IsNullOrWhiteSpace(opts.PreLaunchTimeout) && !int.TryParse(opts.PreLaunchTimeout, out preLaunchTimeoutValue) || preLaunchTimeoutValue < MinPreLaunchTimeoutSeconds)
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchTimeout);
            }

            int preLaunchRetryAttemptsValue = 0;
            if (!string.IsNullOrWhiteSpace(opts.PreLaunchRetryAttempts) && !int.TryParse(opts.PreLaunchRetryAttempts, out preLaunchRetryAttemptsValue) || preLaunchRetryAttemptsValue < MinPreLaunchRetryAttempts)
            {
                return CommandResult.Fail(Strings.Msg_InvalidPreLaunchRetryAttempts);
            }

            // Post-Launch
            if (!string.IsNullOrWhiteSpace(opts.PostLaunchPath) && (!Helper.IsValidPath(opts.PostLaunchPath) || !File.Exists(opts.PostLaunchPath)))
                return CommandResult.Fail(Strings.Msg_InvalidPostLaunchPath);

            if (!string.IsNullOrWhiteSpace(opts.PostLaunchStartupDir) && (!Helper.IsValidPath(opts.PostLaunchStartupDir) || !Directory.Exists(opts.PostLaunchStartupDir)))
            {
                return CommandResult.Fail(Strings.Msg_InvalidPostLaunchStartupDirectory);
            }

            return CommandResult.Ok("Validation passed.");
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
