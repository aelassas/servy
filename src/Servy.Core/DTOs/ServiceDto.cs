using Newtonsoft.Json;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Data Transfer Object for persisting a Windows service configuration in SQLite.
    /// </summary>
    public class ServiceDto
    {
        /// <summary>
        /// Primary key of the service record.
        /// </summary>
        [JsonIgnore]
        public int? Id { get; set; }

        /// <summary>
        /// Child Process PID.
        /// </summary>
        [JsonIgnore]
        public int? Pid { get; set; }

        /// <summary>
        /// The unique name of the service.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The **Display Name** of the service, shown in the Windows Services management console (<c>services.msc</c>).
        /// </summary>
        /// <remarks>
        /// This name is human-readable, often includes prefixes for grouping, and can be changed 
        /// after the service has been installed.
        /// </remarks>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the service.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Path to the executable of the service.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Optional startup directory for the service executable.
        /// </summary>
        public string StartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters to pass to the service executable.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Startup type of the service (stored as int, represents <see cref="Servy.Core.Enums.ServiceStartType"/>).
        /// </summary>
        public int? StartupType { get; set; }

        /// <summary>
        /// Process priority of the service (stored as int, represents <see cref="Servy.Core.Enums.ProcessPriority"/>).
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// Optional path for the standard output log.
        /// </summary>
        public string StdoutPath { get; set; }

        /// <summary>
        /// Optional path for the standard error log.
        /// </summary>
        public string StderrPath { get; set; }

        /// <summary>
        /// Whether size-based log rotation is enabled.
        /// </summary>
        public bool? EnableRotation { get; set; }

        /// <summary>
        /// Maximum size of the log file in Megabytes (MB) before rotation.
        /// </summary>
        public int? RotationSize { get; set; }

        /// <summary>
        /// Whether date-based log rotation is enabled.
        /// </summary>
        public bool? EnableDateRotation { get; set; }

        /// <summary>
        /// Date rotation type (stored as int, represents <see cref="Servy.Core.Enums.DateRotationType"/>).
        /// </summary>
        public int? DateRotationType { get; set; }

        /// <summary>
        /// Maximum number of rotated log files to keep. 
        /// Set to 0 for unlimited.
        /// </summary>
        public int? MaxRotations { get; set; }

        /// <summary>
        /// Whether health monitoring is enabled.
        /// </summary>
        public bool? EnableHealthMonitoring { get; set; }

        /// <summary>
        /// Heartbeat interval in seconds for health monitoring.
        /// </summary>
        public int? HeartbeatInterval { get; set; }

        /// <summary>
        /// Maximum number of consecutive failed health checks before triggering recovery.
        /// </summary>
        public int? MaxFailedChecks { get; set; }

        /// <summary>
        /// Recovery action for the service (stored as int, represents <see cref="Servy.Core.Enums.RecoveryAction"/>).
        /// </summary>
        public int? RecoveryAction { get; set; }

        /// <summary>
        /// Maximum number of restart attempts if the service fails.
        /// </summary>
        public int? MaxRestartAttempts { get; set; }

        /// <summary>
        /// Gets or sets the path to the process to run on failure.
        /// </summary>
        public string FailureProgramPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the failure program.
        /// </summary>
        public string FailureProgramStartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line parameters for the failure program.
        /// </summary>
        public string FailureProgramParameters { get; set; }

        /// <summary>
        /// Optional environment variables for the service, in key=value format separated by semicolons.
        /// </summary>
        public string EnvironmentVariables { get; set; }

        /// <summary>
        /// Optional names of dependent services, separated by semicolons.
        /// </summary>
        public string ServiceDependencies { get; set; }

        /// <summary>
        /// Whether to run the service as LocalSystem account.
        /// </summary>
        public bool? RunAsLocalSystem { get; set; }

        /// <summary>
        /// Optional user account name to run the service under.
        /// </summary>
        public string UserAccount { get; set; }

        /// <summary>
        /// Optional password for the user account (stored encrypted in the database).
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Optional path to an executable that runs before the service starts.
        /// </summary>
        public string PreLaunchExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the pre-launch executable.
        /// </summary>
        public string PreLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the pre-launch executable.
        /// </summary>
        public string PreLaunchParameters { get; set; }

        /// <summary>
        /// Optional environment variables for the pre-launch executable, in key=value format.
        /// </summary>
        public string PreLaunchEnvironmentVariables { get; set; }

        /// <summary>
        /// Optional path for the pre-launch executable's standard output log.
        /// </summary>
        public string PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Optional path for the pre-launch executable's standard error log.
        /// </summary>
        public string PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Maximum time in seconds to wait for the pre-launch executable to complete.
        /// </summary>
        public int? PreLaunchTimeoutSeconds { get; set; }

        /// <summary>
        /// Maximum number of retry attempts for the pre-launch executable.
        /// </summary>
        public int? PreLaunchRetryAttempts { get; set; }

        /// <summary>
        /// Whether to ignore failure of the pre-launch executable.
        /// </summary>
        public bool? PreLaunchIgnoreFailure { get; set; }

        /// <summary>
        /// Optional path to an executable that runs before the service starts.
        /// </summary>
        public string PostLaunchExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the post-launch executable.
        /// </summary>
        public string PostLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the post-launch executable.
        /// </summary>
        public string PostLaunchParameters { get; set; }

        /// <summary>
        /// Whether debug logs are enabled.
        /// When enabled, environment variables and process parameters are recorded in the Windows Event Log. 
        /// Not recommended for production environments, as these logs may contain sensitive information.
        /// </summary>
        public bool? EnableDebugLogs { get; set; }

        /// <summary>
        /// Timeout in seconds to wait for the process to start successfully before considering the startup as failed.
        /// </summary>
        public int? StartTimeout { get; set; }

        /// <summary>
        /// Timeout in seconds to wait for the process to exit.
        /// </summary>
        public int? StopTimeout { get; set; }

        #region ShouldSerialize Methods

        public bool ShouldSerializeId() => false;
        public bool ShouldSerializePid() => false;
        public bool ShouldSerializeDescription() => !string.IsNullOrWhiteSpace(Description);
        public bool ShouldSerializeStartupDirectory() => !string.IsNullOrWhiteSpace(StartupDirectory);
        public bool ShouldSerializeParameters() => !string.IsNullOrWhiteSpace(Parameters);
        public bool ShouldSerializeStartupType() => StartupType.HasValue;
        public bool ShouldSerializePriority() => Priority.HasValue;
        public bool ShouldSerializeStdoutPath() => !string.IsNullOrWhiteSpace(StdoutPath);
        public bool ShouldSerializeStderrPath() => !string.IsNullOrWhiteSpace(StderrPath);
        public bool ShouldSerializeEnableRotation() => EnableRotation.HasValue;
        public bool ShouldSerializeRotationSize() => RotationSize.HasValue;
        public bool ShouldSerializeEnableDateRotation() => EnableDateRotation.HasValue;
        public bool ShouldSerializeDateRotationType() => DateRotationType.HasValue;
        public bool ShouldSerializeMaxRotations() => MaxRotations.HasValue;
        public bool ShouldSerializeEnableHealthMonitoring() => EnableHealthMonitoring.HasValue;
        public bool ShouldSerializeHeartbeatInterval() => HeartbeatInterval.HasValue;
        public bool ShouldSerializeMaxFailedChecks() => MaxFailedChecks.HasValue;
        public bool ShouldSerializeRecoveryAction() => RecoveryAction.HasValue;
        public bool ShouldSerializeMaxRestartAttempts() => MaxRestartAttempts.HasValue;
        public bool ShouldSerializeFailureProgramPath() => !string.IsNullOrWhiteSpace(FailureProgramPath);
        public bool ShouldSerializeFailureProgramStartupDirectory() => !string.IsNullOrWhiteSpace(FailureProgramStartupDirectory);
        public bool ShouldSerializeFailureProgramParameters() => !string.IsNullOrWhiteSpace(FailureProgramParameters);
        public bool ShouldSerializeEnvironmentVariables() => !string.IsNullOrWhiteSpace(EnvironmentVariables);
        public bool ShouldSerializeServiceDependencies() => !string.IsNullOrWhiteSpace(ServiceDependencies);
        public bool ShouldSerializeRunAsLocalSystem() => RunAsLocalSystem.HasValue;
        public bool ShouldSerializeUserAccount() => !string.IsNullOrWhiteSpace(UserAccount);
        public bool ShouldSerializePassword() => !string.IsNullOrWhiteSpace(Password);
        public bool ShouldSerializePreLaunchExecutablePath() => !string.IsNullOrWhiteSpace(PreLaunchExecutablePath);
        public bool ShouldSerializePreLaunchStartupDirectory() => !string.IsNullOrWhiteSpace(PreLaunchStartupDirectory);
        public bool ShouldSerializePreLaunchParameters() => !string.IsNullOrWhiteSpace(PreLaunchParameters);
        public bool ShouldSerializePreLaunchEnvironmentVariables() => !string.IsNullOrWhiteSpace(PreLaunchEnvironmentVariables);
        public bool ShouldSerializePreLaunchStdoutPath() => !string.IsNullOrWhiteSpace(PreLaunchStdoutPath);
        public bool ShouldSerializePreLaunchStderrPath() => !string.IsNullOrWhiteSpace(PreLaunchStderrPath);
        public bool ShouldSerializePreLaunchTimeoutSeconds() => PreLaunchTimeoutSeconds.HasValue;
        public bool ShouldSerializePreLaunchRetryAttempts() => PreLaunchRetryAttempts.HasValue;
        public bool ShouldSerializePreLaunchIgnoreFailure() => PreLaunchIgnoreFailure.HasValue;
        public bool ShouldSerializePostLaunchExecutablePath() => !string.IsNullOrWhiteSpace(PostLaunchExecutablePath);
        public bool ShouldSerializePostLaunchStartupDirectory() => !string.IsNullOrWhiteSpace(PostLaunchStartupDirectory);
        public bool ShouldSerializePostLaunchParameters() => !string.IsNullOrWhiteSpace(PostLaunchParameters);
        public bool ShouldSerializeEnableDebugLogs() => EnableDebugLogs.HasValue;
        public bool ShouldSerializeStartTimeout() => StartTimeout.HasValue;
        public bool ShouldSerializeStopTimeout() => StopTimeout.HasValue;

        #endregion
    }
}
