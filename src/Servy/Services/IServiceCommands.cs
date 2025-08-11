using Servy.Core.Enums;

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
        /// <param name="enableRotation">Whether to enable log rotation.</param>
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
        void InstallService(
            string serviceName,
            string serviceDescription,
            string processPath,
            string startupDirectory,
            string processParameters,
            ServiceStartType startupType,
            ProcessPriority processPriority,
            string stdoutPath,
            string stderrPath,
            bool enableRotation,
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
            string confirmPassword
            );

        /// <summary>
        /// Uninstalls the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to uninstall.</param>
        void UninstallService(string serviceName);

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
    }
}
