using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Data
{
    /// <summary>
    /// Defines a repository interface for managing <see cref="ServiceDto"/> records
    /// and domain <see cref="Service"/> operations.
    /// </summary>
    public interface IServiceRepository
    {
        // -------------------
        // DTO-based operations
        // -------------------

        /// <summary>
        /// Adds a new <see cref="ServiceDto"/> record to the repository.
        /// </summary>
        /// <param name="service">The DTO representing the service to add.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The ID of the added service.</returns>
        Task<int> AddAsync(ServiceDto service, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing <see cref="ServiceDto"/> record.
        /// </summary>
        /// <param name="service">The DTO containing updated values.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> UpdateAsync(ServiceDto service, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds or updates a <see cref="ServiceDto"/> record depending on whether it exists.
        /// </summary>
        /// <param name="service">The DTO to upsert.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> UpsertAsync(ServiceDto service, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a <see cref="ServiceDto"/> by its database ID.
        /// </summary>
        /// <param name="id">The ID of the service to delete.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a <see cref="ServiceDto"/> by its unique name.
        /// </summary>
        /// <param name="name">The name of the service to delete.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> DeleteAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a <see cref="ServiceDto"/> by its database ID.
        /// </summary>
        /// <param name="id">The ID of the service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        Task<ServiceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a <see cref="ServiceDto"/> by its unique name.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        Task<ServiceDto> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all <see cref="ServiceDto"/> records in the repository.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A collection of all service DTOs.</returns>
        Task<IEnumerable<ServiceDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for <see cref="ServiceDto"/> records containing the specified keyword
        /// in their name or description.
        /// </summary>
        /// <param name="keyword">The keyword to search for.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A collection of matching <see cref="ServiceDto"/> records.</returns>
        Task<IEnumerable<ServiceDto>> Search(string keyword, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a <see cref="ServiceDto"/> to an XML string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An XML string representing the service.</returns>
        Task<string> ExportXML(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a <see cref="ServiceDto"/> from an XML string.
        /// </summary>
        /// <param name="xml">The XML data to import.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if import succeeded; otherwise, <c>false</c>.</returns>
        Task<bool> ImportXML(string xml, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a <see cref="ServiceDto"/> to a JSON string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A JSON string representing the service.</returns>
        Task<string> ExportJSON(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a <see cref="ServiceDto"/> from a JSON string.
        /// </summary>
        /// <param name="json">The JSON data to import.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if import succeeded; otherwise, <c>false</c>.</returns>
        Task<bool> ImportJSON(string json, CancellationToken cancellationToken = default);

        // ------------------------
        // Domain service operations
        // ------------------------

        /// <summary>
        /// Adds a new domain <see cref="Service"/> to the repository.
        /// </summary>
        /// <param name="service">The domain service to add.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The ID of the added service.</returns>
        Task<int> AddDomainServiceAsync(Service service, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing domain <see cref="Service"/>.
        /// </summary>
        /// <param name="service">The domain service containing updated values.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> UpdateDomainServiceAsync(Service service, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds or updates a domain <see cref="Service"/> depending on whether it exists.
        /// </summary>
        /// <param name="service">The domain service to upsert.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> UpsertDomainServiceAsync(Service service, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a domain <see cref="Service"/> by ID.
        /// </summary>
        /// <param name="id">The ID of the service to delete.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> DeleteDomainServiceAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a domain <see cref="Service"/> by name.
        /// </summary>
        /// <param name="name">The name of the service to delete.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> DeleteDomainServiceAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a domain <see cref="Service"/> by ID.
        /// </summary>
        /// <param name="serviceManager">The service manager used to manage the service.</param>
        /// <param name="id">The ID of the service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="Service"/> or <c>null</c> if not found.</returns>
        Task<Service> GetDomainServiceByIdAsync(IServiceManager serviceManager, int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a domain <see cref="Service"/> by name.
        /// </summary>
        /// <param name="serviceManager">The service manager used to manage the service.</param>
        /// <param name="name">The name of the service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="Service"/> or <c>null</c> if not found.</returns>
        Task<Service> GetDomainServiceByNameAsync(IServiceManager serviceManager, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all domain <see cref="Service"/> objects.
        /// </summary>
        /// <param name="serviceManager">The service manager used to manage services.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A collection of all domain services.</returns>
        Task<IEnumerable<Service>> GetAllDomainServicesAsync(IServiceManager serviceManager, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for domain <see cref="Service"/> objects containing the specified keyword
        /// in their name or description.
        /// </summary>
        /// <param name="serviceManager">The service manager used to manage services.</param>
        /// <param name="keyword">The keyword to search for.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A collection of matching domain services.</returns>
        Task<IEnumerable<Service>> SearchDomainServicesAsync(IServiceManager serviceManager, string keyword, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a domain <see cref="Service"/> to an XML string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An XML string representing the domain service.</returns>
        Task<string> ExportDomainServiceXMLAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a domain <see cref="Service"/> from an XML string.
        /// </summary>
        /// <param name="xml">The XML data to import.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if import succeeded; otherwise, <c>false</c>.</returns>
        Task<bool> ImportDomainServiceXMLAsync(string xml, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a domain <see cref="Service"/> to a JSON string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A JSON string representing the domain service.</returns>
        Task<string> ExportDomainServiceJSONAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a domain <see cref="Service"/> from a JSON string.
        /// </summary>
        /// <param name="json">The JSON data to import.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if import succeeded; otherwise, <c>false</c>.</returns>
        Task<bool> ImportDomainServiceJSONAsync(string json, CancellationToken cancellationToken = default);
    }
}
