using Servy.Core.Enums;
using Servy.Core.Services;
using System.ServiceProcess;

namespace Servy.Core.Domain
{
    /// <summary>
    /// Represents a Windows service to be managed by Servy.
    /// Contains configuration, execution, and pre-launch settings.
    /// </summary>
    public class Service
    {
        #region Private Fields

        private IServiceManager _serviceManager;

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
        /// Gets or sets the unique name of the service.
        /// </summary>
        public string Name { get; set; } = string.Empty;

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
        /// Gets or sets a value indicating whether log rotation is enabled.
        /// Default is false.
        /// </summary>
        public bool EnableRotation { get; set; } = false;

        /// <summary>
        /// Gets or sets the rotation size in bytes for log files.
        /// Default is 1,048,576 (1 MB).
        /// </summary>
        public int RotationSize { get; set; } = 1_048_576;

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
        public bool Stop()
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

        #endregion
    }
}
