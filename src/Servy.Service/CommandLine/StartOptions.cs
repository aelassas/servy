using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using System.Collections.Generic;
using System.Diagnostics;

namespace Servy.Service.CommandLine
{
    /// <summary>
    /// Represents the configuration options used to start and monitor the service process.
    /// </summary>
    public class StartOptions
    {
        /// <summary>
        /// Gets or sets the full path to the executable to run.
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the executable.
        /// </summary>
        public string ExecutableArgs { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the process.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the process priority class.
        /// Defaults to <see cref="ProcessPriorityClass.Normal"/>.
        /// </summary>
        public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.Normal;

        /// <summary>
        /// Gets or sets the path to the standard output log file.
        /// </summary>
        public string StdOutPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the standard error log file.
        /// </summary>
        public string StdErrPath { get; set; }

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
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of restart attempts allowed for the child process.
        /// Defaults to 3.
        /// </summary>
        public int MaxRestartAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the environment variables of the child process.
        /// </summary>
        public List<EnvironmentVariable> EnvironmentVariables { get; set; } = new List<EnvironmentVariable>();

        /// <summary>
        /// Gets or sets the full path to the pre-launch executable to run.
        /// </summary>
        public string PreLaunchExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the pre-launch process.
        /// </summary>
        public string PreLaunchWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the pre-launch executable.
        /// </summary>
        public string PreLaunchExecutableArgs { get; set; }

        /// <summary>
        /// Gets or sets the environment variables of the pre-launch process.
        /// </summary>
        public List<EnvironmentVariable> PreLaunchEnvironmentVariables { get; set; } = new List<EnvironmentVariable>();

        /// <summary>
        /// Gets or sets the path to the pre-launch standard output log file.
        /// </summary>
        public string PreLaunchStdOutPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the pre-launch standard error log file.
        /// </summary>
        public string PreLaunchStdErrPath { get; set; }

        /// <summary>
        /// Gets or sets the timeout of pre-launch script.
        /// Defaults to 30 seconds.
        /// </summary>
        public int PreLaunchTimeout { get; set; } = 30;

        /// <summary>
        /// Gets or sets the pre-launch script retry attempts.
        /// Defaults to 0.
        /// </summary>
        public int PreLaunchRetryAttempts { get; set; } = 0;

        /// <summary>
        /// Gets or sets the ignore failure option of pre-launch script.
        /// Defaults to false.
        /// </summary>
        public bool PreLaunchIgnoreFailure { get; set; } = false;

        /// <summary>
        /// Gets or sets the full path to the failure program to run.
        /// </summary>
        public string FailureProgramPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the failure program.
        /// </summary>
        public string FailureProgramWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the failure program.
        /// </summary>
        public string FailureProgramArgs { get; set; }

        /// <summary>
        /// Gets or sets the full path to the post-launch executable to run.
        /// </summary>
        public string PostLaunchExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the post-launch process.
        /// </summary>
        public string PostLaunchWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the post-launch executable.
        /// </summary>
        public string PostLaunchExecutableArgs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug logs are enabled.
        /// When enabled, environment variables and process parameters are recorded in the Windows Event Log. 
        /// Not recommended for production environments, as these logs may contain sensitive information.
        /// </summary>
        public bool EnableDebugLogs { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of rotated log files to keep. 
        /// Defaults to 0 (unlimited).
        /// </summary>
        public int MaxRotations { get; set; } = AppConfig.DefaultMaxRotations;

        /// <summary>
        /// Gets or sets a value indicating whether size log rotation is enabled.
        /// </summary>
        public bool EnableSizeRotation { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether date log rotation is enabled.
        /// </summary>
        public bool EnableDateRotation { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating date rotation type (<see cref="Core.Enums.DateRotationType"/>).
        /// </summary>
        public DateRotationType DateRotationType { get; set; } = DateRotationType.Daily;

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to start successfully before considering the startup as failed.
        /// </summary>
        public int StartTimeout { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to exit.
        /// </summary>
        public int StopTimeout { get; set; }

    }
}
