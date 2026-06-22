using Servy.Core.Config;
using Servy.Core.DTOs;
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
        [ServicePath("Executable path", isFile: true, required: true)]
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the executable.
        /// </summary>
        public string ExecutableArgs { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the process.
        /// </summary>
        [ServicePath("Startup directory", isFile: false)]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the process priority class.
        /// </summary>
        public ProcessPriorityClass Priority { get; set; } = StartOptionsParser.MapPriority(AppConfig.DefaultProcessPriority);

        /// <summary>
        /// Gets or sets a value indicating whether to enable the console user interface for the service.
        /// </summary>
        public bool EnableConsoleUI { get; set; } = AppConfig.DefaultEnableConsoleUI;

        /// <summary>
        /// Gets or sets the path to the standard output log file.
        /// </summary>
        public string StdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the standard error log file.
        /// </summary>
        public string StderrPath { get; set; }

        /// <summary>
        /// Gets or sets the maximum size in bytes for log rotation.
        /// </summary>
        public long RotationSizeInBytes { get; set; }

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
        public bool UseLocalTimeForRotation { get; set; } = AppConfig.DefaultUseLocalTimeForRotation;

        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// </summary>
        public bool EnableHealthMonitoring { get; set; } = AppConfig.DefaultEnableHealthMonitoring;

        /// <summary>
        /// Gets or sets the heartbeat interval in seconds for health monitoring.
        /// </summary>
        public int HeartbeatInterval { get; set; } = AppConfig.DefaultHeartbeatInterval;

        /// <summary>
        /// Gets or sets the maximum allowed consecutive failed health checks before recovery action is triggered.
        /// </summary>
        public int MaxFailedChecks { get; set; } = AppConfig.DefaultMaxFailedChecks;

        /// <summary>
        /// Gets or sets the recovery action to perform when health checks fail.
        /// </summary>
        public RecoveryAction RecoveryAction { get; set; } = AppConfig.DefaultRecoveryAction;

        /// <summary>
        /// Gets or sets a flag for running recovery action even if the process exits successfully.
        /// </summary>
        public bool RecoveryOnCleanExit { get; set; } = AppConfig.DefaultRecoveryOnCleanExit;

        /// <summary>
        /// Gets or sets the name of the Windows service.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of restart attempts allowed for the child process.
        /// Defaults to <see cref="AppConfig.DefaultMaxRestartAttempts"/>.
        /// </summary>
        public int MaxRestartAttempts { get; set; } = AppConfig.DefaultMaxRestartAttempts;

        /// <summary>
        /// Gets or sets the full path to the failure program to run.
        /// </summary>
        [ServicePath("Failure program executable path", isFile: true)]
        public string FailureProgramPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the failure program.
        /// </summary>
        [ServicePath("Failure program startup directory", isFile: false)]
        public string FailureProgramWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the failure program.
        /// </summary>
        public string FailureProgramArgs { get; set; }

        /// <summary>
        /// Gets or sets the environment variables of the child process.
        /// </summary>
        public List<EnvironmentVariable> EnvironmentVariables { get; set; } = new List<EnvironmentVariable>();

        /// <summary>
        /// Gets or sets the full path to the pre-launch executable to run.
        /// </summary>
        [ServicePath("Pre-launch executable path", isFile: true)]
        public string PreLaunchExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the pre-launch process.
        /// </summary>
        [ServicePath("Pre-launch startup directory", isFile: false)]
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
        public string PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the pre-launch standard error log file.
        /// </summary>
        public string PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Gets or sets the timeout of pre-launch script.
        /// Defaults to <see cref="AppConfig.DefaultPreLaunchTimeoutSeconds"/>.
        /// </summary>
        public int PreLaunchTimeoutInSeconds { get; set; } = AppConfig.DefaultPreLaunchTimeoutSeconds;

        /// <summary>
        /// Gets or sets the pre-launch script retry attempts.
        /// Defaults to <see cref="AppConfig.DefaultPreLaunchRetryAttempts"/>.
        /// </summary>
        public int PreLaunchRetryAttempts { get; set; } = AppConfig.DefaultPreLaunchRetryAttempts;

        /// <summary>
        /// Gets or sets the ignore failure option of pre-launch script.
        /// Defaults to <see cref="AppConfig.DefaultPreLaunchIgnoreFailure"/>.
        /// </summary>
        public bool PreLaunchIgnoreFailure { get; set; } = AppConfig.DefaultPreLaunchIgnoreFailure;

        /// <summary>
        /// Gets or sets the full path to the post-launch executable to run.
        /// </summary>
        [ServicePath("Post-launch executable path", isFile: true)]
        public string PostLaunchExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the post-launch process.
        /// </summary>
        [ServicePath("Post-launch startup directory", isFile: false)]
        public string PostLaunchWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the post-launch executable.
        /// </summary>
        public string PostLaunchExecutableArgs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug logs are enabled.
        /// When enabled, environment variables and process parameters are recorded in the local
        /// log file at <c>%ProgramData%\Servy\logs\Servy.Service.log</c>. Sensitive data is
        /// never written to the Windows Event Log or shown by the CLI / PowerShell module.
        /// Not recommended for production environments, as these logs may contain sensitive information.
        /// </summary>
        public bool EnableDebugLogs { get; set; } = AppConfig.DefaultEnableDebugLogs;

        /// <summary>
        /// Gets or sets the maximum number of rotated log files to keep. 
        /// Defaults to <see cref="AppConfig.DefaultMaxRotations"/>.
        /// 0 means unlimited.
        /// </summary>
        public int MaxRotations { get; set; } = AppConfig.DefaultMaxRotations;

        /// <summary>
        /// Gets or sets a value indicating whether size log rotation is enabled.
        /// </summary>
        public bool EnableSizeRotation { get; set; } = AppConfig.DefaultEnableSizeRotation;

        /// <summary>
        /// Gets or sets a value indicating whether date log rotation is enabled.
        /// </summary>
        public bool EnableDateRotation { get; set; } = AppConfig.DefaultEnableDateRotation;

        /// <summary>
        /// Gets or sets a value indicating date rotation type (<see cref="Core.Enums.DateRotationType"/>).
        /// </summary>
        public DateRotationType DateRotationType { get; set; } = AppConfig.DefaultDateRotationType;

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to start successfully before considering the startup as failed.
        /// </summary>
        public int StartTimeoutInSeconds { get; set; } = AppConfig.DefaultStartTimeout;

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to exit.
        /// </summary>
        public int StopTimeoutInSeconds { get; set; } = AppConfig.DefaultStopTimeout;

        /// <summary>
        /// Gets or sets the optional path to an executable that runs before the service stops.
        /// </summary>
        [ServicePath("Pre-stop executable path", isFile: true)]
        public string PreStopExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the optional startup directory for the pre-stop executable.
        /// </summary>
        [ServicePath("Pre-stop startup directory", isFile: false)]
        public string PreStopWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the optional parameters for the pre-stop executable.
        /// </summary>
        public string PreStopExecutableArgs { get; set; }

        /// <summary>
        /// Gets or sets the maximum time in seconds to wait for the pre-stop executable to complete.
        /// </summary>
        public int PreStopTimeoutInSeconds { get; set; } = AppConfig.DefaultPreStopTimeoutSeconds;

        /// <summary>
        /// Gets or sets a value indicating whether pre-stop failure is logged as an error.
        /// </summary>
        public bool PreStopLogAsError { get; set; } = AppConfig.DefaultPreStopLogAsError;

        /// <summary>
        /// Gets or sets the optional path to an executable that runs after the service stops.
        /// </summary>
        [ServicePath("Post-stop executable path", isFile: true)]
        public string PostStopExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the optional startup directory for the post-stop executable.
        /// </summary>
        [ServicePath("Post-stop startup directory", isFile: false)]
        public string PostStopWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the optional parameters for the post-stop executable.
        /// </summary>
        public string PostStopExecutableArgs { get; set; }
    }
}