using Servy.Core.DTOs;

namespace Servy.Core.Data
{
    /// <summary>
    /// Defines a repository interface for managing <see cref="ServiceDto"/> records.
    /// Provides methods to add, update, upsert, delete, and query services.
    /// </summary>
    public interface IServiceRepository
    {
        /// <summary>
        /// Adds a new service to the database.
        /// </summary>
        /// <param name="service">The service DTO to add.</param>
        /// <returns>The newly created service ID.</returns>
        Task<int> AddAsync(ServiceDto service);

        /// <summary>
        /// Updates an existing service in the database.
        /// </summary>
        /// <param name="service">The service DTO containing updated data.</param>
        /// <returns>The number of affected rows.</returns>
        Task<int> UpdateAsync(ServiceDto service);

        /// <summary>
        /// Inserts a new service if it does not exist, or updates it if it already exists.
        /// </summary>
        /// <param name="service">The service DTO to upsert.</param>
        /// <returns>The number of affected rows or the ID of the inserted service.</returns>
        Task<int> UpsertAsync(ServiceDto service);

        /// <summary>
        /// Deletes a service by its numeric ID.
        /// </summary>
        /// <param name="id">The ID of the service to delete.</param>
        /// <returns>The number of affected rows.</returns>
        Task<int> DeleteAsync(int id);

        /// <summary>
        /// Deletes a service by its name.
        /// </summary>
        /// <param name="name">The name of the service to delete.</param>
        /// <returns>The number of affected rows.</returns>
        Task<int> DeleteAsync(string name);

        /// <summary>
        /// Retrieves a service by its numeric ID.
        /// </summary>
        /// <param name="id">The ID of the service to retrieve.</param>
        /// <returns>The service DTO if found; otherwise, null.</returns>
        Task<ServiceDto?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves a service by its name.
        /// </summary>
        /// <param name="name">The name of the service to retrieve.</param>
        /// <returns>The service DTO if found; otherwise, null.</returns>
        Task<ServiceDto?> GetByNameAsync(string name);

        /// <summary>
        /// Retrieves all services from the database.
        /// </summary>
        /// <returns>A collection of all <see cref="ServiceDto"/> objects.</returns>
        Task<IEnumerable<ServiceDto>> GetAllAsync();

        /// <summary>
        /// Searches for services where the name or description matches the specified keyword.
        /// </summary>
        /// <param name="keyword">The keyword to search for (case-insensitive).</param>
        /// <returns>A collection of matching <see cref="ServiceDto"/> objects.</returns>
        Task<IEnumerable<ServiceDto>> Search(string keyword);

        /// <summary>
        /// Exports the service configuration with the specified name as an XML string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the serialized XML string of the service.
        /// Returns an empty string if the service is not found.
        /// </returns>
        Task<string> ExportXML(string name);

        /// <summary>
        /// Imports a service configuration from an XML string and saves it to the database.
        /// If a service with the same name exists, it is updated; otherwise, it is inserted.
        /// </summary>
        /// <param name="xml">The XML string representing the <see cref="ServiceDto"/> to import.</param>
        /// <returns>
        /// A task representing the asynchronous operation, containing <c>true</c> if the import was successful; otherwise, <c>false</c>.
        /// </returns>
        Task<bool> ImportXML(string xml);

        /// <summary>
        /// Exports the service configuration with the specified name as a JSON string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the serialized JSON string of the service.
        /// Returns an empty string if the service is not found.
        /// </returns>
        Task<string> ExportJSON(string name);

        /// <summary>
        /// Imports a service configuration from a JSON string and saves it to the database.
        /// If a service with the same name exists, it is updated; otherwise, it is inserted.
        /// </summary>
        /// <param name="json">The JSON string representing the <see cref="ServiceDto"/> to import.</param>
        /// <returns>
        /// A task representing the asynchronous operation, containing <c>true</c> if the import was successful; otherwise, <c>false</c>.
        /// </returns>
        Task<bool> ImportJSON(string json);
    }
}
