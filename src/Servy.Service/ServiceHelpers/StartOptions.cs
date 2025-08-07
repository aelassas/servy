using Servy.Core.Enums;
using System.Diagnostics;

namespace Servy.Service.ServiceHelpers
{
    /// <summary>
    /// Represents the configuration options used to start and monitor the service process.
    /// </summary>
    public class StartOptions
    {
        /// <summary>
        /// Gets or sets the full path to the executable to run.
        /// </summary>
        public string? ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the executable.
        /// </summary>
        public string? ExecutableArgs { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the process.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the process priority class.
        /// Defaults to <see cref="ProcessPriorityClass.Normal"/>.
        /// </summary>
        public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.Normal;

        /// <summary>
        /// Gets or sets the path to the standard output log file.
        /// </summary>
        public string? StdOutPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the standard error log file.
        /// </summary>
        public string? StdErrPath { get; set; }

        /// <summary>
        /// Gets or sets the maximum size in bytes for log rotation.
        /// </summary>
        public int RotationSizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets the heartbeat interval in seconds for health monitoring.
        /// </summary>
        public int HeartbeatInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed consecutive failed health checks before recovery action is triggered.
        /// </summary>
        public int MaxFailedChecks { get; set; }

        /// <summary>
        /// Gets or sets the recovery action to perform when health checks fail.
        /// Defaults to <see cref="RecoveryAction.None"/>.
        /// </summary>
        public RecoveryAction RecoveryAction { get; set; } = RecoveryAction.None;

        /// <summary>
        /// Gets or sets the name of the Windows service.
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of restart attempts allowed for the child process.
        /// Defaults to 3.
        /// </summary>
        public int MaxRestartAttempts { get; set; } = 3;
    }
}
