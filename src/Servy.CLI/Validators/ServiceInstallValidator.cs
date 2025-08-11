using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;

namespace Servy.CLI.Validators
{
    /// <summary>
    /// Validates the options for installing a service.
    /// </summary>
    public class ServiceInstallValidator : IServiceInstallValidator
    {
        private const int MinRotationSize = 1 * 1024 * 1024;
        private const int MinHeartbeatInterval = 5;
        private const int MinMaxFailedChecks = 1;
        private const int MinMaxRestartAttempts = 1;

        /// <summary>
        /// Validates the install service options.
        /// </summary>
        /// <param name="opts">The install service options to validate.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure with a message.</returns>
        public CommandResult Validate(InstallServiceOptions opts)
        {
            if (string.IsNullOrWhiteSpace(opts.ServiceName) || string.IsNullOrWhiteSpace(opts.ProcessPath))
                return CommandResult.Fail(Strings.Msg_ValidationError);

            if (!Helper.IsValidPath(opts.ProcessPath) || !File.Exists(opts.ProcessPath))
                return CommandResult.Fail(Strings.Msg_InvalidPath);

            if (!ValidateEnumOption<ServiceStartType>(opts.ServiceStartType))
                return CommandResult.Fail(Strings.Msg_InvalidStartupType);

            if (!ValidateEnumOption<ProcessPriority>(opts.ProcessPriority))
                return CommandResult.Fail(Strings.Msg_InvalidProcessPriority);

            if (opts.EnableRotation)
            {
                if (!int.TryParse(opts.RotationSize, out var rotation) || rotation < MinRotationSize)
                    return CommandResult.Fail(Strings.Msg_InvalidRotationSize);
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

            string envVarsErrorMessage;
            if (!EnvironmentVariablesValidator.Validate(opts.EnvironmentVariables, out envVarsErrorMessage))
                return CommandResult.Fail(envVarsErrorMessage);

            return CommandResult.Ok("Validation passed.");
        }

        /// <summary>
        /// Validates whether a string option represents a valid value of the enum type <typeparamref name="T"/>.
        /// Null or whitespace values are considered valid.
        /// </summary>
        /// <typeparam name="T">The enum type to validate against.</typeparam>
        /// <param name="option">The string option value.</param>
        /// <returns>True if the option is null/empty or a valid enum value; otherwise, false.</returns>
        private static bool ValidateEnumOption<T>(string? option) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(option))
                return true;

            return Enum.TryParse<T>(option, true, out _);
        }
    }
}
