using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Services
{
    /// <summary>
    /// Defines the contract for managing Windows services within Servy, 
    /// including installation, uninstallation, starting, stopping, and restarting.
    /// Implementations handle low-level service control operations and configuration,
    /// including process monitoring, logging, and recovery options.
    /// </summary>
    public interface IServiceManager
    {
        /// <summary>
        /// Installs a Windows service using a wrapper executable that launches the real target executable
        /// with specified arguments and working directory.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service to create.</param>
        /// <param name="description">The service description displayed in the Services MMC snap-in.</param>
        /// <param name="wrapperExePath">The full path to the wrapper executable that will be installed as the service binary.</param>
        /// <param name="realExePath">The full path to the real executable to be launched by the wrapper.</param>
        /// <param name="workingDirectory">The working directory to use when launching the real executable.</param>
        /// <param name="realArgs">The command line arguments to pass to the real executable.</param>
        /// <param name="startType">The service startup type (Automatic, Manual, Disabled).</param>
        /// <param name="processPriority">Optional process priority for the service. Defaults to Normal.</param>
        /// <param name="stdoutPath">Optional path for standard output redirection. If null, no redirection is performed.</param>
        /// <param name="stderrPath">Optional path for standard error redirection. If null, no redirection is performed.</param>
        /// <param name="enableSizeRotation">Enable size-based log rotation.</param>
        /// <param name="rotationSizeInBytes">Size in bytes for log rotation. If 0, no rotation is performed.</param>
        /// <param name="enableHealthMonitoring">Enable health monitoring.</param>
        /// <param name="heartbeatInterval">Heartbeat interval in seconds for the process. If 0, health monitoring is disabled.</param>
        /// <param name="maxFailedChecks">Maximum number of failed health checks before the service is considered unhealthy. If 0, health monitoring is disabled.</param>
        /// <param name="recoveryAction">Recovery action to take if the service fails. If None, health monitoring is disabled.</param>
        /// <param name="maxRestartAttempts">Maximum number of restart attempts if the service fails.</param>
        /// <param name="environmentVariables">Environment variables.</param>
        /// <param name="serviceDependencies">Service dependencies.</param>
        /// <param name="username">Service account username: .\username  for local accounts, DOMAIN\username for domain accounts.</param>
        /// <param name="password">Service account password.</param>
        /// <param name="preLaunchExePath">Pre-launch script exe path.</param>
        /// <param name="preLaunchWorkingDirectory">Pre-launch working directory.</param>
        /// <param name="preLaunchArgs">Command line arguments to pass to the pre-launch executable.</param>
        /// <param name="preLaunchEnvironmentVariables">Pre-launch environment variables.</param>
        /// <param name="preLaunchStdoutPath">Optional path for pre-launch standard output redirection. If null, no redirection is performed.</param>
        /// <param name="preLaunchStderrPath">Optional path for pre-launch standard error redirection. If null, no redirection is performed.</param>
        /// <param name="preLaunchTimeout">Pre-launch script timeout in seconds. Default is 30 seconds.</param>
        /// <param name="preLaunchRetryAttempts">Pre-launch script retry attempts.</param>
        /// <param name="preLaunchIgnoreFailure">Ignore failure and start service even if pre-launch script fails.</param>
        /// <param name="failureProgramPath">Failure program path.</param>
        /// <param name="failureProgramWorkingDirectory">Failure program working directory.</param>
        /// <param name="failureProgramArgs">Failure program parameters.</param>
        /// <param name="postLaunchExePath">Post-launch script exe path.</param>
        /// <param name="postLaunchWorkingDirectory">Post-launch working directory.</param>
        /// <param name="postLaunchArgs">Command line arguments to pass to the post-launch executable.</param>
        /// <param name="enableDebugLogs">Enable debug logs for the service wrapper.</param>
        /// <param name="displayName">The Display Name of the service, shown in the Windows Services management console (<c>services.msc</c>).</param>
        /// <param name="maxRotations">The maximum number of rotated log file to keep. Set to 0 for unlimited.</param>
        /// <param name="enableDateRotation">Enables rotation based on the date interval specified by <paramref name="dateRotationType"/>.</param>
        /// <param name="dateRotationType">Defines the date-based rotation schedule (daily, weekly, or monthly).</param>
        /// <param name="startTimeout">The timeout in seconds to wait for the process to start successfully before considering the startup as failed.</param>
        /// <param name="stopTimeout">The timeout in seconds to wait for the process to exit.</param>
        /// <returns>True if the service was successfully installed or updated; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceName"/>, <paramref name="wrapperExePath"/>, or <paramref name="realExePath"/> is null or empty.</exception>
        /// <exception cref="Win32Exception">Thrown if opening the Service Control Manager or creating/updating the service fails.</exception>
        Task<bool> InstallService(
                string serviceName,
                string description,
                string wrapperExePath,
                string realExePath,
                string workingDirectory = null,
                string realArgs = null,
                ServiceStartType startType = ServiceStartType.Automatic,
                ProcessPriority processPriority = ProcessPriority.Normal,
                string stdoutPath = null,
                string stderrPath = null,
                bool enableSizeRotation = false,
                ulong rotationSizeInBytes = AppConfig.DefaultRotationSize * 1024 * 1024,
                bool enableHealthMonitoring = false,
                int heartbeatInterval = AppConfig.DefaultHeartbeatInterval,
                int maxFailedChecks = AppConfig.DefaultMaxFailedChecks,
                RecoveryAction recoveryAction = RecoveryAction.None,
                int maxRestartAttempts = AppConfig.DefaultMaxRestartAttempts,
                string environmentVariables = null,
                string serviceDependencies = null,
                string username = null,
                string password = null,
                string preLaunchExePath = null,
                string preLaunchWorkingDirectory = null,
                string preLaunchArgs = null,
                string preLaunchEnvironmentVariables = null,
                string preLaunchStdoutPath = null,
                string preLaunchStderrPath = null,
                int preLaunchTimeout = AppConfig.DefaultPreLaunchTimeoutSeconds,
                int preLaunchRetryAttempts = AppConfig.DefaultPreLaunchRetryAttempts,
                bool preLaunchIgnoreFailure = false,
                string failureProgramPath = null,
                string failureProgramWorkingDirectory = null,
                string failureProgramArgs = null,
                string postLaunchExePath = null,
                string postLaunchWorkingDirectory = null,
                string postLaunchArgs = null,
                bool enableDebugLogs = false,
                string displayName = null,
                int? maxRotations = AppConfig.DefaultMaxRotations,
                bool enableDateRotation = false,
                DateRotationType dateRotationType = DateRotationType.Daily,
                int? startTimeout = AppConfig.DefaultStartTimeout,
                int? stopTimeout = AppConfig.DefaultStopTimeout
        );

        /// <summary>
        /// Uninstalls the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>True if the service was uninstalled; otherwise false.</returns>
        Task<bool> UninstallService(string serviceName);

        /// <summary>
        /// Starts the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>True if the service was started; otherwise false.</returns>
        bool StartService(string serviceName);

        /// <summary>
        /// Stops the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>True if the service was stopped; otherwise false.</returns>
        bool StopService(string serviceName);

        /// <summary>
        /// Restarts the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>True if the service was restarted; otherwise false.</returns>
        bool RestartService(string serviceName);

        /// <summary>
        /// Gets the current status of the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The current <see cref="ServiceControllerStatus"/> of the service.</returns>
        ServiceControllerStatus GetServiceStatus(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a Windows service with the specified name is installed 
        /// on the local machine.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service to check.</param>
        /// <returns>
        /// <c>true</c> if a service with the specified name is installed; 
        /// otherwise, <c>false</c>.
        /// </returns>
        bool IsServiceInstalled(string serviceName);

        /// <summary>
        /// Gets the startup type of a Windows service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="ServiceStartType"/> value if the service is found; otherwise, <c>null</c>.
        /// </returns>
        ServiceStartType? GetServiceStartupType(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the description of a Windows service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="ServiceStartType"/> value if the service is found; otherwise, <c>empty string</c>.
        /// </returns>
        string GetServiceDescription(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the account under which the Windows service runs by its name.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="ServiceStartType"/> value if the service is found; otherwise, <c>empty string</c>.
        /// </returns>
        string GetServiceUser(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all Windows services on the local machine and maps them to <see cref="ServiceInfo"/> objects.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>A list of <see cref="ServiceInfo"/> representing all services with status, startup type, user, and description.</returns>
        /// <remarks>
        /// This method uses WMI (Win32_Service) to query all services in a single call for performance.
        /// It maps the WMI <c>State</c> property to <see cref="ServiceStatus"/> enum,
        /// and the WMI <c>StartMode</c> property to <see cref="ServiceStartType"/> enum.
        /// </remarks>
        List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default);
    }
}
