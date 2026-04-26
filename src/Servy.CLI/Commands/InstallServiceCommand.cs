using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.CLI.Validators;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;

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
        public async Task<CommandResult> Execute(Options.InstallServiceOptions opts)
        {
            var action = $"install service '{opts.ServiceName}'";
            var suggestion = "Ensure the executable path is correct, the service name is not already in use, and you are running with Administrator privileges.";

            return await ExecuteWithHandlingAsync("install", action, suggestion, async () =>
            {
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

                // Validate options
                var validation = _validator.Validate(opts);
                if (!validation.Success)
                    return validation;

                // Ensure wrapper executable exists
                var wrapperExePath = AppConfig.GetServyCLIServicePath();

                if (!File.Exists(wrapperExePath))
                    return CommandResult.Fail(Strings.Msg_WrapperNotFound);

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
                int preStopTimeout = int.TryParse(opts.PreStopTimeout, out var psTimeout) ? psTimeout : AppConfig.DefaultPreStopTimeoutSeconds;

                var options = new InstallServiceOptions
                {
                    ServiceName = opts.ServiceName!,
                    Description = opts.ServiceDescription ?? string.Empty,
                    WrapperExePath = wrapperExePath,
                    RealExePath = opts.ProcessPath ?? string.Empty,
                    WorkingDirectory = opts.StartupDirectory ?? string.Empty,
                    RealArgs = opts.ProcessParameters ?? string.Empty,
                    StartType = startupType,
                    ProcessPriority = processPriority,
                    EnableConsoleUI = opts.EnableConsoleUI,
                    StdoutPath = opts.StdoutPath,
                    StderrPath = opts.StderrPath,
                    EnableSizeRotation = opts.EnableRotation || opts.EnableSizeRotation,
                    RotationSizeInBytes = rotationSize,
                    UseLocalTimeForRotation = opts.UseLocalTimeForRotation,
                    EnableHealthMonitoring = opts.EnableHealthMonitoring,
                    HeartbeatInterval = heartbeatInterval,
                    MaxFailedChecks = maxFailedChecks,
                    RecoveryAction = recoveryAction,
                    MaxRestartAttempts = maxRestartAttempts,
                    EnvironmentVariables = opts.EnvironmentVariables,
                    ServiceDependencies = opts.ServiceDependencies,
                    Username = opts.User,

                    // SECURITY FIX: Prioritize command line option (for backward compatibility/testing), 
                    // but fall back to the secure environment variable.
                    Password = !string.IsNullOrEmpty(opts.Password)
                                ? opts.Password
                                : Environment.GetEnvironmentVariable("SERVY_PASSWORD"),

                    // Pre-Launch
                    PreLaunchExePath = opts.PreLaunchPath,
                    PreLaunchWorkingDirectory = opts.PreLaunchStartupDir,
                    PreLaunchArgs = opts.PreLaunchParameters,
                    PreLaunchEnvironmentVariables = opts.PreLaunchEnvironmentVariables,
                    PreLaunchStdoutPath = opts.PreLaunchStdoutPath,
                    PreLaunchStderrPath = opts.PreLaunchStderrPath,
                    PreLaunchTimeout = preLaunchTimeout,
                    PreLaunchRetryAttempts = preLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = opts.PreLaunchIgnoreFailure,

                    // Failure program
                    FailureProgramPath = opts.FailureProgramPath,
                    FailureProgramWorkingDirectory = opts.FailureProgramStartupDir,
                    FailureProgramArgs = opts.FailureProgramParameters,

                    // Post-Launch
                    PostLaunchExePath = opts.PostLaunchPath,
                    PostLaunchWorkingDirectory = opts.PostLaunchStartupDir,
                    PostLaunchArgs = opts.PostLaunchParameters,

                    // Debug Logs
                    EnableDebugLogs = opts.EnableDebugLogs,

                    // Display name
                    DisplayName = opts.ServiceDisplayName,

                    // Max Rotations
                    MaxRotations = maxRotations,

                    // Date rotation
                    EnableDateRotation = opts.EnableDateRotation,
                    DateRotationType = dateRotationType,

                    // Start/Stop timeouts
                    StartTimeout = startTimeout,
                    StopTimeout = stopTimeout,

                    // Pre-Stop
                    PreStopExePath = opts.PreStopPath,
                    PreStopWorkingDirectory = opts.PreStopStartupDir,
                    PreStopArgs = opts.PreStopParameters,
                    PreStopTimeout = preStopTimeout,
                    PreStopLogAsError = opts.PreStopLogAsError,

                    // Post-Stop
                    PostStopExePath = opts.PostStopPath,
                    PostStopWorkingDirectory = opts.PostStopStartupDir,
                    PostStopArgs = opts.PostStopParameters
                };

                // Call the service manager install method
                var res = await _serviceManager.InstallServiceAsync(options);

                if (res.IsSuccess)
                {
                    // Use a localized string that includes the service name for clarity
                    var successMsg = string.Format(Strings.Msg_InstallSuccess, opts.ServiceName);

                    Logger.Info(successMsg);
                    return CommandResult.Ok(successMsg);
                }
                else
                {
                    Logger.Error(res.ErrorMessage);
                    return res.ToFailure();
                }
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
