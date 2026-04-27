using Servy.Core.Common;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using System.ServiceProcess;

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
        /// <param name="options">The options containing all configuration parameters for the service installation.</param>
        /// <returns>True if the service was successfully installed or updated; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <see cref="InstallServiceOptions.ServiceName"/>, <see cref="InstallServiceOptions.WrapperExePath"/>, or <see cref="InstallServiceOptions.RealExePath"/> is null or empty.</exception>
        /// <exception cref="Win32Exception">Thrown if opening the Service Control Manager or creating/updating the service fails.</exception>
        Task<OperationResult> InstallServiceAsync(InstallServiceOptions options);

        /// <summary>
        /// Uninstalls the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True if the service was uninstalled; otherwise false.</returns>
        Task<OperationResult> UninstallServiceAsync(string? serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="logSuccessfulStart">Indicates whether to log a message when the service starts successfully.</param>
        /// <returns>True if the service was started; otherwise false.</returns>
        Task<OperationResult> StartServiceAsync(string? serviceName, bool logSuccessfulStart = true);

        /// <summary>
        /// Stops the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="logSuccessfulStop">Indicates whether to log a message when the service stops successfully.</param>
        /// <returns>True if the service was stopped; otherwise false.</returns>
        Task<OperationResult> StopServiceAsync(string? serviceName, bool logSuccessfulStop = true);

        /// <summary>
        /// Restarts the specified service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>True if the service was restarted; otherwise false.</returns>
        Task<OperationResult> RestartServiceAsync(string? serviceName);

        /// <summary>
        /// Gets the current status of the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The current <see cref="ServiceControllerStatus"/> of the service.</returns>
        ServiceControllerStatus GetServiceStatus(string? serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a Windows service with the specified name is installed 
        /// on the local machine.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service to check.</param>
        /// <returns>
        /// <c>true</c> if a service with the specified name is installed; 
        /// otherwise, <c>false</c>.
        /// </returns>
        bool IsServiceInstalled(string? serviceName);

        /// <summary>
        /// Gets the startup type of a Windows service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="ServiceStartType"/> value if the service is found; otherwise, <c>null</c>.
        /// </returns>
        ServiceStartType? GetServiceStartupType(string? serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all Windows services on the local machine and maps them to <see cref="ServiceInfo"/> objects.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>A list of <see cref="ServiceInfo"/> representing all services with status, startup type, user, and description.</returns>
        List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        /// <summary>
        /// Gets the dependency tree for the specified Windows service.
        /// </summary>
        /// <param name="serviceName">
        /// The internal name of the service.
        /// </param>
        /// <returns>
        /// A <see cref="ServiceDependencyNode"/> representing the service
        /// dependency hierarchy, or <c>null</c> if the dependencies could
        /// not be resolved.
        /// </returns>
        ServiceDependencyNode? GetDependencies(string? serviceName);
    }
}
