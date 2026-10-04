using Servy.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Data
{
    /// <summary>
    /// Defines a repository interface for managing <see cref="ServiceDto"/> records.
    /// </summary>
    public interface IServiceRepository
    {
        /// <summary>
        /// Adds or updates a <see cref="ServiceDto"/> record depending on whether it exists.
        /// </summary>
        /// <param name="service">The DTO to upsert.</param>
        /// <param name="preserveExistingRuntimeState">Required flag to preserve runtime state (PID, ActiveStdoutPath, ActiveStderrPath, PreviousStopTimeout, RestartAttempts and its timestamp).</param>
        /// <param name="preserveExistingCredentials">Required flag to preserve existing credentials (RunAsLocalSystem, UserAccount, Password).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The ID of the upserted service.</returns>
        /// <exception cref="Servy.Core.Security.ServiceDecryptionFailedException">Thrown when the stored row has a sensitive field that no longer decrypts with the current key, or <paramref name="service"/> was read from such a row; nothing is written.</exception>
        Task<int> UpsertAsync(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken = default);

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
        /// Retrieves a <see cref="ServiceDto"/> by its unique name.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="decrypt">Optional flag to decrypt sensitive data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The matching <see cref="ServiceDto"/> or <c>null</c> if not found.</returns>
        Task<ServiceDto> GetByNameAsync(string name, bool decrypt = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lightweight query to fetch only the Process ID (PID) for a given service.
        /// Used by high-frequency UI timers to check running state without allocating full DTOs.
        /// </summary>
        /// <param name="name">The unique name of the service to query.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The PID of the running service, or <c>null</c> if not found or not running.</returns>
        Task<int?> GetServicePidAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a lightweight projection of a service's running state.
        /// </summary>
        /// <param name="name">The unique name of the service to query.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="ServiceConsoleStateDto"/> containing the PID and active log paths;
        /// or <see langword="null"/> if the service is not found.
        /// </returns>
        /// <remarks>
        /// This method is optimized for high-frequency UI polling (e.g., in the Console tab).
        /// It fetches only the columns necessary to determine if a service has restarted
        /// or changed its active log targets, minimizing database I/O and memory allocations.
        /// </remarks>
        Task<ServiceConsoleStateDto> GetServiceConsoleStateAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes only the runtime state columns of a service's row: <c>Pid</c>, <c>ActiveStdoutPath</c>,
        /// <c>ActiveStderrPath</c>, and <c>PreviousStopTimeout</c> when <see cref="ServiceRuntimeStateDto.UpdatePreviousStopTimeout"/> is set.
        /// The configuration columns are never touched.
        /// </summary>
        /// <param name="name">The unique name of the service.</param>
        /// <param name="state">The runtime state to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of rows updated; 0 when the service has no row.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
        Task<int> UpdateRuntimeStateAsync(string name, ServiceRuntimeStateDto state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes only the two metadata columns a Manager refresh tick keeps in sync with the service control manager:
        /// <c>Description</c> and <c>StartupType</c>. The sensitive columns are never touched, so a row read for display
        /// cannot be written back from a degraded in-memory copy.
        /// </summary>
        /// <param name="name">The unique name of the service.</param>
        /// <param name="description">The description to store; may be <see langword="null"/>.</param>
        /// <param name="startupType">The startup type to store, or <see langword="null"/> to leave the stored value as it is.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of rows updated; 0 when the service has no row.</returns>
        Task<int> UpdateDescriptionAndStartupTypeAsync(string name, string description, int? startupType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the restart attempts counter of a service.
        /// </summary>
        /// <param name="name">The unique name of the service.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The counter (0 when never written) and the UTC time it was last written; <see langword="null"/> when the service has no row.</returns>
        Task<RestartAttemptsDto> GetRestartAttemptsAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the restart attempts counter of a service and the time it was written.
        /// </summary>
        /// <param name="name">The unique name of the service.</param>
        /// <param name="attempts">The counter value; must not be negative.</param>
        /// <param name="updatedAtUtc">The UTC time of the write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The number of rows updated; 0 when the service has no row.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="attempts"/> is negative.</exception>
        Task<int> UpdateRestartAttemptsAsync(string name, int attempts, DateTime updatedAtUtc, CancellationToken cancellationToken = default);

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
        /// <returns>An XML string representing the service, or <see cref="string.Empty"/> if
        /// <paramref name="name"/> is null/whitespace or no matching service exists.</returns>
        /// <exception cref="Servy.Core.Security.ServiceDecryptionFailedException">Thrown when the stored row has a sensitive field that no longer decrypts with the current key; the export would otherwise be written without its parameters and environment variables.</exception>
        Task<string> ExportXmlAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a <see cref="ServiceDto"/> to a JSON string.
        /// </summary>
        /// <param name="name">The name of the service to export.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A JSON string representing the service, or <see cref="string.Empty"/> if
        /// <paramref name="name"/> is null/whitespace or no matching service exists.</returns>
        /// <exception cref="Servy.Core.Security.ServiceDecryptionFailedException">Thrown when the stored row has a sensitive field that no longer decrypts with the current key; the export would otherwise be written without its parameters and environment variables.</exception>
        Task<string> ExportJsonAsync(string name, CancellationToken cancellationToken = default);

    }
}
