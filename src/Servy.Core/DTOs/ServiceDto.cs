using Newtonsoft.Json;
using System.Xml.Serialization;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Data Transfer Object for persisting a Windows service configuration in SQLite.
    /// </summary>
    public class ServiceDto : ICloneable
    {
        #region Properties

        /// <summary>
        /// Primary key of the service record.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public int? Id { get; set; }

        /// <summary>
        /// Child Process PID.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
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
        public string? Description { get; set; }

        /// <summary>
        /// Path to the executable of the service.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Optional startup directory for the service executable.
        /// </summary>
        public string? StartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters to pass to the service executable.
        /// </summary>
        public string? Parameters { get; set; }

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
        public string? StdoutPath { get; set; }

        /// <summary>
        /// Optional path for the standard error log.
        /// </summary>
        public string? StderrPath { get; set; }

        /// <summary>
        /// Whether size-based log rotation is enabled.
        /// </summary>
        public bool? EnableSizeRotation { get; set; }

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
        /// Gets or sets a value indicating whether to use local system time for log rotation.
        /// </summary>
        /// <remarks>
        /// <para>Default is <c>false</c> (UTC).</para>
        /// <para>Set to <c>true</c> to rotate logs based on the server's local time (e.g., exactly at local midnight). 
        /// This is often preferred for manual log inspection but can be affected by Daylight Saving Time transitions.</para>
        /// <para>Set to <c>false</c> to use Coordinated Universal Time (UTC). 
        /// This ensures a consistent, 24-hour rotation interval regardless of time zone or DST changes.</para>
        /// </remarks>
        public bool? UseLocalTimeForRotation { get; set; }

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
        public string? FailureProgramPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the failure program.
        /// </summary>
        public string? FailureProgramStartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line parameters for the failure program.
        /// </summary>
        public string? FailureProgramParameters { get; set; }

        /// <summary>
        /// Optional environment variables for the service, in key=value format separated by semicolons.
        /// </summary>
        public string? EnvironmentVariables { get; set; }

        /// <summary>
        /// Optional names of dependent services, separated by semicolons.
        /// </summary>
        public string? ServiceDependencies { get; set; }

        /// <summary>
        /// Whether to run the service as LocalSystem account.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public bool? RunAsLocalSystem { get; set; }

        /// <summary>
        /// Optional user account name to run the service under.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public string? UserAccount { get; set; }

        /// <summary>
        /// Optional password for the user account (stored encrypted in the database).
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public string? Password { get; set; }

        /// <summary>
        /// Optional path to an executable that runs before the service starts.
        /// </summary>
        public string? PreLaunchExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the pre-launch executable.
        /// </summary>
        public string? PreLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the pre-launch executable.
        /// </summary>
        public string? PreLaunchParameters { get; set; }

        /// <summary>
        /// Optional environment variables for the pre-launch executable, in key=value format.
        /// </summary>
        public string? PreLaunchEnvironmentVariables { get; set; }

        /// <summary>
        /// Optional path for the pre-launch executable's standard output log.
        /// </summary>
        public string? PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Optional path for the pre-launch executable's standard error log.
        /// </summary>
        public string? PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Maximum time in seconds to wait for the pre-launch executable to complete.
        /// </summary>
        /// <value>
        /// The default is synchronous execution. Set to 0 to execute the pre-launch hook 
        /// asynchronously (fire-and-forget), which disables logging and retry support for the hook.
        /// </value>
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
        /// Optional path to an executable that runs after the service starts.
        /// </summary>
        /// <remarks>
        /// Post-launch hooks always run asynchronously and do not support supervisor features 
        /// (stdout capture, timeouts, or retries).
        /// </remarks>
        public string? PostLaunchExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the post-launch executable.
        /// </summary>
        public string? PostLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the post-launch executable.
        /// </summary>
        public string? PostLaunchParameters { get; set; }

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

        /// <summary>
        /// Previous Timeout in seconds to wait for the process to exit.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public int? PreviousStopTimeout { get; set; }

        /// <summary>
        /// Gets or sets the absolute file path where standard output is currently being redirected.
        /// Returns <see langword="null"/> if the service is not redirected or not running.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public string? ActiveStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the absolute file path where standard error output is currently being redirected.
        /// Returns <see langword="null"/> if the service is not redirected or not running.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public string? ActiveStderrPath { get; set; }

        /// <summary>
        /// Optional path to an executable that runs before the service stops.
        /// </summary>
        public string? PreStopExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the pre-stop executable.
        /// </summary>
        public string? PreStopStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the pre-stop executable.
        /// </summary>
        public string? PreStopParameters { get; set; }

        /// <summary>
        /// Maximum time in seconds to wait for the pre-stop executable to complete.
        /// </summary>
        public int? PreStopTimeoutSeconds { get; set; }

        /// <summary>
        /// Whether to log pre-stop failure as error.
        /// </summary>
        public bool? PreStopLogAsError { get; set; }

        /// <summary>
        /// Optional path to an executable that runs after the service stops.
        /// </summary>
        public string? PostStopExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the post-stop executable.
        /// </summary>
        public string? PostStopStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the post-stop executable.
        /// </summary>
        public string? PostStopParameters { get; set; }

        #endregion

        #region ICloneable Implementation

        /// <summary>
        /// Creates a deep copy of the current <see cref="ServiceDto"/>.
        /// </summary>
        /// <returns>A new <see cref="ServiceDto"/> object with copied property values.</returns>
        /// <remarks>
        /// We use explicit property-by-property copying instead of <c>MemberwiseClone</c> 
        /// to ensure that any future mutable reference types are handled safely and to 
        /// make the cloning contract explicit for maintainers.
        /// </remarks>
        public object Clone()
        {
            var dto = new ServiceDto
            {
                Id = Id,
                Pid = Pid,
                Name = Name,
                DisplayName = DisplayName,
                Description = Description,
                ExecutablePath = ExecutablePath,
                StartupDirectory = StartupDirectory,
                Parameters = Parameters,
                StartupType = StartupType,
                Priority = Priority,
                StdoutPath = StdoutPath,
                StderrPath = StderrPath,
                EnableSizeRotation = EnableSizeRotation,
                RotationSize = RotationSize,
                EnableDateRotation = EnableDateRotation,
                DateRotationType = DateRotationType,
                MaxRotations = MaxRotations,
                UseLocalTimeForRotation = UseLocalTimeForRotation,
                EnableHealthMonitoring = EnableHealthMonitoring,
                HeartbeatInterval = HeartbeatInterval,
                MaxFailedChecks = MaxFailedChecks,
                RecoveryAction = RecoveryAction,
                MaxRestartAttempts = MaxRestartAttempts,
                FailureProgramPath = FailureProgramPath,
                FailureProgramStartupDirectory = FailureProgramStartupDirectory,
                FailureProgramParameters = FailureProgramParameters,
                EnvironmentVariables = EnvironmentVariables,
                ServiceDependencies = ServiceDependencies,
                RunAsLocalSystem = RunAsLocalSystem,
                UserAccount = UserAccount,
                Password = Password,
                PreLaunchExecutablePath = PreLaunchExecutablePath,
                PreLaunchStartupDirectory = PreLaunchStartupDirectory,
                PreLaunchParameters = PreLaunchParameters,
                PreLaunchEnvironmentVariables = PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = PreLaunchStdoutPath,
                PreLaunchStderrPath = PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = PreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = PreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = PreLaunchIgnoreFailure,
                PostLaunchExecutablePath = PostLaunchExecutablePath,
                PostLaunchStartupDirectory = PostLaunchStartupDirectory,
                PostLaunchParameters = PostLaunchParameters,
                EnableDebugLogs = EnableDebugLogs,
                StartTimeout = StartTimeout,
                StopTimeout = StopTimeout,
                PreviousStopTimeout = PreviousStopTimeout,
                ActiveStdoutPath = ActiveStdoutPath,
                ActiveStderrPath = ActiveStderrPath,
                PreStopExecutablePath = PreStopExecutablePath,
                PreStopStartupDirectory = PreStopStartupDirectory,
                PreStopParameters = PreStopParameters,
                PreStopTimeoutSeconds = PreStopTimeoutSeconds,
                PreStopLogAsError = PreStopLogAsError,
                PostStopExecutablePath = PostStopExecutablePath,
                PostStopStartupDirectory = PostStopStartupDirectory,
                PostStopParameters = PostStopParameters,
            };

            return dto;
        }

        #endregion

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
        public bool ShouldSerializeEnableSizeRotation() => EnableSizeRotation.HasValue;
        public bool ShouldSerializeRotationSize() => RotationSize.HasValue;
        public bool ShouldSerializeEnableDateRotation() => EnableDateRotation.HasValue;
        public bool ShouldSerializeDateRotationType() => DateRotationType.HasValue;
        public bool ShouldSerializeMaxRotations() => MaxRotations.HasValue;
        public bool ShouldSerializeUseLocalTimeForRotation() => UseLocalTimeForRotation.HasValue;
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
        public bool ShouldSerializeRunAsLocalSystem() => false;
        public bool ShouldSerializeUserAccount() => false;
        public bool ShouldSerializePassword() => false;
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
        public bool ShouldSerializePreviousStopTimeout() => false;
        public bool ShouldSerializeActiveStdoutPath() => false;
        public bool ShouldSerializeActiveStderrPath() => false;
        public bool ShouldSerializePreStopExecutablePath() => !string.IsNullOrWhiteSpace(PreStopExecutablePath);
        public bool ShouldSerializePreStopStartupDirectory() => !string.IsNullOrWhiteSpace(PreStopStartupDirectory);
        public bool ShouldSerializePreStopParameters() => !string.IsNullOrWhiteSpace(PreStopParameters);
        public bool ShouldSerializePreStopTimeoutSeconds() => PreStopTimeoutSeconds.HasValue;
        public bool ShouldSerializePreStopLogAsError() => PreStopLogAsError.HasValue;
        public bool ShouldSerializePostStopExecutablePath() => !string.IsNullOrWhiteSpace(PostStopExecutablePath);
        public bool ShouldSerializePostStopStartupDirectory() => !string.IsNullOrWhiteSpace(PostStopStartupDirectory);
        public bool ShouldSerializePostStopParameters() => !string.IsNullOrWhiteSpace(PostStopParameters);

        #endregion

    }
}
