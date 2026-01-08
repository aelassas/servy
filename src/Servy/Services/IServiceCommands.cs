using Servy.Core.DTOs;
using Servy.Core.Enums;
using System.Threading.Tasks;

namespace Servy.Services
{
    /// <summary>
    /// Defines commands for managing Windows services, including install, uninstall, start, stop, and restart operations.
    /// </summary>
    public interface IServiceCommands
    {
        /// <summary>
        /// Installs a Windows service with the specified configuration.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="serviceDescription">The description of the service.</param>
        /// <param name="processPath">The executable path of the process to run as a service.</param>
        /// <param name="startupDirectory">The working directory for the process.</param>
        /// <param name="processParameters">Command line parameters for the process.</param>
        /// <param name="startupType">The service startup type.</param>
        /// <param name="processPriority">The process priority.</param>
        /// <param name="stdoutPath">Path to standard output log file.</param>
        /// <param name="stderrPath">Path to standard error log file.</param>
        /// <param name="enableSizeRotation">Whether to enable size-based log rotation.</param>
        /// <param name="rotationSize">The log rotation size threshold.</param>
        /// <param name="enableHealthMonitoring">Whether to enable health monitoring.</param>
        /// <param name="heartbeatInterval">Interval in seconds for health check heartbeat.</param>
        /// <param name="maxFailedChecks">Maximum number of failed health checks before recovery action.</param>
        /// <param name="recoveryAction">The recovery action to take on failure.</param>
        /// <param name="maxRestartAttempts">Maximum number of restart attempts.</param>
        /// <param name="environmentVariables">Environment variables.</param>
        /// <param name="serviceDependencies">Service dependencies.</param>
        /// <param name="runAsLocalSystem">Run service as local user account.</param>
        /// <param name="userAccount">The service account username (e.g., <c>.\username</c>, <c>DOMAIN\username</c>).</param>
        /// <param name="password">The password for the service account.</param>
        /// <param name="confirmPassword">The confirmation of the service account password.</param>
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
        /// <param name="displayName">The display name of the service.</param>
        /// <param name="maxRotations">The maximum number of log rotations to keep.</param>
        /// <param name="enableDateRotation">Enables rotation based on the date interval specified by <paramref name="dateRotationType"/>.</param>
        /// <param name="dateRotationType">Defines the date-based rotation schedule (daily, weekly, or monthly).</param>
        /// <param name="startTimeout">The timeout in seconds to wait for the process to start successfully before considering the startup as failed.</param>
        /// <param name="stopTimeout">The timeout in seconds to wait for the process to exit.</param>
        Task InstallService(
            string serviceName,
            string serviceDescription,
            string processPath,
            string startupDirectory,
            string processParameters,
            ServiceStartType startupType,
            ProcessPriority processPriority,
            string stdoutPath,
            string stderrPath,
            bool enableSizeRotation,
            string rotationSize,
            bool enableHealthMonitoring,
            string heartbeatInterval,
            string maxFailedChecks,
            RecoveryAction recoveryAction,
            string maxRestartAttempts,
            string environmentVariables,
            string serviceDependencies,
            bool runAsLocalSystem,
            string userAccount,
            string password,
            string confirmPassword,
            string preLaunchExePath,
            string preLaunchWorkingDirectory,
            string preLaunchArgs,
            string preLaunchEnvironmentVariables,
            string preLaunchStdoutPath,
            string preLaunchStderrPath,
            string preLaunchTimeout,
            string preLaunchRetryAttempts,
            bool preLaunchIgnoreFailure,
            string failureProgramPath,
            string failureProgramWorkingDirectory,
            string failureProgramArgs,
            string postLaunchExePath,
            string postLaunchWorkingDirectory,
            string postLaunchArgs,
            bool enableDebugLogs,
            string displayName,
            string maxRotations,
            bool enableDateRotation,
            DateRotationType dateRotationType,
            string startTimeout,
            string stopTimeout
            );

        /// <summary>
        /// Uninstalls the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to uninstall.</param>
        Task UninstallService(string serviceName);

        /// <summary>
        /// Starts the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to start.</param>
        void StartService(string serviceName);

        /// <summary>
        /// Stops the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to stop.</param>
        void StopService(string serviceName);

        /// <summary>
        /// Restarts the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to restart.</param>
        void RestartService(string serviceName);

        /// <summary>
        /// Exports the service configuration to an XML file selected by the user.
        /// </summary>
        /// <param name="confirmPassword">The confirmation of the service account password.</param>
        Task ExportXmlConfig(string confirmPassword);

        /// <summary>
        /// Exports the service configuration to an JSON file selected by the user.
        /// </summary>
        /// <param name="confirmPassword">The confirmation of the service account password.</param>
        Task ExportJsonConfig(string confirmPassword);

        /// <summary>
        /// Opens a file dialog to select an XML configuration file for a service,
        /// validates the XML against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the XML is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        Task ImportXmlConfig();

        /// <summary>
        /// Opens a file dialog to select an JSON configuration file for a service,
        /// validates the JSON against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the JSON is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        Task ImportJsonConfig();

        /// <summary>
        /// Opens Servy Manager to manage services.
        /// </summary>
        Task OpenManager();
    }
}
