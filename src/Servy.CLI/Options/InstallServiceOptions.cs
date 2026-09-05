using CommandLine;
using Servy.CLI.Resources;
using Servy.Core.Config;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Command options for <c>install</c> command.
    /// Installs a new Windows service with specified parameters.
    /// </summary>
    [Verb("install", HelpText = "Help_Verb_Install", ResourceType = typeof(Strings))]
    public class InstallServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the service name.
        /// This option is required and specifies the unique name of the service to install.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Help_Install_Name", ResourceType = typeof(Strings))]
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the service display name.
        /// </summary>
        [Option("displayName", HelpText = "Help_Install_DisplayName", ResourceType = typeof(Strings))]
        public string? ServiceDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the service description.
        /// Optional descriptive text about the service.
        /// </summary>
        [Option('d', "description", HelpText = "Help_Install_Description", ResourceType = typeof(Strings))]
        public string? ServiceDescription { get; set; }

        /// <summary>
        /// Gets or sets the path to the executable process to run as service.
        /// This option is required.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Help_Install_Path", ResourceType = typeof(Strings))]
        public string? ProcessPath { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the service process.
        /// Optional.
        /// </summary>
        [Option("startupDir", HelpText = "Help_Install_StartupDir", ResourceType = typeof(Strings))]
        public string? StartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets additional command-line parameters for the process.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing parameters via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.ProcessParametersEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("params", HelpText = "Help_Install_Params", ResourceType = typeof(Strings))]
        public string? ProcessParameters { get; set; }

        /// <summary>
        /// Gets or sets the startup type of the service.
        /// Possible values:
        /// <list type="bullet">
        /// <item><description>Automatic - Service starts automatically during system startup.</description></item>
        /// <item><description>AutomaticDelayedStart - Service starts automatically with a short delay after system startup.</description></item>
        /// <item><description>Manual - Service must be started manually.</description></item>
        /// <item><description>Disabled - Service is disabled and cannot be started.</description></item>
        /// </list>
        /// </summary>
        [Option("startupType", HelpText = "Help_Install_StartupType", ResourceType = typeof(Strings))]
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
        [Option("priority", HelpText = "Help_Install_Priority", ResourceType = typeof(Strings))]
        public string? ProcessPriority { get; set; }

        /// <summary>
        /// Gets or sets the logical CPUs the process may run on (e.g., '0-3,8' or '0xFF00').
        /// </summary>
        [Option('a', "cpuAffinity", Required = false, HelpText = "Help_Install_CpuAffinity", ResourceType = typeof(Strings))]
        public string? CpuAffinity { get; set; }

        /// <summary>
        /// Gets or sets timeout in seconds to wait for the process to start successfully before considering the startup as failed.
        /// Must be between <see cref="AppConfig.MinStartTimeout"/> and <see cref="AppConfig.MaxStartTimeout"/> seconds.
        /// Optional. Defaults to 10 seconds.
        /// </summary>
        [Option("startTimeout", HelpText = "Help_Install_StartTimeout", ResourceType = typeof(Strings))]
        public string? StartTimeout { get; set; }

        /// <summary>
        /// Gets or sets timeout in seconds to wait for the process to exit.
        /// Must be between <see cref="AppConfig.MinStopTimeout"/> and <see cref="AppConfig.MaxStopTimeout"/> seconds.
        /// Optional. Defaults to 5 seconds.
        /// </summary>
        [Option("stopTimeout", HelpText = "Help_Install_StopTimeout", ResourceType = typeof(Strings))]
        public string? StopTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the console user interface for the service.
        /// </summary>
        [Option("enableConsoleUI", HelpText = "Help_Install_EnableConsoleUI", ResourceType = typeof(Strings))]
        public bool EnableConsoleUI { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard output logs.
        /// Optional.
        /// </summary>
        [Option("stdout", HelpText = "Help_Install_Stdout", ResourceType = typeof(Strings))]
        public string? StdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard error logs.
        /// Optional.
        /// </summary>
        [Option("stderr", HelpText = "Help_Install_Stderr", ResourceType = typeof(Strings))]
        public string? StderrPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether size-based log rotation is enabled.
        /// This option is deprecated and is kept for backward compatibility. Use --enableSizeRotation instead.
        /// </summary>
        [Option("enableRotation", HelpText = "Help_Install_EnableRotation", ResourceType = typeof(Strings))]
        public bool EnableRotation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether size-based log rotation is enabled.
        /// </summary>
        [Option("enableSizeRotation", HelpText = "Help_Install_EnableSizeRotation", ResourceType = typeof(Strings))]
        public bool EnableSizeRotation { get; set; }

        /// <summary>
        /// Gets or sets the rotation size in megabytes (MB) for log files.
        /// Must be between <see cref="AppConfig.MinRotationSize"/> and <see cref="AppConfig.MaxRotationSize"/> MB if rotation is enabled.
        /// </summary>
        [Option("rotationSize", HelpText = "Help_Install_RotationSize", ResourceType = typeof(Strings))]
        public string? RotationSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether date-based log rotation is enabled based on the date interval specified by --dateRotationType.
        /// </summary>
        [Option("enableDateRotation", HelpText = "Help_Install_EnableDateRotation", ResourceType = typeof(Strings))]
        public bool EnableDateRotation { get; set; }

        /// <summary>
        /// Gets or sets the date rotation type.
        /// Possible values:
        /// <list type="bullet">
        /// <item><description>Daily</description></item>
        /// <item><description>Weekly</description></item>
        /// <item><description>Monthly</description></item>
        /// <item><description>None - Disables date-based rotation; use when only size rotation is desired.</description></item>
        /// </list>
        /// </summary>
        [Option("dateRotationType", HelpText = "Help_Install_DateRotationType", ResourceType = typeof(Strings))]
        public string? DateRotationType { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of rotated log files to keep.
        /// Must be between <see cref="AppConfig.MinMaxRotations"/> and <see cref="AppConfig.MaxMaxRotations"/>.
        /// Set to 0 for unlimited.
        /// </summary>
        [Option("maxRotations", HelpText = "Help_Install_MaxRotations", ResourceType = typeof(Strings))]
        public string? MaxRotations { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use local system time for log rotation.
        /// </summary>
        /// <remarks>
        /// <para>Default is <see cref="AppConfig.DefaultUseLocalTimeForRotation"/> (<c>false</c>).</para>
        /// <para>When <c>true</c>, rotation occurs at local midnight. When <c>false</c>, rotation occurs at UTC midnight.</para>
        /// </remarks>
        [Option("useLocalTimeForRotation", HelpText = "Help_Install_UseLocalTimeForRotation", ResourceType = typeof(Strings))]
        public bool UseLocalTimeForRotation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug logs are enabled.
        /// When enabled, environment variables and process parameters are recorded in the Servy.Service.log file.
        /// Not recommended for production environments, as these logs may contain sensitive information.
        /// </summary>
        [Option("debug", HelpText = "Help_Install_Debug", ResourceType = typeof(Strings))]
        public bool EnableDebugLogs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// </summary>
        [Option("enableHealth", HelpText = "Help_Install_EnableHealth", ResourceType = typeof(Strings))]
        public bool EnableHealthMonitoring { get; set; }

        /// <summary>
        /// Gets or sets the heartbeat interval in seconds for health monitoring.
        /// Must be between <see cref="AppConfig.MinHeartbeatInterval"/> and <see cref="AppConfig.MaxHeartbeatInterval"/> seconds if health monitoring is enabled.
        /// </summary>
        [Option("heartbeatInterval", HelpText = "Help_Install_HeartbeatInterval", ResourceType = typeof(Strings))]
        public string? HeartbeatInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of failed health checks before recovery action.
        /// Must be between <see cref="AppConfig.MinMaxFailedChecks"/> and <see cref="AppConfig.MaxMaxFailedChecks"/> if health monitoring is enabled.
        /// </summary>
        [Option("maxFailedChecks", HelpText = "Help_Install_MaxFailedChecks", ResourceType = typeof(Strings))]
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
        [Option("recoveryAction", HelpText = "Help_Install_RecoveryAction", ResourceType = typeof(Strings))]
        public string? RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to run recovery action even if the process exits successfully.
        /// </summary>
        [Option("recoveryOnCleanExit", HelpText = "Help_Install_RecoveryOnCleanExit", ResourceType = typeof(Strings))]
        public bool RecoveryOnCleanExit { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of restart attempts after failure.
        /// Must be between <see cref="AppConfig.MinMaxRestartAttempts"/> and <see cref="AppConfig.MaxMaxRestartAttempts"/> if health monitoring is enabled.
        /// Set to 0 for unlimited restart attempts.
        /// </summary>
        [Option("maxRestartAttempts", HelpText = "Help_Install_MaxRestartAttempts", ResourceType = typeof(Strings))]
        public string? MaxRestartAttempts { get; set; }

        /// <summary>
        /// Gets or sets the absolute URL used to send out-of-band diagnostic heartbeat pings (e.g., healthchecks.io).
        /// While the process is healthy, Servy periodically pings this endpoint to confirm service vitality.
        /// </summary>
        [Option("heartbeatUrl", HelpText = "Help_Install_HeartbeatUrl", ResourceType = typeof(Strings))]
        public string? HeartbeatUrl { get; set; }

        /// <summary>
        /// Gets or sets the HTTP request timeout in seconds for external heartbeat URL pings.
        /// Value must be between <see cref="AppConfig.MinHeartbeatUrlTimeoutSeconds"/> and <see cref="AppConfig.MaxHeartbeatUrlTimeoutSeconds"/>.
        /// Default is <see cref="AppConfig.DefaultHeartbeatUrlTimeoutSeconds"/>.
        /// </summary>
        [Option("heartbeatUrlTimeoutSeconds", HelpText = "Help_Install_HeartbeatUrlTimeoutSeconds", ResourceType = typeof(Strings))]
        public string? HeartbeatUrlTimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether extended flags (/start, /fail) are appended to the heartbeat URL during service startup and failure events.
        /// Default is <see cref="AppConfig.DefaultEnableHeartbeatUrlFlags"/>.
        /// </summary>
        [Option("enableHeartbeatUrlFlags", HelpText = "Help_Install_EnableHeartbeatUrlFlags", ResourceType = typeof(Strings))]
        public bool EnableHeartbeatUrlFlags { get; set; }

        /// <summary>
        /// Gets or sets the failure program path.
        /// Optional.
        /// </summary>
        [Option("failureProgramPath", HelpText = "Help_Install_FailureProgramPath", ResourceType = typeof(Strings))]
        public string? FailureProgramPath { get; set; }

        /// <summary>
        /// Gets or sets the failure program startup directory.
        /// Optional. If not set, defaults to the service working directory.
        /// </summary>
        [Option("failureProgramStartupDir", HelpText = "Help_Install_FailureProgramStartupDir", ResourceType = typeof(Strings))]
        public string? FailureProgramStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional command-line parameters for the failure program.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing parameters via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.FailureProgramParametersEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("failureProgramParams", HelpText = "Help_Install_FailureProgramParams", ResourceType = typeof(Strings))]
        public string? FailureProgramParameters { get; set; }

        /// <summary>
        /// Gets or sets environment variables for the process.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing environment variables via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.EnvironmentVariablesEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("envVars", HelpText = "Help_Install_EnvVars", ResourceType = typeof(Strings))]
        public string? EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets Windows service dependencies.
        /// Optional.
        /// </summary>
        [Option("deps", HelpText = "Help_Install_Deps", ResourceType = typeof(Strings))]
        public string? ServiceDependencies { get; set; }

        /// <summary>
        /// Gets or sets the Windows service account username.
        /// Optional.
        /// </summary>
        [Option("user", HelpText = "Help_Install_User", ResourceType = typeof(Strings))]
        public string? User { get; set; }

        /// <summary>
        /// Gets or sets the Windows service account password.
        /// </summary>
        /// <remarks>
        /// Passing passwords via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.PasswordEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("password", HelpText = "Help_Install_Password", ResourceType = typeof(Strings))]
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch executable path.
        /// Optional.
        /// </summary>
        [Option("preLaunchPath", HelpText = "Help_Install_PreLaunchPath", ResourceType = typeof(Strings))]
        public string? PreLaunchPath { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch startup directory.
        /// Optional. If not set, defaults to the service working directory.
        /// </summary>
        [Option("preLaunchStartupDir", HelpText = "Help_Install_PreLaunchStartupDir", ResourceType = typeof(Strings))]
        public string? PreLaunchStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional parameters for the pre-launch executable.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing parameters via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.PreLaunchParametersEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("preLaunchParams", HelpText = "Help_Install_PreLaunchParams", ResourceType = typeof(Strings))]
        public string? PreLaunchParameters { get; set; }

        /// <summary>
        /// Gets or sets environment variables for the pre-launch executable.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing environment variables via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.PreLaunchEnvironmentVariablesEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("preLaunchEnv", HelpText = "Help_Install_PreLaunchEnv", ResourceType = typeof(Strings))]
        public string? PreLaunchEnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard output logs.
        /// Optional.
        /// </summary>
        [Option("preLaunchStdout", HelpText = "Help_Install_PreLaunchStdout", ResourceType = typeof(Strings))]
        public string? PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the file path to capture standard error logs.
        /// Optional.
        /// </summary>
        [Option("preLaunchStderr", HelpText = "Help_Install_PreLaunchStderr", ResourceType = typeof(Strings))]
        public string? PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the pre-launch executable.
        /// Must be between <see cref="AppConfig.MinPreLaunchTimeoutSeconds"/> and <see cref="AppConfig.MaxPreLaunchTimeoutSeconds"/> seconds.
        /// Optional.
        /// </summary>
        [Option("preLaunchTimeout", HelpText = "Help_Install_PreLaunchTimeout", ResourceType = typeof(Strings))]
        public string? PreLaunchTimeout { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch retry attempts.
        /// Must be between <see cref="AppConfig.MinPreLaunchRetryAttempts"/> and <see cref="AppConfig.MaxPreLaunchRetryAttempts"/>.
        /// Optional.
        /// </summary>
        [Option("preLaunchRetryAttempts", HelpText = "Help_Install_PreLaunchRetryAttempts", ResourceType = typeof(Strings))]
        public string? PreLaunchRetryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the pre-launch ignore failure flag.
        /// Optional.
        /// </summary>
        [Option("preLaunchIgnoreFailure", HelpText = "Help_Install_PreLaunchIgnoreFailure", ResourceType = typeof(Strings))]
        public bool PreLaunchIgnoreFailure { get; set; }

        /// <summary>
        /// Gets or sets the post-launch executable path.
        /// Optional.
        /// </summary>
        [Option("postLaunchPath", HelpText = "Help_Install_PostLaunchPath", ResourceType = typeof(Strings))]
        public string? PostLaunchPath { get; set; }

        /// <summary>
        /// Gets or sets the post-launch startup directory.
        /// Optional. If not set, defaults to the service working directory.
        /// </summary>
        [Option("postLaunchStartupDir", HelpText = "Help_Install_PostLaunchStartupDir", ResourceType = typeof(Strings))]
        public string? PostLaunchStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional parameters for the post-launch executable.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing parameters via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.PostLaunchParametersEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("postLaunchParams", HelpText = "Help_Install_PostLaunchParams", ResourceType = typeof(Strings))]
        public string? PostLaunchParameters { get; set; }

        /// <summary>
        /// Gets or sets the pre-stop executable path.
        /// Optional.
        /// </summary>
        [Option("preStopPath", HelpText = "Help_Install_PreStopPath", ResourceType = typeof(Strings))]
        public string? PreStopPath { get; set; }

        /// <summary>
        /// Gets or sets the pre-stop startup directory.
        /// Optional. If not set, defaults to the service working directory.
        /// </summary>
        [Option("preStopStartupDir", HelpText = "Help_Install_PreStopStartupDir", ResourceType = typeof(Strings))]
        public string? PreStopStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional parameters for the pre-stop executable.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing parameters via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.PreStopParametersEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("preStopParams", HelpText = "Help_Install_PreStopParams", ResourceType = typeof(Strings))]
        public string? PreStopParameters { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the pre-stop executable.
        /// Must be between <see cref="AppConfig.MinPreStopTimeoutSeconds"/> and <see cref="AppConfig.MaxPreStopTimeoutSeconds"/> seconds.
        /// Optional.
        /// </summary>
        [Option("preStopTimeout", HelpText = "Help_Install_PreStopTimeout", ResourceType = typeof(Strings))]
        public string? PreStopTimeout { get; set; }

        /// <summary>
        /// Gets or sets a flag to log pre-stop failure as error.
        /// Optional.
        /// </summary>
        [Option("preStopLogAsError", HelpText = "Help_Install_PreStopLogAsError", ResourceType = typeof(Strings))]
        public bool PreStopLogAsError { get; set; }

        /// <summary>
        /// Gets or sets the post-stop executable path.
        /// Optional.
        /// </summary>
        [Option("postStopPath", HelpText = "Help_Install_PostStopPath", ResourceType = typeof(Strings))]
        public string? PostStopPath { get; set; }

        /// <summary>
        /// Gets or sets the post-stop startup directory.
        /// Optional. If not set, defaults to the service working directory.
        /// </summary>
        [Option("postStopStartupDir", HelpText = "Help_Install_PostStopStartupDir", ResourceType = typeof(Strings))]
        public string? PostStopStartupDir { get; set; }

        /// <summary>
        /// Gets or sets additional parameters for the post-stop executable.
        /// Optional.
        /// </summary>
        /// <remarks>
        /// Passing parameters via CLI flags is insecure as they are visible
        /// in process listings and shell history. Use the <see cref="AppConfig.PostStopParametersEnvVarName"/> environment
        /// variable instead.
        /// </remarks>
        [Sensitive]
        [Option("postStopParams", HelpText = "Help_Install_PostStopParams", ResourceType = typeof(Strings))]
        public string? PostStopParameters { get; set; }
    }
}
