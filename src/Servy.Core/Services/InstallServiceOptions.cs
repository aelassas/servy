using Servy.Core.Config;
using Servy.Core.Enums;

namespace Servy.Core.Services
{
    /// <summary>
    /// Represents the configuration options required for installing or updating a Windows service.
    /// </summary>
    public class InstallServiceOptions
    {
        /// <summary>The name of the Windows service to create.</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>The service description displayed in the Services MMC snap-in.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>The full path to the wrapper executable that will be installed as the service binary.</summary>
        public string WrapperExePath { get; set; } = string.Empty;

        /// <summary>The full path to the real executable to be launched by the wrapper.</summary>
        public string RealExePath { get; set; } = string.Empty;

        /// <summary>The working directory to use when launching the real executable.</summary>
        public string WorkingDirectory { get; set; }

        /// <summary>The command line arguments to pass to the real executable.</summary>
        public string RealArgs { get; set; }

        /// <summary>The service startup type (Automatic, Manual, Disabled).</summary>
        public ServiceStartType StartType { get; set; } = ServiceStartType.Automatic;

        /// <summary>Optional process priority for the service. Defaults to Normal.</summary>
        public ProcessPriority ProcessPriority { get; set; } = ProcessPriority.Normal;

        /// <summary>Whether to enable the console user interface for the service.</summary>
        public bool? EnableConsoleUI { get; set; } = AppConfig.DefaultEnableConsoleUI;

        /// <summary>Optional path for standard output redirection. If null, no redirection is performed.</summary>
        public string StdoutPath { get; set; }

        /// <summary>Optional path for standard error redirection. If null, no redirection is performed.</summary>
        public string StderrPath { get; set; }

        /// <summary>Enable size-based log rotation.</summary>
        public bool EnableSizeRotation { get; set; } = false;

        /// <summary>Size in bytes for log rotation. If 0, no rotation is performed.</summary>
        public ulong RotationSizeInBytes { get; set; } = AppConfig.DefaultRotationSizeMB * 1024 * 1024;

        /// <summary>
        /// Gets or sets a value indicating whether to use local system time for log rotation.
        /// Default is <c>false</c> (UTC).
        /// </summary>
        public bool UseLocalTimeForRotation { get; set; } = false;

        /// <summary>Enable health monitoring.</summary>
        public bool EnableHealthMonitoring { get; set; } = false;

        /// <summary>Heartbeat interval in seconds for the process. If 0, health monitoring is disabled.</summary>
        public int HeartbeatInterval { get; set; } = AppConfig.DefaultHeartbeatInterval;

        /// <summary>Maximum number of failed health checks before the service is considered unhealthy. If 0, health monitoring is disabled.</summary>
        public int MaxFailedChecks { get; set; } = AppConfig.DefaultMaxFailedChecks;

        /// <summary>Recovery action to take if the service fails. If None, health monitoring is disabled.</summary>
        public RecoveryAction RecoveryAction { get; set; } = RecoveryAction.None;

        /// <summary>Maximum number of restart attempts if the service fails.</summary>
        public int MaxRestartAttempts { get; set; } = AppConfig.DefaultMaxRestartAttempts;

        /// <summary>Environment variables.</summary>
        public string EnvironmentVariables { get; set; }

        /// <summary>Service dependencies.</summary>
        public string ServiceDependencies { get; set; }

        /// <summary>Service account username: .\username  for local accounts, DOMAIN\username for domain accounts.</summary>
        public string Username { get; set; }

        /// <summary>Service account password.</summary>
        public string Password { get; set; }

        /// <summary>Pre-launch script exe path.</summary>
        public string PreLaunchExePath { get; set; }

        /// <summary>Pre-launch working directory.</summary>
        public string PreLaunchWorkingDirectory { get; set; }

        /// <summary>Command line arguments to pass to the pre-launch executable.</summary>
        public string PreLaunchArgs { get; set; }

        /// <summary>Pre-launch environment variables.</summary>
        public string PreLaunchEnvironmentVariables { get; set; }

        /// <summary>Optional path for pre-launch standard output redirection. If null, no redirection is performed.</summary>
        public string PreLaunchStdoutPath { get; set; }

        /// <summary>Optional path for pre-launch standard error redirection. If null, no redirection is performed.</summary>
        public string PreLaunchStderrPath { get; set; }

        /// <summary>Pre-launch script timeout in seconds. Default is 30 seconds.</summary>
        public int PreLaunchTimeout { get; set; } = AppConfig.DefaultPreLaunchTimeoutSeconds;

        /// <summary>Pre-launch script retry attempts.</summary>
        public int PreLaunchRetryAttempts { get; set; } = AppConfig.DefaultPreLaunchRetryAttempts;

        /// <summary>Ignore failure and start service even if pre-launch script fails.</summary>
        public bool PreLaunchIgnoreFailure { get; set; } = false;

        /// <summary>Failure program path.</summary>
        public string FailureProgramPath { get; set; }

        /// <summary>Failure program working directory.</summary>
        public string FailureProgramWorkingDirectory { get; set; }

        /// <summary>Failure program parameters.</summary>
        public string FailureProgramArgs { get; set; }

        /// <summary>Post-launch script exe path.</summary>
        public string PostLaunchExePath { get; set; }

        /// <summary>Post-launch working directory.</summary>
        public string PostLaunchWorkingDirectory { get; set; }

        /// <summary>Command line arguments to pass to the post-launch executable.</summary>
        public string PostLaunchArgs { get; set; }

        /// <summary>Enable debug logs for the service wrapper.</summary>
        public bool EnableDebugLogs { get; set; } = false;

        /// <summary>The Display Name of the service, shown in the Windows Services management console (<c>services.msc</c>).</summary>
        public string DisplayName { get; set; }

        /// <summary>The maximum number of rotated log file to keep. Set to 0 for unlimited.</summary>
        public int? MaxRotations { get; set; } = AppConfig.DefaultMaxRotations;

        /// <summary>Enables rotation based on the date interval specified by <paramref name="dateRotationType"/>.</summary>
        public bool EnableDateRotation { get; set; } = false;

        /// <summary>Defines the date-based rotation schedule (daily, weekly, or monthly).</summary>
        public DateRotationType DateRotationType { get; set; } = DateRotationType.Daily;

        /// <summary>The timeout in seconds to wait for the process to start successfully before considering the startup as failed.</summary>
        public int? StartTimeout { get; set; } = AppConfig.DefaultStartTimeout;

        /// <summary>The timeout in seconds to wait for the process to exit.</summary>
        public int? StopTimeout { get; set; } = AppConfig.DefaultStopTimeout;

        /// <summary>The path to an executable that runs before the service stops.</summary>
        public string PreStopExePath { get; set; }

        /// <summary>The startup directory for the pre-stop executable.</summary>
        public string PreStopWorkingDirectory { get; set; }

        /// <summary>The parameters for the pre-stop executable.</summary>
        public string PreStopArgs { get; set; }

        /// <summary>The maximum time in seconds to wait for the pre-stop executable to complete.</summary>
        public int? PreStopTimeout { get; set; } = AppConfig.DefaultPreStopTimeoutSeconds;

        /// <summary>A flag to log pre-stop failure as error.</summary>
        public bool? PreStopLogAsError { get; set; } = false;

        /// <summary>The path to an executable that runs after the service stops.</summary>
        public string PostStopExePath { get; set; }

        /// <summary>The startup directory for the post-stop executable.</summary>
        public string PostStopWorkingDirectory { get; set; }

        /// <summary>The parameters for the post-stop executable.</summary>
        public string PostStopArgs { get; set; }
    }
}