using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.Enums;
using Servy.Core.Interfaces;
using System;
using System.IO;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to install a Windows service with the specified options.
    /// </summary>
    public class InstallServiceCommand : BaseCommand
    {
        private readonly IServiceManager _serviceManager;
        private readonly IServiceInstallValidator _validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceManager">Service manager to perform service operations.</param>
        /// <param name="validator">Validator for installation options.</param>
        public InstallServiceCommand(IServiceManager serviceManager, IServiceInstallValidator validator)
        {
            _serviceManager = serviceManager;
            _validator = validator;
        }

        /// <summary>
        /// Executes the installation of the service with the given options.
        /// </summary>
        /// <param name="opts">Installation options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public CommandResult Execute(InstallServiceOptions opts)
        {
            return ExecuteWithHandling(() =>
            {
                // Validate options
                var validation = _validator.Validate(opts);
                if (!validation.Success)
                    return validation;

                // Ensure wrapper executable exists
                var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Program.ServyServiceExeFileName}.exe");
                if (!File.Exists(wrapperExePath))
                    return CommandResult.Fail("Wrapper executable not found.");

                // Parse enums safely from strings, with defaults
                var startupType = ParseEnumOption(opts.ServiceStartType, ServiceStartType.Automatic);
                var processPriority = ParseEnumOption(opts.ProcessPriority, ProcessPriority.Normal);
                var recoveryAction = ParseEnumOption(opts.RecoveryAction, RecoveryAction.RestartService);

                // Parse numeric string options with fallback
                int rotationSize = int.TryParse(opts.RotationSize, out var rot) ? rot : 0;
                int heartbeatInterval = int.TryParse(opts.HeartbeatInterval, out var hb) ? hb : 0;
                int maxFailedChecks = int.TryParse(opts.MaxFailedChecks, out var failed) ? failed : 0;
                int maxRestartAttempts = int.TryParse(opts.MaxRestartAttempts, out var restart) ? restart : 0;

                // pre-launch
                int preLaunchTimeout = int.TryParse(opts.PreLaunchTimeout, out var plTimeout) ? plTimeout : 30;
                int preLaunchRetryAttempts = int.TryParse(opts.PreLaunchRetryAttempts, out var plRetryAttempts) ? plRetryAttempts : 0;

                // Call the service manager install method
                var success = _serviceManager.InstallService(
                    opts.ServiceName!,
                    opts.ServiceDescription ?? string.Empty,
                    wrapperExePath,
                    opts.ProcessPath ?? string.Empty,
                    opts.StartupDirectory ?? string.Empty,
                    opts.ProcessParameters ?? string.Empty,
                    startupType,
                    processPriority,
                    opts.StdoutPath,
                    opts.StderrPath,
                    rotationSize,
                    heartbeatInterval,
                    maxFailedChecks,
                    recoveryAction,
                    maxRestartAttempts,
                    opts.EnvironmentVariables,
                    opts.ServiceDependencies,
                    opts.User,
                    opts.Password,

                    // Pre-Launch
                    opts.PreLaunchPath,
                    opts.PreLaunchStartupDir,
                    opts.PreLaunchParameters,
                    opts.PreLaunchEnvironmentVariables,
                    opts.PreLaunchStdoutPath,
                    opts.PreLaunchStderrPath,
                    preLaunchTimeout,
                    preLaunchRetryAttempts,
                    opts.PreLaunchIgnoreFailure
                );

                return success
                    ? CommandResult.Ok("Service installed successfully.")
                    : CommandResult.Fail("Failed to install service.");
            });
        }

        /// <summary>
        /// Parses an enum option from string, ignoring case, and returns the default value on failure.
        /// </summary>
        private static T ParseEnumOption<T>(string? option, T defaultValue) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(option)) return defaultValue;
            return Enum.TryParse(option, ignoreCase: true, out T result) ? result : defaultValue;
        }
    }
}
