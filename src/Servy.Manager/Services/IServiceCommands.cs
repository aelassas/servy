using Servy.Manager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Servy.Manager.Services
{
    /// <summary>
    /// Defines commands and operations related to service management.
    /// Provides methods for searching, starting, stopping, configuring,
    /// installing, uninstalling, removing, and importing/exporting services.
    /// </summary>
    public interface IServiceCommands
    {
        /// <summary>
        /// Searches for services matching the specified search text.
        /// </summary>
        /// <param name="searchText">The text to search for in service names.</param>
        /// <returns>A collection of <see cref="Service"/> objects that match the search.</returns>
        Task<IEnumerable<Service>> SearchServicesAsync(string searchText);

        /// <summary>
        /// Starts the specified service.
        /// </summary>
        /// <param name="service">The service to start.</param>
        /// <returns>True if the service started successfully; otherwise, false.</returns>
        Task<bool> StartServiceAsync(Service service);

        /// <summary>
        /// Stops the specified service.
        /// </summary>
        /// <param name="service">The service to stop.</param>
        /// <returns>True if the service stopped successfully; otherwise, false.</returns>
        Task<bool> StopServiceAsync(Service service);

        /// <summary>
        /// Restarts the specified service.
        /// </summary>
        /// <param name="service">The service to restart.</param>
        /// <returns>True if the service restarted successfully; otherwise, false.</returns>
        Task<bool> RestartServiceAsync(Service service);

        /// <summary>
        /// Opens the configuration app for the specified service.
        /// </summary>
        /// <param name="service">The service to configure.</param>
        Task ConfigureServiceAsync(Service service);

        /// <summary>
        /// Installs the specified service, optionally using a custom wrapper executable directory.
        /// </summary>
        /// <param name="service">The service to install.</param>
        /// <returns>True if the service was installed successfully; otherwise, false.</returns>
        Task<bool> InstallServiceAsync(Service service);

        /// <summary>
        /// Uninstalls the specified service.
        /// </summary>
        /// <param name="service">The service to uninstall.</param>
        /// <returns>True if the service was uninstalled successfully; otherwise, false.</returns>
        Task<bool> UninstallServiceAsync(Service service);

        /// <summary>
        /// Removes the specified service from the repository.
        /// </summary>
        /// <param name="service">The service to remove.</param>
        /// <returns>True if the service was removed successfully; otherwise, false.</returns>
        Task<bool> RemoveServiceAsync(Service service);

        /// <summary>
        /// Exports the specified service configuration to an XML file.
        /// </summary>
        /// <param name="service">The service to export.</param>
        Task ExportServiceToXmlAsync(Service service);

        /// <summary>
        /// Exports the specified service configuration to a JSON file.
        /// </summary>
        /// <param name="service">The service to export.</param>
        Task ExportServiceToJsonAsync(Service service);

        /// <summary>
        /// Imports service configurations from an XML file.
        /// </summary>
        Task ImportXmlConfigAsync();

        /// <summary>
        /// Imports service configurations from a JSON file.
        /// </summary>
        Task ImportJsonConfigAsync();
    }
}
