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
        /// Gets or sets a value indicating whether log rotation is enabled.
        /// </summary>
        public bool EnableRotation { get; set; }

        /// <summary>
        /// Gets or sets the rotation size in bytes.
        /// </summary>
        public string RotationSize { get; set; }

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
        /// Environment Variables.
        /// </summary>
        public string EnvironmentVariables { get; set; }

        /// <summary>
        /// Windows Service Dependencies.
        /// </summary>
        public string ServiceDependencies { get; set; }
    }
}
