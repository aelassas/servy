using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Services;
using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
#if !DEBUG
using Servy.Core.Logging;
#endif

namespace Servy.Core.Domain
{
    /// <summary>
    /// Represents a Windows service to be managed by Servy.
    /// Contains configuration, execution, and pre-launch settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hook Execution Logic:</b>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Pre-Launch (Synchronous):</b> By default, these are "Gatekeepers." They block service startup, 
    /// support Stdout/Stderr capture, Retries, and Managed Timeouts.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Pre-Launch (Asynchronous):</b> If <see cref="PreLaunchTimeoutSeconds"/> is set to 0, 
    /// the hook transitions to fire-and-forget mode. It will not block startup, and supervisor 
    /// features (logging/retries) are disabled.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Post-Launch (Asynchronous):</b> Always "Sidecars." These run in a fire-and-forget manner 
    /// after the main process starts and do not support supervisor features.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
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
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
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
        public ServiceStartType StartupType { get; set; } = AppConfig.DefaultStartupType;

        /// <summary>
        /// Gets or sets the process priority for the service.
        /// </summary>
        public ProcessPriority Priority { get; set; } = AppConfig.DefaultPriority;

        /// <summary>
        /// Whether to enable the console user interface for the service.
        /// When enabled, stdout/stderr redirection is disabled, and the service runs in a console window.
        /// </summary>
        public bool EnableConsoleUI { get; set; }

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
        public bool EnableSizeRotation { get; set; } = AppConfig.DefaultEnableRotation;

        /// <summary>
        /// Gets or sets the rotation size in Megabytes (MB) for log files.
        /// </summary>
        public int RotationSize { get; set; } = AppConfig.DefaultRotationSizeMB;

        /// <summary>
        /// Gets or sets a value indicating whether date-based log rotation is enabled.
        /// Default is false.
        /// </summary>
        public bool EnableDateRotation { get; set; } = AppConfig.DefaultEnableDateRotation;

        /// <summary>
        /// Gets or sets a value indicating date rotation type (stored as int, represents <see cref="Servy.Core.Enums.DateRotationType"/>).
        /// </summary>
        public DateRotationType DateRotationType { get; set; } = AppConfig.DefaultDateRotationType;

        /// <summary>
        /// Maximum number of rotated log files to keep. 
        /// Set to 0 for unlimited.
        /// </summary>
        public int MaxRotations { get; set; } = AppConfig.DefaultMaxRotations;

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
        /// Default is false.
        /// </summary>
        public bool EnableHealthMonitoring { get; set; } = AppConfig.DefaultEnableHealthMonitoring;

        /// <summary>
        /// Gets or sets the heartbeat interval in seconds for health monitoring.
        /// Default is 30 seconds.
        /// </summary>
        public int HeartbeatInterval { get; set; } = AppConfig.DefaultHeartbeatInterval;

        /// <summary>
        /// Gets or sets the maximum number of failed health checks before taking recovery action.
        /// Default is 3.
        /// </summary>
        public int MaxFailedChecks { get; set; } = AppConfig.DefaultMaxFailedChecks;

        /// <summary>
        /// Gets or sets the recovery action to take when the service fails.
        /// </summary>
        public RecoveryAction RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of automatic restart attempts.
        /// Default is 3.
        /// </summary>
        public int MaxRestartAttempts { get; set; } = AppConfig.DefaultMaxRestartAttempts;

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
        public bool RunAsLocalSystem { get; set; } = AppConfig.DefaultRunAsLocalSystem;

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
        public int PreLaunchTimeoutSeconds { get; set; } = AppConfig.DefaultPreLaunchTimeoutSeconds;

        /// <summary>
        /// Gets or sets the number of retry attempts for the pre-launch process.
        /// Default is 0.
        /// </summary>
        public int PreLaunchRetryAttempts { get; set; } = AppConfig.DefaultPreLaunchRetryAttempts;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore failures of the pre-launch process.
        /// Default is false.
        /// </summary>
        public bool PreLaunchIgnoreFailure { get; set; } = AppConfig.DefaultPreLaunchIgnoreFailure;

        /// <summary>
        /// Optional path to an executable that runs after the service starts.
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

        /// <summary>
        /// Gets or sets the absolute file path where standard output is currently being redirected.
        /// Returns <see langword="null"/> if the service is not redirected or not running.
        /// </summary>
        public string ActiveStdoutPath { get; set; } = null;

        /// <summary>
        /// Gets or sets the absolute file path where standard error output is currently being redirected.
        /// Returns <see langword="null"/> if the service is not redirected or not running.
        /// </summary>
        public string ActiveStderrPath { get; set; } = null;

        /// <summary>
        /// Optional path to an executable that runs before the service stops.
        /// </summary>
        public string PreStopExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the pre-stop executable.
        /// </summary>
        public string PreStopStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the pre-stop executable.
        /// </summary>
        public string PreStopParameters { get; set; }

        /// <summary>
        /// Maximum time in seconds to wait for the pre-stop executable to complete.
        /// </summary>
        public int PreStopTimeoutSeconds { get; set; } = AppConfig.DefaultPreStopTimeoutSeconds;

        /// <summary>
        /// Whether to log pre-stop failure as error.
        /// </summary>
        public bool PreStopLogAsError { get; set; }

        /// <summary>
        /// Optional path to an executable that runs after the service stops.
        /// </summary>
        public string PostStopExecutablePath { get; set; }

        /// <summary>
        /// Optional startup directory for the post-stop executable.
        /// </summary>
        public string PostStopStartupDirectory { get; set; }

        /// <summary>
        /// Optional parameters for the post-stop executable.
        /// </summary>
        public string PostStopParameters { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the Windows service represented by this instance.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// <c>true</c> if the service was successfully started; otherwise, <c>false</c>.
        /// </returns>
        public async Task<OperationResult> Start(CancellationToken cancellationToken = default)
        {
            return await _serviceManager.StartServiceAsync(Name, logSuccessfulStart: true, cancellationToken);
        }

        /// <summary>
        /// Stops the Windows service represented by this instance.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// <c>true</c> if the service was successfully stopped; otherwise, <c>false</c>.
        /// </returns>
        public virtual async Task<OperationResult> Stop(CancellationToken cancellationToken = default)
        {
            return await _serviceManager.StopServiceAsync(Name, logSuccessfulStop: true, cancellationToken);
        }

        /// <summary>
        /// Restarts the Windows service represented by this instance.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// <c>true</c> if the service was successfully restarted; otherwise, <c>false</c>.
        /// </returns>
        public async Task<OperationResult> Restart(CancellationToken cancellationToken = default)
        {
            return await _serviceManager.RestartServiceAsync(Name, logSuccessfulRestart: true, cancellationToken);
        }

        /// <summary>
        /// Retrieves the current status of the Windows service represented by this instance.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="ServiceControllerStatus"/> value representing the current service status,
        /// or <c>null</c> if the service is not installed.
        /// </returns>
        public ServiceControllerStatus? GetStatus(CancellationToken cancellationToken = default)
        {
            if (IsInstalled())
            {
                var status = _serviceManager.GetServiceStatus(Name, cancellationToken);
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
        /// <param name="isCLI">Indicates if install is from the CLI.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if required properties such as <see cref="Name"/> or 
        /// <see cref="ExecutablePath"/> are null or empty.
        /// </exception>
        /// <exception cref="Win32Exception">
        /// Thrown if the Service Control Manager cannot be accessed or the service 
        /// cannot be created/updated.
        /// </exception>
        public async Task<OperationResult> Install(string wrapperExeDir = null, bool isCLI = false, CancellationToken cancellationToken = default)
        {
            var servyServiceFilename = isCLI ? AppConfig.ServyServiceCLIExe : AppConfig.ServyServiceUIExe;
#if DEBUG
            var wrapperExePath = Path.Combine(wrapperExeDir ?? AppConfig.ProgramDataPath, servyServiceFilename);
#else
            // SECURITY/STABILITY GUARD:
            // In RELEASE builds, we enforce the standard ProgramData path.
            // We warn the caller so they aren't wondering why their custom directory was ignored.
            if (!string.IsNullOrWhiteSpace(wrapperExeDir))
            {
                Logger.Warn($"Install: The 'wrapperExeDir' parameter ('{wrapperExeDir}') is ignored in RELEASE builds. " +
                            $"The service wrapper will be installed to the standard path: {AppConfig.ProgramDataPath}");
            }
            var wrapperExePath = Path.Combine(AppConfig.ProgramDataPath, servyServiceFilename);
#endif

            var options = new InstallServiceOptions
            {
                ServiceName = Name,
                Description = Description ?? string.Empty,
                WrapperExePath = wrapperExePath,
                RealExePath = ExecutablePath,
                WorkingDirectory = StartupDirectory ?? Path.GetDirectoryName(ExecutablePath) ?? string.Empty,
                RealArgs = Parameters ?? string.Empty,
                StartType = StartupType,
                ProcessPriority = Priority,
                EnableConsoleUI = EnableConsoleUI,
                StdoutPath = StdoutPath,
                StderrPath = StderrPath,
                EnableSizeRotation = EnableSizeRotation,
                RotationSizeInBytes = (ulong)Math.Max(1, RotationSize) * 1024UL * 1024UL,
                EnableHealthMonitoring = EnableHealthMonitoring,
                UseLocalTimeForRotation = UseLocalTimeForRotation,
                HeartbeatInterval = HeartbeatInterval,
                MaxFailedChecks = MaxFailedChecks,
                RecoveryAction = RecoveryAction,
                MaxRestartAttempts = MaxRestartAttempts,
                EnvironmentVariables = EnvironmentVariables,
                ServiceDependencies = ServiceDependencies,
                Username = RunAsLocalSystem ? null : UserAccount,
                Password = RunAsLocalSystem ? null : Password,

                PreLaunchExePath = PreLaunchExecutablePath,
                PreLaunchWorkingDirectory = PreLaunchStartupDirectory,
                PreLaunchArgs = PreLaunchParameters,
                PreLaunchEnvironmentVariables = PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = PreLaunchStdoutPath,
                PreLaunchStderrPath = PreLaunchStderrPath,
                PreLaunchTimeout = PreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = PreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = PreLaunchIgnoreFailure,

                FailureProgramPath = FailureProgramPath,
                FailureProgramWorkingDirectory = FailureProgramStartupDirectory,
                FailureProgramArgs = FailureProgramParameters,

                PostLaunchExePath = PostLaunchExecutablePath,
                PostLaunchWorkingDirectory = PostLaunchStartupDirectory,
                PostLaunchArgs = PostLaunchParameters,

                EnableDebugLogs = EnableDebugLogs,
                DisplayName = DisplayName,
                MaxRotations = MaxRotations,
                EnableDateRotation = EnableDateRotation,
                DateRotationType = DateRotationType,
                StartTimeout = StartTimeout,
                StopTimeout = StopTimeout,

                PreStopExePath = PreStopExecutablePath,
                PreStopWorkingDirectory = PreStopStartupDirectory,
                PreStopArgs = PreStopParameters,
                PreStopTimeout = PreStopTimeoutSeconds,
                PreStopLogAsError = PreStopLogAsError,

                PostStopExePath = PostStopExecutablePath,
                PostStopWorkingDirectory = PostStopStartupDirectory,
                PostStopArgs = PostStopParameters,
            };

            return await _serviceManager.InstallServiceAsync(options, cancellationToken);
        }

        /// <summary>
        /// Uninstalls the Windows service with the configured <see cref="Name"/>.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
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
        public async Task<OperationResult> Uninstall(CancellationToken cancellationToken = default)
        {
            return await _serviceManager.UninstallServiceAsync(Name, cancellationToken);
        }

        #endregion

    }
}
