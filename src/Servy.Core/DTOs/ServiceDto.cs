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
        [SqlColumn("INTEGER PRIMARY KEY AUTOINCREMENT")] // Typically excluded from dynamic inserts, but good for completeness
        public int? Id { get; set; }

        /// <summary>
        /// Child Process PID.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [SqlColumn("INTEGER")]
        public int? Pid { get; set; }

        /// <summary>
        /// The unique name of the service.
        /// </summary>
        [SqlColumn("TEXT NOT NULL")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The **Display Name** of the service, shown in the Windows Services management console (<c>services.msc</c>).
        /// </summary>
        /// <remarks>
        /// This name is human-readable, often includes prefixes for grouping, and can be changed 
        /// after the service has been installed.
        /// </remarks>
        [SqlColumn("TEXT")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Optional description of the service.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? Description { get; set; }

        /// <summary>
        /// Path to the executable of the service.
        /// </summary>
        [ServicePath("executable path", isFile: true, required: true)]
        [SqlColumn("TEXT NOT NULL")]
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Optional startup directory for the service executable.
        /// </summary>
        [ServicePath("startup directory", isFile: false)]
        [SqlColumn("TEXT")]
        public string? StartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters to pass to the service executable.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? Parameters { get; set; }

        /// <summary>
        /// Startup type of the service (stored as int, represents <see cref="Servy.Core.Enums.ServiceStartType"/>).
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? StartupType { get; set; }

        /// <summary>
        /// Process priority of the service (stored as int, represents <see cref="Servy.Core.Enums.ProcessPriority"/>).
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? Priority { get; set; }

        /// <summary>
        /// Whether to enable the console user interface for the service.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? EnableConsoleUI { get; set; }

        /// <summary>
        /// Optional path for the standard output log.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? StdoutPath { get; set; }

        /// <summary>
        /// Optional path for the standard error log.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? StderrPath { get; set; }

        /// <summary>
        /// Whether size-based log rotation is enabled.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? EnableSizeRotation { get; set; }

        /// <summary>
        /// Maximum size of the log file in Megabytes (MB) before rotation.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? RotationSize { get; set; }

        /// <summary>
        /// Whether date-based log rotation is enabled.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? EnableDateRotation { get; set; }

        /// <summary>
        /// Date rotation type (stored as int, represents <see cref="Servy.Core.Enums.DateRotationType"/>).
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? DateRotationType { get; set; }

        /// <summary>
        /// Maximum number of rotated log files to keep. 
        /// Set to 0 for unlimited.
        /// </summary>
        [SqlColumn("INTEGER")]
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
        [SqlColumn("INTEGER")]
        public bool? UseLocalTimeForRotation { get; set; }

        /// <summary>
        /// Whether health monitoring is enabled.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? EnableHealthMonitoring { get; set; }

        /// <summary>
        /// Heartbeat interval in seconds for health monitoring.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? HeartbeatInterval { get; set; }

        /// <summary>
        /// Maximum number of consecutive failed health checks before triggering recovery.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? MaxFailedChecks { get; set; }

        /// <summary>
        /// Recovery action for the service (stored as int, represents <see cref="Servy.Core.Enums.RecoveryAction"/>).
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? RecoveryAction { get; set; }

        /// <summary>
        /// Whether to run recovery action even if the process exits successfully.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? RecoveryOnCleanExit { get; set; }

        /// <summary>
        /// Maximum number of restart attempts if the service fails.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? MaxRestartAttempts { get; set; }

        /// <summary>
        /// Gets or sets the path to the process to run on failure.
        /// </summary>
        [ServicePath("failure program executable path", isFile: true)]
        [SqlColumn("TEXT")]
        public string? FailureProgramPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the failure program.
        /// </summary>
        [ServicePath("failure program startup directory", isFile: false)]
        [SqlColumn("TEXT")]
        public string? FailureProgramStartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line parameters for the failure program.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? FailureProgramParameters { get; set; }

        /// <summary>
        /// Optional environment variables for the service, in key=value format separated by semicolons.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? EnvironmentVariables { get; set; }

        /// <summary>
        /// Optional names of dependent services, separated by semicolons.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? ServiceDependencies { get; set; }

        /// <summary>
        /// Whether to run the service as LocalSystem account.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [SqlColumn("INTEGER")]
        public bool? RunAsLocalSystem { get; set; }

        /// <summary>
        /// Optional user account name to run the service under.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [SqlColumn("TEXT")]
        public string? UserAccount { get; set; }

        /// <summary>
        /// Optional password for the user account (stored encrypted in the database).
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [SqlColumn("TEXT")]
        public string? Password { get; set; }

        /// <summary>
        /// Optional path to an executable that runs before the service starts.
        /// </summary>
        [ServicePath("pre-launch executable path", isFile: true)]
        [SqlColumn("TEXT")]
        public string? PreLaunchExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the pre-launch executable.
        /// </summary>
        [ServicePath("pre-launch startup directory", isFile: false)]
        [SqlColumn("TEXT")]
        public string? PreLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the pre-launch executable.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? PreLaunchParameters { get; set; }

        /// <summary>
        /// Optional environment variables for the pre-launch executable, in key=value format.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? PreLaunchEnvironmentVariables { get; set; }

        /// <summary>
        /// Optional path for the pre-launch executable's standard output log.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Optional path for the pre-launch executable's standard error log.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Maximum time in seconds to wait for the pre-launch executable to complete.
        /// </summary>
        /// <value>
        /// The default is synchronous execution. Set to 0 to execute the pre-launch hook 
        /// asynchronously (fire-and-forget), which disables logging and retry support for the hook.
        /// </value>
        [SqlColumn("INTEGER")]
        public int? PreLaunchTimeoutSeconds { get; set; }

        /// <summary>
        /// Maximum number of retry attempts for the pre-launch executable.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? PreLaunchRetryAttempts { get; set; }

        /// <summary>
        /// Whether to ignore failure of the pre-launch executable.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? PreLaunchIgnoreFailure { get; set; }

        /// <summary>
        /// Optional path to an executable that runs after the service starts.
        /// </summary>
        /// <remarks>
        /// Post-launch hooks always run asynchronously and do not support supervisor features 
        /// (stdout capture, timeouts, or retries).
        /// </remarks>
        [ServicePath("post-launch executable path", isFile: true)]
        [SqlColumn("TEXT")]
        public string? PostLaunchExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the post-launch executable.
        /// </summary>
        [ServicePath("post-launch startup directory", isFile: false)]
        [SqlColumn("TEXT")]
        public string? PostLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the post-launch executable.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? PostLaunchParameters { get; set; }

        /// <summary>
        /// Whether debug logs are enabled.
        /// When enabled, environment variables and process parameters are recorded in the local
        /// log file at <c>%ProgramData%\Servy\logs\Servy.Service.log</c>. Sensitive data is
        /// never written to the Windows Event Log or shown by the CLI / PowerShell module.
        /// Not recommended for production environments, as the local log file may contain
        /// sensitive information.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? EnableDebugLogs { get; set; }

        /// <summary>
        /// Timeout in seconds to wait for the process to start successfully before considering the startup as failed.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? StartTimeout { get; set; }

        /// <summary>
        /// Timeout in seconds to wait for the process to exit.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? StopTimeout { get; set; }

        /// <summary>
        /// Previous Timeout in seconds to wait for the process to exit.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [SqlColumn("INTEGER")]
        public int? PreviousStopTimeout { get; set; }

        /// <summary>
        /// Gets or sets the absolute file path where standard output is currently being redirected.
        /// Returns <see langword="null"/> if the service is not redirected or not running.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [SqlColumn("TEXT")] // (Assuming these are tracked in SQLite per the existing schema logic)
        public string? ActiveStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the absolute file path where standard error output is currently being redirected.
        /// Returns <see langword="null"/> if the service is not redirected or not running.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [SqlColumn("TEXT")]
        public string? ActiveStderrPath { get; set; }

        /// <summary>
        /// Optional path to an executable that runs before the service stops.
        /// </summary>
        [ServicePath("pre-stop executable path", isFile: true)]
        [SqlColumn("TEXT")]
        public string? PreStopExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the pre-stop executable.
        /// </summary>
        [ServicePath("pre-stop startup directory", isFile: false)]
        [SqlColumn("TEXT")]
        public string? PreStopStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the pre-stop executable.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? PreStopParameters { get; set; }

        /// <summary>
        /// Maximum time in seconds to wait for the pre-stop executable to complete.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? PreStopTimeoutSeconds { get; set; }

        /// <summary>
        /// Whether to log pre-stop failure as error.
        /// </summary>
        [SqlColumn("INTEGER")]
        public bool? PreStopLogAsError { get; set; }

        /// <summary>
        /// Optional path to an executable that runs after the service stops.
        /// </summary>
        [ServicePath("post-stop executable path", isFile: true)]
        [SqlColumn("TEXT")]
        public string? PostStopExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the post-stop executable.
        /// </summary>
        [ServicePath("post-stop startup directory", isFile: false)]
        [SqlColumn("TEXT")]
        public string? PostStopStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the post-stop executable.
        /// </summary>
        [SqlColumn("TEXT")]
        public string? PostStopParameters { get; set; }

        #endregion

        #region ICloneable Implementation

        /// <summary>
        /// Creates a shallow copy of the current <see cref="ServiceDto"/>.
        /// </summary>
        /// <returns>A new <see cref="ServiceDto"/> object with copied property values.</returns>
        /// <remarks>
        /// <para>
        /// This method uses <c>MemberwiseClone</c> to perform a bitwise copy of the object's fields. 
        /// For value types and strings, this effectively creates a copy of the data. 
        /// </para>
        /// <para>
        /// <b>Note:</b> If this DTO is expanded to include mutable reference types (like Lists or Classes), 
        /// those objects will not be deeply cloned; the reference itself will be copied, leading 
        /// to shared state between the original and the clone.
        /// </para>
        /// </remarks>
        public object Clone() => MemberwiseClone();

        #endregion

        #region ShouldSerialize Methods

        public bool ShouldSerializeDisplayName() => !string.IsNullOrWhiteSpace(DisplayName);
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
        public bool ShouldSerializeRecoveryOnCleanExit() => RecoveryOnCleanExit.HasValue;
        public bool ShouldSerializeMaxRestartAttempts() => MaxRestartAttempts.HasValue;
        public bool ShouldSerializeFailureProgramPath() => !string.IsNullOrWhiteSpace(FailureProgramPath);
        public bool ShouldSerializeFailureProgramStartupDirectory() => !string.IsNullOrWhiteSpace(FailureProgramStartupDirectory);
        public bool ShouldSerializeFailureProgramParameters() => !string.IsNullOrWhiteSpace(FailureProgramParameters);
        public bool ShouldSerializeEnvironmentVariables() => !string.IsNullOrWhiteSpace(EnvironmentVariables);
        public bool ShouldSerializeServiceDependencies() => !string.IsNullOrWhiteSpace(ServiceDependencies);
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
        public bool ShouldSerializePreStopExecutablePath() => !string.IsNullOrWhiteSpace(PreStopExecutablePath);
        public bool ShouldSerializePreStopStartupDirectory() => !string.IsNullOrWhiteSpace(PreStopStartupDirectory);
        public bool ShouldSerializePreStopParameters() => !string.IsNullOrWhiteSpace(PreStopParameters);
        public bool ShouldSerializePreStopTimeoutSeconds() => PreStopTimeoutSeconds.HasValue;
        public bool ShouldSerializePreStopLogAsError() => PreStopLogAsError.HasValue;
        public bool ShouldSerializePostStopExecutablePath() => !string.IsNullOrWhiteSpace(PostStopExecutablePath);
        public bool ShouldSerializePostStopStartupDirectory() => !string.IsNullOrWhiteSpace(PostStopStartupDirectory);
        public bool ShouldSerializePostStopParameters() => !string.IsNullOrWhiteSpace(PostStopParameters);
        public bool ShouldSerializeEnableConsoleUI() => EnableConsoleUI.HasValue;
        
        #endregion
    }
}
