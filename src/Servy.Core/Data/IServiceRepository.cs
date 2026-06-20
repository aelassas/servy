using Servy.Core.DTOs;

namespace Servy.Core.Data
{
    /// <summary>
    /// Defines a repository interface for managing <see cref="ServiceDto"/> records.
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
        /// <param name="preserveExistingRuntimeState">Required flag to preserve runtime state (PID, ActiveStdoutPath, ActiveStderrPath, PreviousStopTimeout).</param>
        /// <param name="preserveExistingCredentials">Required flag to preserve existing credentials (RunAsLocalSystem, UserAccount, Password).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of affected records.</returns>
        Task<int> UpdateAsync(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing <see cref="ServiceDto"/> record.
        /// </summary>
        /// <param name="service">The DTO containing updated values.</param>
        /// <param name="preserveExistingRuntimeState">Required flag to preserve runtime state (PID, ActiveStdoutPath, ActiveStderrPath, PreviousStopTimeout).</param>
        /// <param name="preserveExistingCredentials">Required flag to preserve existing credentials (RunAsLocalSystem, UserAccount, Password).</param>
        /// <returns>The number of affected records.</returns>
        int Update(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials);

        /// <summary>
        /// Adds or updates a <see cref="ServiceDto"/> record depending on whether it exists.
        /// </summary>
        /// <param name="service">The DTO to upsert.</param>
        /// <param name="preserveExistingRuntimeState">Required flag to preserve runtime state (PID, ActiveStdoutPath, ActiveStderrPath, PreviousStopTimeout).</param>
        /// <param name="preserveExistingCredentials">Required flag to preserve existing credentials (RunAsLocalSystem, UserAccount, Password).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The ID of the upserted service.</returns>
        Task<int> UpsertAsync(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously inserts or updates a collection of services in the database.
        /// Preserves runtime state and credentials for each incoming DTO.
        /// </summary>
        /// <param name="services">The collection of <see cref="ServiceDto"/> objects to be persisted.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the total number of rows affected.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method iterates through the collection and executes individual upsert commands. 
        /// </para>
        /// <para>
        /// After the database write, the method attempts to synchronize the auto-incremented 
        /// IDs back to the original <see cref="ServiceDto"/> objects in chunks.
        /// </para>
        /// </remarks>
        Task<int> UpsertBatchAsync(IEnumerable<ServiceDto> services, CancellationToken cancellationToken = default);

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
        Task<int> DeleteAsync(string? name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a <see cref="ServiceDto"/> by its database ID.
        /// </summary>
        /// <param name="id">The ID of the service.</param>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        Task<ServiceDto?> GetByIdAsync(int id, bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a <see cref="ServiceDto"/> by its unique name.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        Task<ServiceDto?> GetByNameAsync(string? name, bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a <see cref="ServiceDto"/> by its unique name.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        ServiceDto? GetByName(string? name, bool decrypt = true);

        /// <summary>
        /// Lightweight query to fetch only the Process ID (PID) for a given service.
        /// Used by high-frequency UI timers to check running state without allocating full DTOs.
        /// </summary>
        /// <param name="name">The unique name of the service to query.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The PID of the running service, or <c>null</c> if not found or not running.</returns>
        Task<int?> GetServicePidAsync(string? name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a lightweight projection of a service's running state.
        /// </summary>
        /// <param name="name">The unique name of the service to query.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="ServiceConsoleStateDto"/> containing the PID and active log paths; 
        /// or <see langword="null"/> if the service is not found.
        /// </returns>
        /// <remarks>
        /// This method is optimized for high-frequency UI polling (e.g., in the Console tab).
        /// It fetches only the columns necessary to determine if a service has restarted 
        /// or changed its active log targets, minimizing database I/O and memory allocations.
        /// </remarks>
        Task<ServiceConsoleStateDto?> GetServiceConsoleStateAsync(string? name, CancellationToken cancellationToken = default);

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
        Task<IEnumerable<ServiceDto>> SearchAsync(string? keyword, bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a <see cref="ServiceDto"/> to an XML string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An XML string representing the service, or <see cref="string.Empty"/> if
        /// <paramref name="name"/> is null/whitespace or no matching service exists.</returns>
        Task<string> ExportXmlAsync(string? name, CancellationToken cancellationToken = default);

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
        /// <returns>A JSON string representing the service, or <see cref="string.Empty"/> if
        /// <paramref name="name"/> is null/whitespace or no matching service exists.</returns>
        Task<string> ExportJsonAsync(string? name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a <see cref="ServiceDto"/> from a JSON string.
        /// </summary>
        /// <param name="json">The JSON data to import.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if import succeeded; otherwise, <c>false</c>.</returns>
        Task<bool> ImportJsonAsync(string json, CancellationToken cancellationToken = default);
    }
}
