using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.CLI.Validation;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Helpers;
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
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> ExecuteAsync(Options.InstallServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"install service '{opts.ServiceName}'";
            var suggestion = "Ensure the executable path is correct, the service name is not already in use, and you are running with Administrator privileges.";

            return await ExecuteWithHandlingAsync("install", action, suggestion, async () =>
            {
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

                // SECURITY: Read sensitive values from environment variables first, 
                // falling back to command line options to prevent credential leakage.
                opts.Password = GetSecureValue(opts.Password, AppConfig.PasswordEnvVarName);
                opts.ProcessParameters = GetSecureValue(opts.ProcessParameters, AppConfig.ProcessParametersEnvVarName);
                opts.EnvironmentVariables = GetSecureValue(opts.EnvironmentVariables, AppConfig.EnvironmentVariablesEnvVarName);
                opts.FailureProgramParameters = GetSecureValue(opts.FailureProgramParameters, AppConfig.FailureProgramParametersEnvVarName);
                opts.PreLaunchParameters = GetSecureValue(opts.PreLaunchParameters, AppConfig.PreLaunchParametersEnvVarName);
                opts.PreLaunchEnvironmentVariables = GetSecureValue(opts.PreLaunchEnvironmentVariables, AppConfig.PreLaunchEnvironmentVariablesEnvVarName);
                opts.PostLaunchParameters = GetSecureValue(opts.PostLaunchParameters, AppConfig.PostLaunchParametersEnvVarName);
                opts.PreStopParameters = GetSecureValue(opts.PreStopParameters, AppConfig.PreStopParametersEnvVarName);
                opts.PostStopParameters = GetSecureValue(opts.PostStopParameters, AppConfig.PostStopParametersEnvVarName);

                // Validate options
                var validation = _validator.Validate(opts);
                if (!validation.Success)
                    return validation;

                // Ensure wrapper executable exists
                var wrapperExePath = AppConfig.GetServyCLIServicePath();

                if (!File.Exists(wrapperExePath))
                    return CommandResult.Fail(Strings.Msg_WrapperNotFound);

                // Parse enums safely with defaults
                var startupType = ConfigParser.ParseEnum(opts.ServiceStartType, AppConfig.DefaultStartupType);
                var processPriority = ConfigParser.ParseEnum(opts.ProcessPriority, AppConfig.DefaultProcessPriority);
                var dateRotationType = ConfigParser.ParseEnum(opts.DateRotationType, AppConfig.DefaultDateRotationType);
                var recoveryAction = ConfigParser.ParseEnum(opts.RecoveryAction, AppConfig.DefaultRecoveryAction);

                // Parse numeric options
                long rotationSizeBytes = AppConfig.ToBytes(ConfigParser.ParseInt(opts.RotationSize, AppConfig.DefaultRotationSizeMB));
                int heartbeatInterval = ConfigParser.ParseInt(opts.HeartbeatInterval, AppConfig.DefaultHeartbeatInterval);
                int maxFailedChecks = ConfigParser.ParseInt(opts.MaxFailedChecks, AppConfig.DefaultMaxFailedChecks);
                int maxRestartAttempts = ConfigParser.ParseInt(opts.MaxRestartAttempts, AppConfig.DefaultMaxRestartAttempts);
                int preLaunchTimeout = ConfigParser.ParseInt(opts.PreLaunchTimeout, AppConfig.DefaultPreLaunchTimeoutSeconds);
                int preLaunchRetryAttempts = ConfigParser.ParseInt(opts.PreLaunchRetryAttempts, AppConfig.DefaultPreLaunchRetryAttempts);
                int maxRotations = ConfigParser.ParseInt(opts.MaxRotations, AppConfig.DefaultMaxRotations);
                int startTimeout = ConfigParser.ParseInt(opts.StartTimeout, AppConfig.DefaultStartTimeout);
                int stopTimeout = ConfigParser.ParseInt(opts.StopTimeout, AppConfig.DefaultStopTimeout);
                int preStopTimeout = ConfigParser.ParseInt(opts.PreStopTimeout, AppConfig.DefaultPreStopTimeoutSeconds);

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
                    RotationSizeInBytes = rotationSizeBytes,
                    UseLocalTimeForRotation = opts.UseLocalTimeForRotation,
                    EnableHealthMonitoring = opts.EnableHealthMonitoring,
                    HeartbeatInterval = heartbeatInterval,
                    MaxFailedChecks = maxFailedChecks,
                    RecoveryAction = recoveryAction,
                    RecoveryOnCleanExit = opts.RecoveryOnCleanExit,
                    MaxRestartAttempts = maxRestartAttempts,
                    EnvironmentVariables = opts.EnvironmentVariables,
                    ServiceDependencies = opts.ServiceDependencies,

                    Username = opts.User,
                    Password = opts.Password,

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
                var res = await _serviceManager.InstallServiceAsync(options, cancellationToken: cancellationToken);

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
        /// Helper method to securely resolve sensitive fields by prioritizing environment variables over CLI options.
        /// </summary>
        private static string? GetSecureValue(string? optionValue, string envVarName)
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            return !string.IsNullOrEmpty(envValue) ? envValue : optionValue;
        }
    }
}
