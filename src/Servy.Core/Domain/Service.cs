using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Services;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Servy.Core.Domain
{
    /// <summary>
    /// Represents a Windows service to be managed by Servy.
    /// Contains configuration, execution, and pre-launch settings.
    /// </summary>
    public class Service
    {
        #region Private Fields

        private readonly IServiceManager _serviceManager;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Service Domain.
        /// </summary>
        /// <param name="serviceManager">Service manager.</param>
        public Service(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the child Process PID.
        /// </summary>
        public int? Pid { get; set; }

        /// <summary>
        /// Gets or sets the unique name of the service.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The **Display Name** of the service, shown in the Windows Services management console (<c>services.msc</c>).
        /// </summary>
        /// <remarks>
        /// This name is human-readable, often includes prefixes for grouping, and can be changed 
        /// after the service has been installed.
        /// </remarks>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional description of the service.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the full path to the service executable.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional startup directory for the service process.
        /// </summary>
        public string StartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets optional command-line parameters for the service executable.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Gets or sets the startup type of the service (e.g., Automatic, Manual).
        /// </summary>
        public ServiceStartType StartupType { get; set; }

        /// <summary>
        /// Gets or sets the process priority for the service.
        /// </summary>
        public ProcessPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the optional file path for redirecting standard output.
        /// </summary>
        public string StdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the optional file path for redirecting standard error output.
        /// </summary>
        public string StderrPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether size-based log rotation is enabled.
        /// Default is false.
        /// </summary>
        public bool EnableRotation { get; set; } = false;

        /// <summary>
        /// Gets or sets the rotation size in Megabytes (MB) for log files.
        /// </summary>
        public int RotationSize { get; set; } = AppConfig.DefaultRotationSize;

        /// <summary>
        /// Gets or sets a value indicating whether date-based log rotation is enabled.
        /// Default is false.
        /// </summary>
        public bool EnableDateRotation { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating date rotation type (stored as int, represents <see cref="Servy.Core.Enums.DateRotationType"/>).
        /// </summary>
        public DateRotationType DateRotationType { get; set; }

        /// <summary>
        /// Maximum number of rotated log files to keep. 
        /// Set to 0 for unlimited.
        /// </summary>
        public int MaxRotations { get; set; } = AppConfig.DefaultMaxRotations;

        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// Default is false.
        /// </summary>
        public bool EnableHealthMonitoring { get; set; } = false;

        /// <summary>
        /// Gets or sets the heartbeat interval in seconds for health monitoring.
        /// Default is 30 seconds.
        /// </summary>
        public int HeartbeatInterval { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum number of failed health checks before taking recovery action.
        /// Default is 3.
        /// </summary>
        public int MaxFailedChecks { get; set; } = 3;

        /// <summary>
        /// Gets or sets the recovery action to take when the service fails.
        /// </summary>
        public RecoveryAction RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of automatic restart attempts.
        /// Default is 3.
        /// </summary>
        public int MaxRestartAttempts { get; set; } = 3;

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
        /// Gets or sets environment variables for the service in the form "KEY=VALUE;KEY2=VALUE2".
        /// </summary>
        public string EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets a comma-separated list of dependent service names.
        /// </summary>
        public string ServiceDependencies { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the service should run as LocalSystem.
        /// Default is true.
        /// </summary>
        public bool RunAsLocalSystem { get; set; } = true;

        /// <summary>
        /// Gets or sets the username for the service account (used if not running as LocalSystem).
        /// </summary>
        public string UserAccount { get; set; }

        /// <summary>
        /// Gets or sets the password for the service account (used if not running as LocalSystem).
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the full path to an optional pre-launch executable.
        /// </summary>
        public string PreLaunchExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the optional startup directory for the pre-launch executable.
        /// </summary>
        public string PreLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Gets or sets optional command-line parameters for the pre-launch executable.
        /// </summary>
        public string PreLaunchParameters { get; set; }

        /// <summary>
        /// Gets or sets environment variables for the pre-launch executable.
        /// </summary>
        public string PreLaunchEnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the optional file path for redirecting standard output of the pre-launch process.
        /// </summary>
        public string PreLaunchStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the optional file path for redirecting standard error output of the pre-launch process.
        /// </summary>
        public string PreLaunchStderrPath { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds for the pre-launch process.
        /// Default is 30 seconds.
        /// </summary>
        public int PreLaunchTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the number of retry attempts for the pre-launch process.
        /// Default is 0.
        /// </summary>
        public int PreLaunchRetryAttempts { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore failures of the pre-launch process.
        /// Default is false.
        /// </summary>
        public bool PreLaunchIgnoreFailure { get; set; } = false;

        /// <summary>
        /// Optional path to an executable that runs before the service starts.
        /// </summary>
        public string PostLaunchExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the post-launch executable.
        /// </summary>
        public string PostLaunchStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the post-launch executable.
        /// </summary>
        public string PostLaunchParameters { get; set; }

        /// <summary>
        /// Whether debug logs are enabled.
        /// When enabled, environment variables and process parameters are recorded in the Windows Event Log. 
        /// Not recommended for production environments, as these logs may contain sensitive information.
        /// </summary>
        public bool EnableDebugLogs { get; set; } = false;

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to start successfully before considering the startup as failed.
        /// </summary>
        public int StartTimeout { get; set; } = AppConfig.DefaultStartTimeout;

        /// <summary>
        /// Gets or sets the timeout in seconds to wait for the process to exit.
        /// </summary>
        public int StopTimeout { get; set; } = AppConfig.DefaultStopTimeout;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the Windows service represented by this instance.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the service was successfully started; otherwise, <c>false</c>.
        /// </returns>
        public bool Start()
        {
            return _serviceManager.StartService(Name);
        }

        /// <summary>
        /// Stops the Windows service represented by this instance.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the service was successfully stopped; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool Stop()
        {
            return _serviceManager.StopService(Name);
        }

        /// <summary>
        /// Restarts the Windows service represented by this instance.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the service was successfully restarted; otherwise, <c>false</c>.
        /// </returns>
        public bool Restart()
        {
            return _serviceManager.RestartService(Name);
        }

        /// <summary>
        /// Retrieves the current status of the Windows service represented by this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="ServiceControllerStatus"/> value representing the current service status,
        /// or <c>null</c> if the service is not installed.
        /// </returns>
        public ServiceControllerStatus? GetStatus()
        {
            if (IsInstalled())
            {
                var status = _serviceManager.GetServiceStatus(Name);
                return status;
            }
            return null;
        }

        /// <summary>
        /// Determines whether the Windows service represented by this instance is installed.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the service is installed; otherwise, <c>false</c>.
        /// </returns>
        public bool IsInstalled()
        {
            return _serviceManager.IsServiceInstalled(Name);
        }

        /// <summary>
        /// Gets the configured startup type of the Windows service represented by this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="ServiceStartType"/> value representing the startup type,
        /// or <c>null</c> if the service is not installed or the startup type cannot be determined.
        /// </returns>
        public ServiceStartType? GetServiceStartupType()
        {
            return _serviceManager.GetServiceStartupType(Name);
        }

        /// <summary>
        /// Installs the Windows service using the configured domain properties.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the service wrapper executable is resolved from the 
        /// executing assembly directory. In <c>RELEASE</c> builds, it is resolved from 
        /// the <see cref="AppConfig.ProgramDataPath"/>.
        /// <para>
        /// This method passes all service configuration (paths, parameters, startup 
        /// settings, monitoring options, recovery actions, etc.) to the underlying 
        /// <see cref="IServiceManager"/> implementation.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A task that represents the asynchronous install operation. The task result 
        /// is <c>true</c> if the service was successfully installed or updated; 
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <param name="wrapperExeDir">Wrapper exe parent directory.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if required properties such as <see cref="Name"/> or 
        /// <see cref="ExecutablePath"/> are null or empty.
        /// </exception>
        /// <exception cref="Win32Exception">
        /// Thrown if the Service Control Manager cannot be accessed or the service 
        /// cannot be created/updated.
        /// </exception>
        public async Task<bool> Install(string wrapperExeDir = null)
        {
#if DEBUG
            var wrapperExePath = Path.Combine(wrapperExeDir ?? AppConfig.ProgramDataPath, AppConfig.ServyServiceUIExe);
#else
            var wrapperExePath = Path.Combine(AppConfig.ProgramDataPath, AppConfig.ServyServiceUIExe);
#endif

            return await _serviceManager.InstallService(
                serviceName: Name,
                description: Description ?? string.Empty,
                wrapperExePath: wrapperExePath,
                realExePath: ExecutablePath,
                workingDirectory: StartupDirectory ?? Path.GetDirectoryName(ExecutablePath) ?? string.Empty,
                realArgs: Parameters ?? string.Empty,
                startType: StartupType,
                processPriority: Priority,
                stdoutPath: StdoutPath,
                stderrPath: StderrPath,
                enableSizeRotation: EnableRotation,
                rotationSizeInBytes: (ulong)RotationSize * 1024 * 1024,
                enableHealthMonitoring: EnableHealthMonitoring,
                heartbeatInterval: HeartbeatInterval,
                maxFailedChecks: MaxFailedChecks,
                recoveryAction: RecoveryAction,
                maxRestartAttempts: MaxRestartAttempts,
                environmentVariables: EnvironmentVariables,
                serviceDependencies: ServiceDependencies,
                username: RunAsLocalSystem ? null : UserAccount,
                password: RunAsLocalSystem ? null : Password,
                preLaunchExePath: PreLaunchExecutablePath,
                preLaunchWorkingDirectory: PreLaunchStartupDirectory,
                preLaunchArgs: PreLaunchParameters,
                preLaunchEnvironmentVariables: PreLaunchEnvironmentVariables,
                preLaunchStdoutPath: PreLaunchStdoutPath,
                preLaunchStderrPath: PreLaunchStderrPath,
                preLaunchTimeout: PreLaunchTimeoutSeconds,
                preLaunchRetryAttempts: PreLaunchRetryAttempts,
                preLaunchIgnoreFailure: PreLaunchIgnoreFailure,
                failureProgramPath: FailureProgramPath,
                failureProgramWorkingDirectory: FailureProgramStartupDirectory,
                failureProgramArgs: FailureProgramParameters,

                postLaunchExePath: PostLaunchExecutablePath,
                postLaunchWorkingDirectory: PostLaunchStartupDirectory,
                postLaunchArgs: PostLaunchParameters,

                enableDebugLogs: EnableDebugLogs,

                displayName: DisplayName,

                maxRotations: MaxRotations,

                enableDateRotation: EnableDateRotation,
                dateRotationType: DateRotationType,

                startTimeout: StartTimeout,
                stopTimeout: StopTimeout
            );
        }

        /// <summary>
        /// Uninstalls the Windows service with the configured <see cref="Name"/>.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous uninstall operation. The task result 
        /// is <c>true</c> if the service was successfully uninstalled; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <see cref="Name"/> is null or empty.
        /// </exception>
        /// <exception cref="Win32Exception">
        /// Thrown if the Service Control Manager cannot be accessed or the service 
        /// cannot be removed.
        /// </exception>
        public async Task<bool> Uninstall()
        {
            return await _serviceManager.UninstallService(Name);
        }

        #endregion
    }
}
