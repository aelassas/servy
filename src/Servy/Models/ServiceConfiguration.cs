using Servy.Core.Enums;

namespace Servy.Models
{
    /// <summary>
    /// Represents the full configuration of a Windows service to be managed by Servy.
    /// Includes properties for startup settings, process paths, health monitoring, and logging.
    /// </summary>
    public class ServiceConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the service.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the display name of the service.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the description of the service.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the path to the executable process to run.
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the startup directory for the executable.
        /// </summary>
        public string StartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command line parameters to pass to the executable.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Gets or sets the selected startup type for the service.
        /// </summary>
        public ServiceStartType StartupType { get; set; }

        /// <summary>
        /// Gets or sets the process priority for the service's executable.
        /// </summary>
        public ProcessPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the path to the standard output log file.
        /// </summary>
        public string StdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the standard error log file.
        /// </summary>
        public string StderrPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether size-based log rotation is enabled.
        /// </summary>
        public bool EnableSizeRotation { get; set; }

        /// <summary>
        /// Gets or sets the rotation size in MB.
        /// </summary>
        public string RotationSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether date-based log rotation is enabled.
        /// </summary>
        public bool EnableDateRotation { get; set; }

        /// <summary>
        /// Gets or sets the date rotation interval.
        /// </summary>
        public DateRotationType DateRotationType { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of rotated log files to keep.
        /// </summary>
        public string MaxRotations { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// </summary>
        public bool EnableHealthMonitoring { get; set; }

        /// <summary>
        /// Gets or sets the heartbeat interval in seconds.
        /// </summary>
        public string HeartbeatInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of allowed failed health checks.
        /// </summary>
        public string MaxFailedChecks { get; set; }

        /// <summary>
        /// Gets or sets the recovery action to take if the service fails.
        /// </summary>
        public RecoveryAction RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of restart attempts.
        /// </summary>
        public string MaxRestartAttempts { get; set; }

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
        /// Environment Variables.
        /// </summary>
        public string EnvironmentVariables { get; set; }

        /// <summary>
        /// Windows Service Dependencies.
        /// </summary>
        public string ServiceDependencies { get; set; }

        /// <summary>
        /// Indicates whether to run the Windows Service as the Local System account.
        /// </summary>
        public bool RunAsLocalSystem { get; set; } = true;

        /// <summary>
        /// The service account username (e.g., <c>.\username</c>, <c>DOMAIN\username</c>).
        /// </summary>
        public string UserAccount { get; set; }

        /// <summary>
        /// The password for the service account.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The confirmation of the service account password.
        /// </summary>
        public string ConfirmPassword { get; set; }

        /// <summary>
        /// Gets or sets the path to the pre-launch executable process to run.
        /// </summary>
        public string PreLaunchExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the pre-launch executable.
        /// </summary>
        public string PreLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line parameters for the pre-launch executable.
        /// </summary>
        public string PreLaunchParameters { get; set; }

        /// <summary>
        /// Gets or sets the environment variables for the pre-launch executable.
        /// Format: key=value, one per line or separated by semicolons.
        /// </summary>
        public string PreLaunchEnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the path to the standard output log file for the pre-launch process.
        /// </summary>
        public string PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the standard error log file for the pre-launch process.
        /// </summary>
        public string PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds for each pre-launch execution attempt.
        /// Default is 30 seconds.
        /// </summary>
        public string PreLaunchTimeoutSeconds { get; set; } = "30";

        /// <summary>
        /// Gets or sets the number of retry attempts if the pre-launch process fails.
        /// Default is 0.
        /// </summary>
        public string PreLaunchRetryAttempts { get; set; } = "0";

        /// <summary>
        /// Gets or sets a value indicating whether to start the main service even if the pre-launch process fails.
        /// Default is false.
        /// </summary>
        public bool PreLaunchIgnoreFailure { get; set; } = false;

        /// <summary>
        /// Gets or sets the path to the post-launch executable process to run.
        /// </summary>
        public string PostLaunchExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the post-launch executable.
        /// </summary>
        public string PostLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line parameters for the post-launch executable.
        /// </summary>
        public string PostLaunchParameters { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug logs are enabled.
        /// When enabled, environment variables and process parameters are recorded in the Windows Event Log. 
        /// Not recommended for production environments, as these logs may contain sensitive information.
        /// </summary>
        public bool EnableDebugLogs { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to start successfully before considering the startup as failed.
        /// </summary>
        public string StartTimeout { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to exit.
        /// </summary>
        public string StopTimeout { get; set; }

    }
}
