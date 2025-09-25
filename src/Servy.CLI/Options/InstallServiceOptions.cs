using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Command options for <c>install</c> command.
    /// Installs a new Windows service with specified parameters.
    /// </summary>
    [Verb("install", HelpText = "Install a new service.")]
    public class InstallServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the service name.
        /// This option is required and specifies the unique name of the service to install.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Unique service name to install.")]
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the service description.
        /// Optional descriptive text about the service.
        /// </summary>
        [Option('d', "description", HelpText = "Description of the service.")]
        public string? ServiceDescription { get; set; }

        /// <summary>
        /// Gets or sets the path to the executable process to run as service.
        /// This option is required.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Path to the executable process.")]
        public string? ProcessPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the service process.
        /// Optional.
        /// </summary>
        [Option("startupDir", HelpText = "Startup directory for the process.")]
        public string? StartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets additional command-line parameters for the process.
        /// Optional.
        /// </summary>
        [Option("params", HelpText = "Additional parameters for the process. Supports environment variable expansion, example: --param=\"%ProgramData%\\MyApp\" --param=\"%MY_VAR%\\bin\"")]
        public string? ProcessParameters { get; set; }

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
        public string? ServiceStartType { get; set; }

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
        public string? ProcessPriority { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard output logs.
        /// Optional.
        /// </summary>
        [Option("stdout", HelpText = "Path to stdout log file.")]
        public string? StdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard error logs.
        /// Optional.
        /// </summary>
        [Option("stderr", HelpText = "Path to stderr log file.")]
        public string? StderrPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether log rotation is enabled.
        /// </summary>
        [Option("enableRotation", HelpText = "Enable log rotation.")]
        public bool EnableRotation { get; set; }

        /// <summary>
        /// Gets or sets the rotation size in bytes for log files.
        /// Must be >= 1 MB if rotation is enabled.
        /// </summary>
        [Option("rotationSize", HelpText = "Log rotation size in Megabytes (MB). Must be greater than or equal to 1 MB.")]
        public string? RotationSize { get; set; }

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
        public string? HeartbeatInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of failed health checks before recovery action.
        /// Must be >= 1 if health monitoring is enabled.
        /// </summary>
        [Option("maxFailedChecks", HelpText = "Maximum allowed failed health checks.")]
        public string? MaxFailedChecks { get; set; }

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
        public string? RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of restart attempts after failure.
        /// Must be >= 1 if health monitoring is enabled.
        /// </summary>
        [Option("maxRestartAttempts", HelpText = "Maximum restart attempts on failure.")]
        public string? MaxRestartAttempts { get; set; }

        /// <summary>
        /// Gets or sets the failure program path.
        /// Optional.
        /// </summary>
        [Option("failureProgramPath", HelpText = "The failure program path. Configure a script or executable to run when the process fails to start. If health monitoring is disabled, the program will run when the process fails to start. If health monitoring is enabled, the program will only run after all configured recovery action retries have failed.")]
        public string? FailureProgramPath { get; set; }

        /// <summary>
        /// Gets or sets the failure program startup directory.
        /// Optional. Defaults to the failure program directory.
        /// </summary>
        [Option("failureProgramStartupDir", HelpText = "Specifies the directory in which the failure program will start. Defaults to the failure program directory.")]
        public string? FailureProgramStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional command-line parameters for the failure program.
        /// Optional.
        /// </summary>
        [Option("failureProgramParams", HelpText = "Additional parameters for the failure program.")]
        public string? FailureProgramParameters { get; set; }

        /// <summary>
        /// Gets or sets environment variables for the process.
        /// Optional.
        /// </summary>
        [Option("env", HelpText = "Environment variables for the process. Enter variables in the format varName=varValue, one per line or separated by semicolons (;). Use \\= to escape '=', \\\" to escape '\"', \\; to escape ';' and \\\\ to escape '\\'. Supports environment variable expansion, example: VAR1=%ProgramData%\\MyApp; VAR2=%VAR1%\\bin")]
        public string? EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets Windows service dependencies.
        /// Optional.
        /// </summary>
        [Option("deps", HelpText = "Specify one or more Windows service names (not display names) that this service depends on separated with semicolons (;). Use service key names without spaces or special characters. Each dependency service must be installed and running before this service can start. If a dependency's start type is Automatic, Windows will try to start it automatically before this service. If a dependency fails to start or is disabled, this service will not start.")]
        public string? ServiceDependencies { get; set; }

        /// <summary>
        /// Gets or sets the Windows service account username.
        /// Optional.
        /// </summary>
        [Option("user", HelpText = "The service account username (e.g., .\\username, DOMAIN\\username, or DOMAIN\\gMSA$ for Group Managed Service Accounts).")]
        public string? User { get; set; }

        /// <summary>
        /// Gets or sets the Windows service account username.
        /// Optional.
        /// </summary>
        [Option("password", HelpText = "The service account password.")]
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch executable path.
        /// Optional.
        /// </summary>
        [Option("preLaunchPath", HelpText = "The pre-launch executable path. Configure an optional script or executable to run before the main service starts. This is useful for preparing configurations, fetching secrets, or other setup tasks. If the pre-launch script fails, the service will not start unless you enable --preLaunchIgnoreFailure.")]
        public string? PreLaunchPath { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch startup directory.
        /// Optional. Defaults to the service working directory.
        /// </summary>
        [Option("preLaunchStartupDir", HelpText = "Specifies the directory in which the pre-launch executable will start. Defaults to the service working directory.")]
        public string? PreLaunchStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional command-line parameters for the process.
        /// Optional.
        /// </summary>
        [Option("preLaunchParams", HelpText = "Additional parameters for the pre-launch executable.")]
        public string? PreLaunchParameters { get; set; }

        /// <summary>
        /// Gets or sets environment variables for the process.
        /// Optional.
        /// </summary>
        [Option("preLaunchEnv", HelpText = "Environment variables for the pre-launch executable. Enter variables in the format varName=varValue, one per line or separated by semicolons (;). Use \\= to escape '=', \\\" to escape '\"', \\; to escape ';' and \\\\ to escape '\\'. Supports environment variable expansion, example: VAR1=%ProgramData%\\MyApp; VAR2=%VAR1%\\bin")]
        public string? PreLaunchEnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard output logs.
        /// Optional.
        /// </summary>
        [Option("preLaunchStdout", HelpText = "Path to stdout log file of the pre-launch executable.")]
        public string? PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard error logs.
        /// Optional.
        /// </summary>
        [Option("preLaunchStderr", HelpText = "Path to stderr log file of the pre-launch executable.")]
        public string? PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the pre-launch executable.
        /// Must be >= 5 seconds.
        /// Optional.
        /// </summary>
        [Option("preLaunchTimeout", HelpText = "Timeout for the pre-launch executable.")]
        public string? PreLaunchTimeout { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch retry attempts.
        /// Must be greater or equal to 0.
        /// Optional.
        /// </summary>
        [Option("preLaunchRetryAttempts", HelpText = "Number of retry attempts for the pre-launch executable if it fails. Must be greater or equal to 0.")]
        public string? PreLaunchRetryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch ignore failure flag.
        /// Optional.
        /// </summary>
        [Option("preLaunchIgnoreFailure", HelpText = "Ignore failure and start service even if pre-launch executable fails.")]
        public bool PreLaunchIgnoreFailure { get; set; }

        /// <summary>
        /// Gets or sets the post-launch executable path.
        /// Optional.
        /// </summary>
        [Option("postLaunchPath", HelpText = "The post-launch executable path. Configure an optional script or executable to run after the process starts successfully.")]
        public string? PostLaunchPath { get; set; }

        /// <summary>
        /// Gets or sets the post-launch startup directory.
        /// Optional. Defaults to the service working directory.
        /// </summary>
        [Option("postLaunchStartupDir", HelpText = "Specifies the directory in which the post-launch executable will start. Defaults to the directory of the post-launch program.")]
        public string? PostLaunchStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional command-line parameters for the process.
        /// Optional.
        /// </summary>
        [Option("postLaunchParams", HelpText = "Additional parameters for the post-launch executable.")]
        public string? PostLaunchParameters { get; set; }
    }
}
