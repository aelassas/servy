using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Services;
using System;
using System.IO;
using System.Threading.Tasks;

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
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceManager"/> or <paramref name="validator"/> is <c>null</c>.
        /// </exception>
        public InstallServiceCommand(IServiceManager serviceManager, IServiceInstallValidator validator)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        /// <summary>
        /// Executes the installation of the service with the given options.
        /// </summary>
        /// <param name="opts">Installation options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        /// <summary>
        /// Executes the installation of a Windows service using the specified options.
        /// </summary>
        /// <param name="opts">Options for service installation.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> Execute(InstallServiceOptions opts)
        {
            return await ExecuteWithHandlingAsync(async () =>
            {
                // Validate options
                var validation = _validator.Validate(opts);
                if (!validation.Success)
                    return validation;

                // Ensure wrapper executable exists
                var wrapperExePath = AppConfig.GetServyCLIServicePath();

                if (!File.Exists(wrapperExePath))
                    return CommandResult.Fail("Wrapper executable not found.");

                // Parse enums safely with defaults
                var startupType = ParseEnumOption(opts.ServiceStartType, ServiceStartType.Automatic);
                var processPriority = ParseEnumOption(opts.ProcessPriority, ProcessPriority.Normal);
                var dateRotationType = ParseEnumOption(opts.DateRotationType, DateRotationType.Daily);
                var recoveryAction = ParseEnumOption(opts.RecoveryAction, RecoveryAction.RestartService);

                // Parse numeric options
                ulong rotationSize = (ulong.TryParse(opts.RotationSize, out var rot) ? rot : (ulong)AppConfig.DefaultRotationSize) * 1024 * 1024;
                int heartbeatInterval = int.TryParse(opts.HeartbeatInterval, out var hb) ? hb : AppConfig.DefaultHeartbeatInterval;
                int maxFailedChecks = int.TryParse(opts.MaxFailedChecks, out var mf) ? mf : AppConfig.DefaultMaxFailedChecks;
                int maxRestartAttempts = int.TryParse(opts.MaxRestartAttempts, out var mr) ? mr : AppConfig.DefaultMaxRestartAttempts;
                int preLaunchTimeout = int.TryParse(opts.PreLaunchTimeout, out var plTimeout) ? plTimeout : AppConfig.DefaultPreLaunchTimeoutSeconds;
                int preLaunchRetryAttempts = int.TryParse(opts.PreLaunchRetryAttempts, out var plRetry) ? plRetry : AppConfig.DefaultPreLaunchRetryAttempts;
                int maxRotations = int.TryParse(opts.MaxRotations, out var maxRot) ? maxRot : AppConfig.DefaultMaxRotations;
                int startTimeout = int.TryParse(opts.StartTimeout, out var stTimeout) ? stTimeout : AppConfig.DefaultStartTimeout;
                int stopTimeout = int.TryParse(opts.StopTimeout, out var spTimeout) ? spTimeout : AppConfig.DefaultStopTimeout;

                // Call the service manager install method
                var success = await _serviceManager.InstallService(
                    opts.ServiceName,
                    opts.ServiceDescription ?? string.Empty,
                    wrapperExePath,
                    opts.ProcessPath ?? string.Empty,
                    opts.StartupDirectory ?? string.Empty,
                    opts.ProcessParameters ?? string.Empty,
                    startupType,
                    processPriority,
                    opts.StdoutPath,
                    opts.StderrPath,
                    opts.EnableRotation || opts.EnableSizeRotation,
                    rotationSize,
                    opts.EnableHealthMonitoring,
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
                    opts.PreLaunchIgnoreFailure,
                    // Failure program
                    failureProgramPath: opts.FailureProgramPath,
                    failureProgramWorkingDirectory: opts.FailureProgramStartupDir,
                    failureProgramArgs: opts.FailureProgramParameters,
                    // Post-Launch
                    postLaunchExePath: opts.PostLaunchPath,
                    postLaunchWorkingDirectory: opts.PostLaunchStartupDir,
                    postLaunchArgs: opts.PostLaunchParameters,
                    // Debug Logs
                    enableDebugLogs: opts.EnableDebugLogs,
                    // Display name
                    displayName: opts.ServiceDisplayName,
                    // Max Rotations
                    maxRotations: maxRotations,
                    // Date rotation
                    enableDateRotation: opts.EnableDateRotation,
                    dateRotationType: dateRotationType,
                    // Start/Stop timeouts
                    startTimeout: startTimeout,
                    stopTimeout: stopTimeout
                );

                if (!success)
                    return CommandResult.Fail("Failed to install service.");

                return CommandResult.Ok("Service installed successfully.");
            });
        }

        /// <summary>
        /// Parses an enum option from string, ignoring case, and returns the default value on failure.
        /// </summary>
        private static T ParseEnumOption<T>(string option, T defaultValue) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(option)) return defaultValue;
            return Enum.TryParse(option, ignoreCase: true, out T result) ? result : defaultValue;
        }
    }
}
