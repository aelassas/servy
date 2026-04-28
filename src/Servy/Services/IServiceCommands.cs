using Servy.Core.DTOs;
using Servy.Models;

namespace Servy.Services
{
    /// <summary>
    /// Defines commands for managing Windows services, including install, uninstall, start, stop, and restart operations.
    /// </summary>
    public interface IServiceCommands
    {
        /// <summary>
        /// Orchestrates the installation of a new Windows service using the provided configuration.
        /// </summary>
        /// <param name="config">
        /// The <see cref="ServiceConfiguration"/> containing all process paths, 
        /// startup parameters, and lifecycle hook settings.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous installation operation.</returns>
        /// <remarks>
        /// <para>
        /// This method serves as the primary controller for the service installation workflow. 
        /// It performs several critical steps:
        /// <list type="bullet">
        /// <item><description>Locates and validates the Servy UI service wrapper executable.</description></item>
        /// <item><description>Maps the high-level <see cref="ServiceConfiguration"/> to a data-transfer object (<see cref="ServiceDto"/>).</description></item>
        /// <item><description>Triggers comprehensive validation via the <see cref="IServiceConfigurationValidator"/>.</description></item>
        /// <item><description>Checks for existing service name collisions in the Windows SCM.</description></item>
        /// <item><description>Invokes the <see cref="IServiceManager"/> to perform the actual OS-level installation.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Security:</b> User credentials (account and password) are handled according to the 
        /// <see cref="ServiceConfiguration.RunAsLocalSystem"/> flag. Credentials are encrypted 
        /// before being stored in the repository.
        /// </para>
        /// </remarks>
        /// <exception cref="UnauthorizedAccessException">Thrown if the application lacks the administrative privileges required to install a service.</exception>
        /// <exception cref="Exception">Thrown if an unexpected error occurs during the SCM communication or file I/O.</exception>
        Task<bool> InstallService(ServiceConfiguration config, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uninstalls the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to uninstall.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<bool> UninstallService(string? serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to start.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<bool> StartService(string? serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to stop.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<bool> StopService(string? serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restarts the specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service to restart.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<bool> RestartService(string? serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports the service configuration to an XML file selected by the user.
        /// </summary>
        /// <param name="confirmPassword">The confirmation of the service account password.</param>
        Task ExportXmlConfig(string? confirmPassword);

        /// <summary>
        /// Exports the service configuration to an JSON file selected by the user.
        /// </summary>
        /// <param name="confirmPassword">The confirmation of the service account password.</param>
        Task ExportJsonConfig(string? confirmPassword);

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
