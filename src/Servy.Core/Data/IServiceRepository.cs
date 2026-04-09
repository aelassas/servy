using Servy.Core.Domain;
using Servy.Core.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Data
{
    /// <summary>
    /// Defines a repository interface for managing <see cref="ServiceDto"/> records operations.
    /// </summary>
    public interface IServiceRepository
    {
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
        /// Updates an existing <see cref="ServiceDto"/> record.
        /// </summary>
        /// <param name="service">The DTO containing updated values.</param>
        /// <returns>The number of affected records.</returns>
        int Update(ServiceDto service);

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
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        Task<ServiceDto> GetByIdAsync(int id, bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a <see cref="ServiceDto"/> by its unique name.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        Task<ServiceDto> GetByNameAsync(string name, bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a <see cref="ServiceDto"/> by its unique name.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        ServiceDto GetByName(string name, bool decrypt = true);

        /// <summary>
        /// Retrieves all <see cref="ServiceDto"/> records in the repository.
        /// </summary>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A collection of all service DTOs.</returns>
        Task<IEnumerable<ServiceDto>> GetAllAsync(bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for <see cref="ServiceDto"/> records containing the specified keyword
        /// in their name or description.
        /// </summary>
        /// <param name="keyword">The keyword to search for.</param>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A collection of matching <see cref="ServiceDto"/> records.</returns>
        Task<IEnumerable<ServiceDto>> SearchAsync(string keyword, bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a <see cref="ServiceDto"/> to an XML string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An XML string representing the service.</returns>
        Task<string> ExportXmlAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a <see cref="ServiceDto"/> from an XML string.
        /// </summary>
        /// <param name="xml">The XML data to import.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if import succeeded; otherwise, <c>false</c>.</returns>
        Task<bool> ImportXmlAsync(string xml, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a <see cref="ServiceDto"/> to a JSON string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A JSON string representing the service.</returns>
        Task<string> ExportJsonAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a <see cref="ServiceDto"/> from a JSON string.
        /// </summary>
        /// <param name="json">The JSON data to import.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if import succeeded; otherwise, <c>false</c>.</returns>
        Task<bool> ImportJsonAsync(string json, CancellationToken cancellationToken = default);
    }
}
