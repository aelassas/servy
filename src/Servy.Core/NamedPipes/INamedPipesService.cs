using Servy.Core.DTOs;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Defines an asynchronous contract for reading and writing length-prefixed JSON payloads over IPC streams.
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
        Task<T?> ReadAsync<T>(Stream stream, CancellationToken ct = default);

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
        T? Read<T>(Stream stream, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a service configuration by its name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The service configuration or <see langword="null"/> if not found.</returns>
        ServiceDto? GetByName(string serviceName, CancellationToken ct = default);

        /// <summary>
        /// Updates a service configuration.
        /// </summary>
        /// <param name="serviceDto">The service configuration to update.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The number of records updated.</returns>
        int Update(ServiceDto serviceDto, CancellationToken ct = default);
    }
}
