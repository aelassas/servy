using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Command options for <c>install</c> command.
    /// Installs a new Windows service with specified parameters.
    /// </summary>
    [Verb("install", HelpText = "Install a new service.")]
    public class InstallServiceOptions
    {
        /// <summary>
        /// Gets or sets the service name.
        /// This option is required and specifies the unique name of the service to install.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Unique service name to install.")]
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the service description.
        /// Optional descriptive text about the service.
        /// </summary>
        [Option('d', "description", HelpText = "Description of the service.")]
        public string ServiceDescription { get; set; }

        /// <summary>
        /// Gets or sets the path to the executable process to run as service.
        /// This option is required.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Path to the executable process.")]
        public string ProcessPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the service process.
        /// Optional.
        /// </summary>
        [Option("startupDir", HelpText = "Startup directory for the process.")]
        public string StartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets additional command-line parameters for the process.
        /// Optional.
        /// </summary>
        [Option("params", HelpText = "Additional parameters for the process.")]
        public string ProcessParameters { get; set; }

        /// <summary>
        /// Gets or sets the startup type of the service.
        /// Possible values:
        /// <list type="bullet">
        /// <item><description>Automatic - Service starts automatically during system startup.</description></item>
        /// <item><description>Manual - Service must be started manually.</description></item>
        /// <item><description>Disabled - Service is disabled and cannot be started.</description></item>
        /// </list>
        /// </summary>
        [Option("startupType", HelpText = "Service startup type. Options: Automatic, Manual, Disabled.")]
        public string ServiceStartType { get; set; }

        /// <summary>
        /// Gets or sets the process priority for the service.
        /// Possible values:
        /// <list type="bullet">
        /// <item><description>Idle</description></item>
        /// <item><description>BelowNormal</description></item>
        /// <item><description>Normal</description></item>
        /// <item><description>AboveNormal</description></item>
        /// <item><description>High</description></item>
        /// <item><description>RealTime</description></item>
        /// </list>
        /// </summary>
        [Option("priority", HelpText = "Process priority level. Options: Idle, BelowNormal, Normal, AboveNormal, High, RealTime.")]
        public string ProcessPriority { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard output logs.
        /// Optional.
        /// </summary>
        [Option("stdout", HelpText = "Path to stdout log file.")]
        public string StdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard error logs.
        /// Optional.
        /// </summary>
        [Option("stderr", HelpText = "Path to stderr log file.")]
        public string StderrPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether log rotation is enabled.
        /// </summary>
        [Option("enableRotation", HelpText = "Enable log rotation.")]
        public bool EnableRotation { get; set; }

        /// <summary>
        /// Gets or sets the rotation size in bytes for log files.
        /// Must be >= 1 MB if rotation is enabled.
        /// </summary>
        [Option("rotationSize", HelpText = "Log rotation size in bytes.")]
        public string RotationSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// </summary>
        [Option("enableHealth", HelpText = "Enable health monitoring.")]
        public bool EnableHealthMonitoring { get; set; }

        /// <summary>
        /// Gets or sets the heartbeat interval in seconds for health monitoring.
        /// Must be >= 5 seconds if health monitoring is enabled.
        /// </summary>
        [Option("heartbeatInterval", HelpText = "Heartbeat interval in seconds.")]
        public string HeartbeatInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of failed health checks before recovery action.
        /// Must be >= 1 if health monitoring is enabled.
        /// </summary>
        [Option("maxFailedChecks", HelpText = "Maximum allowed failed health checks.")]
        public string MaxFailedChecks { get; set; }

        /// <summary>
        /// Gets or sets the recovery action to perform on failure.
        /// Possible values:
        /// <list type="bullet">
        /// <item><description>None - No action will be taken.</description></item>
        /// <item><description>RestartService - Restart the service.</description></item>
        /// <item><description>RestartProcess - Restart the process.</description></item>
        /// <item><description>RestartComputer - Restart the computer.</description></item>
        /// </list>
        /// </summary>
        [Option("recoveryAction", HelpText = "Recovery action on failure. Options: None, RestartService, RestartProcess, RestartComputer.")]
        public string RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of restart attempts after failure.
        /// Must be >= 1 if health monitoring is enabled.
        /// </summary>
        [Option("maxRestartAttempts", HelpText = "Maximum restart attempts on failure.")]
        public string MaxRestartAttempts { get; set; }

        /// <summary>
        /// Gets or sets environment variables for the process.
        /// Optional.
        /// </summary>
        [Option("env", HelpText = "Environment variables for the process. Enter variables in the format varName=varValue, separated by semicolons. Use \\= to escape '=' and \\; to escape ';'. To include a literal backslash before '=' or ';', use double backslashes (\\\\).")]
        public string EnvironmentVariables { get; set; }
    }
}
