using Servy.Core.DTOs;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Defines the client side of the Servy host named pipe: length-prefixed JSON framing over IPC streams, and the
    /// requests a wrapper (or an administrator) sends to the Servy host service.
    /// </summary>
    public interface INamedPipesService
    {
        /// <summary>
        /// Asynchronously writes a length-prefixed JSON-serialized object to the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        /// <param name="stream">The target pipe stream.</param>
        /// <param name="dto">The object instance to write.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        Task WriteAsync<T>(Stream stream, T dto, CancellationToken ct = default);

        /// <summary>
        /// Asynchronously reads a length-prefixed JSON-serialized object from the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="stream">The source pipe stream.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous read operation, returning the deserialized object instance or <see langword="null"/> if the read fails or the stream ends.</returns>
        Task<T> ReadAsync<T>(Stream stream, CancellationToken ct = default);

        /// <summary>
        /// Writes a length-prefixed JSON-serialized object to the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        /// <param name="stream">The target pipe stream.</param>
        /// <param name="dto">The object instance to write.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        void Write<T>(Stream stream, T dto, CancellationToken ct = default);

        /// <summary>
        /// Reads a length-prefixed JSON-serialized object from the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="stream">The source pipe stream.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The deserialized object instance or <see langword="null"/> if the read fails or the stream ends.</returns>
        T Read<T>(Stream stream, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a service configuration by its name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The service configuration or <see langword="null"/> if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null or blank.</exception>
        /// <exception cref="TimeoutException">Thrown when the host cannot be reached or does not answer in time.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the pipe is not served by the Servy host service.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the host refuses or fails the request.</exception>
        ServiceDto GetByName(string serviceName, CancellationToken ct = default);

        /// <summary>
        /// Persists the runtime state of a service: its child PID, the previous stop timeout and the active stdout/stderr paths.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="state">The runtime state to persist.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The number of records updated.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null or blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException">Thrown when the host cannot be reached or does not answer in time.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the pipe is not served by the Servy host service.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the host refuses or fails the request.</exception>
        int UpdateRuntimeState(string serviceName, ServiceRuntimeStateDto state, CancellationToken ct = default);

        /// <summary>
        /// Asynchronously reads the persisted restart attempts counter of a service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The counter and the time it was last written; 0 attempts and no time when the service has none.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null or blank.</exception>
        /// <exception cref="TimeoutException">Thrown when the host cannot be reached or does not answer in time.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the pipe is not served by the Servy host service.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the host refuses or fails the request.</exception>
        Task<RestartAttemptsDto> GetRestartAttemptsAsync(string serviceName, CancellationToken ct = default);

        /// <summary>
        /// Asynchronously persists the restart attempts counter of a service. The host stamps the write with the current
        /// UTC time, so writing the unchanged count anchors the counter to the current OS session.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="attempts">The counter value; must not be negative.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The number of records updated.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="attempts"/> is negative.</exception>
        /// <exception cref="TimeoutException">Thrown when the host cannot be reached or does not answer in time.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the pipe is not served by the Servy host service.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the host refuses or fails the request.</exception>
        Task<int> UpdateRestartAttemptsAsync(string serviceName, int attempts, CancellationToken ct = default);

        /// <summary>
        /// Asks the Servy host service to rebuild the DACL of its named pipe from the accounts of the installed services,
        /// so an account that was just granted (or revoked) access can (or can no longer) connect. Requires an elevated caller.
        /// </summary>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// <see langword="true"/> when the host rebuilt the DACL; <see langword="false"/> when the host is not running or
        /// refused the request. The host also rebuilds the DACL from the database every time it starts, so a host that is
        /// not running picks the change up then.
        /// </returns>
        Task<bool> RefreshPipeAccessAsync(CancellationToken ct = default);
    }
}
