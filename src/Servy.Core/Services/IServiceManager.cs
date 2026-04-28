using Servy.Core.Common;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// <remarks>
    /// This interface deliberately mixes asynchronous and synchronous patterns:
    /// <list type="bullet">
    /// <item>
    /// <description><b>Async Writes:</b> Lifecycle operations (Start, Stop, Install) are asynchronous 
    /// as they involve long-running process transitions.</description>
    /// </item>
    /// <item>
    /// <description><b>Sync Reads:</b> State interrogation (Status, Exists) is synchronous because 
    /// the underlying <see cref="System.ServiceProcess.ServiceController"/> and Win32 SCM APIs 
    /// are inherently blocking and execute quickly.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public interface IServiceManager
    {
        /// <summary>
        /// Installs a Windows service using a wrapper executable that launches the real target executable
        /// with specified arguments and working directory.
        /// </summary>
        /// <param name="options">The options containing all configuration parameters for the service installation, including paths, names, and environment variables.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the installation to complete.</param>
        /// <returns>An <see cref="OperationResult"/> indicating whether the installation was successful and providing error details upon failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <see cref="InstallServiceOptions.ServiceName"/>, <see cref="InstallServiceOptions.WrapperExePath"/>, or <see cref="InstallServiceOptions.RealExePath"/> is null or empty.</exception>
        /// <exception cref="Win32Exception">Thrown if opening the Service Control Manager or creating/updating the service fails via native APIs.</exception>
        Task<OperationResult> InstallServiceAsync(InstallServiceOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops and uninstalls a Windows service, subsequently removing its metadata from the local database.
        /// </summary>
        /// <param name="serviceName">The unique identifier (internal name) of the Windows service to be removed.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the service to stop or for database operations.</param>
        /// <returns>An <see cref="OperationResult"/> indicating whether the uninstallation was successful or providing diagnostic info if it failed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the required service repository has not been initialized.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="serviceName"/> is null, empty, or only contains whitespace.</exception>
        Task<OperationResult> UninstallServiceAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the specified Windows service and waits for it to reach the <see cref="ServiceControllerStatus.Running"/> state.
        /// </summary>
        /// <param name="serviceName">The unique name of the service to start.</param>
        /// <param name="logSuccessfulStart">Indicates whether to log a success message once the service has successfully reached the running state.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the start request or the wait loop.</param>
        /// <returns>An <see cref="OperationResult"/> indicating if the start sequence and optional wait period completed successfully.</returns>
        Task<OperationResult> StartServiceAsync(string serviceName, bool logSuccessfulStart = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the specified Windows service and waits for it to reach the <see cref="ServiceControllerStatus.Stopped"/> state.
        /// </summary>
        /// <param name="serviceName">The unique name of the service to stop.</param>
        /// <param name="logSuccessfulStop">Indicates whether to log a success message once the service has successfully reached the stopped state.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the stop request or the wait loop.</param>
        /// <returns>An <see cref="OperationResult"/> indicating if the stop sequence and optional wait period completed successfully.</returns>
        Task<OperationResult> StopServiceAsync(string serviceName, bool logSuccessfulStop = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restarts the specified Windows service by performing an asynchronous stop followed by an asynchronous start.
        /// </summary>
        /// <param name="serviceName">The unique name of the service to restart.</param>
        /// <param name="logSuccessfulRestart">Indicates whether to log a success message once the full restart cycle is complete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel any part of the restart sequence.</param>
        /// <returns>An <see cref="OperationResult"/> indicating if the entire stop-and-start sequence completed successfully.</returns>
        Task<OperationResult> RestartServiceAsync(string serviceName, bool logSuccessfulRestart = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of the specified Windows service by querying the Service Control Manager.
        /// </summary>
        /// <param name="serviceName">The unique name of the service to interrogate.</param>
        /// <param name="cancellationToken">Optional cancellation token for the status query.</param>
        /// <returns>The current <see cref="ServiceControllerStatus"/> of the service (e.g., Running, Stopped, Paused).</returns>
        ServiceControllerStatus GetServiceStatus(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a Windows service with the specified name is currently installed on the local machine.
        /// </summary>
        /// <param name="serviceName">The unique internal name of the Windows service to check.</param>
        /// <returns>
        /// <c>true</c> if a service with the specified name exists in the SCM; 
        /// otherwise, <c>false</c>.
        /// </returns>
        bool IsServiceInstalled(string serviceName);

        /// <summary>
        /// Gets the startup configuration type (e.g., Automatic, Manual, Disabled) of a Windows service.
        /// </summary>
        /// <param name="serviceName">The unique internal name of the Windows service.</param>
        /// <param name="cancellationToken">Optional cancellation token for the configuration query.</param>
        /// <returns>
        /// A <see cref="ServiceStartType"/> value if the service is found; otherwise, <c>null</c>.
        /// </returns>
        ServiceStartType? GetServiceStartupType(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all Windows services registered on the local machine and maps them to high-level <see cref="ServiceInfo"/> objects.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while enumerating services.</param>
        /// <returns>A list of <see cref="ServiceInfo"/> objects representing all found services, including status, startup types, and descriptions.</returns>
        List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the dependency tree for the specified Windows service, including both services this service depends on and services that depend on this service.
        /// </summary>
        /// <param name="serviceName">The unique internal name of the service.</param>
        /// <returns>
        /// A <see cref="ServiceDependencyNode"/> representing the recursive dependency hierarchy, or <c>null</c> if the service could not be found or dependencies could not be resolved.
        /// </returns>
        ServiceDependencyNode GetDependencies(string serviceName);
    }
}