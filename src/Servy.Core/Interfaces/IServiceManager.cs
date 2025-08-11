using Servy.Core.Enums;

namespace Servy.Core.Interfaces
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
        /// <param name="rotationSizeInBytes">Size in bytes for log rotation. If 0, no rotation is performed.</param>
        /// <param name="heartbeatInterval">Heartbeat interval in seconds for the process. If 0, health monitoring is disabled.</param>
        /// <param name="maxFailedChecks">Maximum number of failed health checks before the service is considered unhealthy. If 0, health monitoring is disabled.</param>
        /// <param name="recoveryAction">Recovery action to take if the service fails. If None, health monitoring is disabled.</param>
        /// <param name="maxRestartAttempts">Maximum number of restart attempts if the service fails.</param>
        /// <param name="environmentVariables">Environment variables.</param>
        /// <returns>True if the service was successfully installed or updated; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceName"/>, <paramref name="wrapperExePath"/>, or <paramref name="realExePath"/> is null or empty.</exception>
        /// <exception cref="Win32Exception">Thrown if opening the Service Control Manager or creating/updating the service fails.</exception>
        bool InstallService(
            string serviceName,
            string description,
            string wrapperExePath,
            string realExePath,
            string workingDirectory,
            string realArgs,
            ServiceStartType startType,
            ProcessPriority processPriority,
            string? stdoutPath = null,
            string? stderrPath = null,
            int rotationSizeInBytes = 0,
            int heartbeatInterval = 0,
            int maxFailedChecks = 0,
            RecoveryAction recoveryAction = RecoveryAction.None,
            int maxRestartAttempts = 0,
            string? environmentVariables = null
        );

        /// <summary>
        /// Uninstalls the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>True if the service was uninstalled; otherwise false.</returns>
        bool UninstallService(string serviceName);

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
    }
}
