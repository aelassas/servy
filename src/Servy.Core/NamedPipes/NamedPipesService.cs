using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Provides synchronous and asynchronous serialization and IPC communication helpers over Named Pipe streams.
    /// </summary>
    public class NamedPipesService : INamedPipesService
    {
        private readonly string _namedPipesName;

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipesService"/> class using default configuration.
        /// </summary>
        public NamedPipesService()
            : this(AppConfig.ServyHostNamedPipeName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipesService"/> class with a specified named pipe name.
        /// </summary>
        /// <param name="namedPipesName">The name of the target named pipe.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="namedPipesName"/> is <see langword="null"/>.</exception>
        public NamedPipesService(string namedPipesName)
        {
            _namedPipesName = namedPipesName ?? throw new ArgumentNullException(nameof(namedPipesName));
        }

        /// <inheritdoc />
        public async Task WriteAsync<T>(Stream stream, T dto, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            ct.ThrowIfCancellationRequested();

            var (lengthPrefix, jsonBytes) = PrepareFrame(dto);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length, ct).ConfigureAwait(false);
            await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<T> ReadAsync<T>(Stream stream, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            ct.ThrowIfCancellationRequested();

            byte[] lengthPrefix = new byte[4];
            int bytesRead = await ReadExactAsync(stream, lengthPrefix, 0, 4, ct).ConfigureAwait(false);
            if (bytesRead < 4)
                return default;

            int messageLength = BitConverter.ToInt32(lengthPrefix, 0);
            if (messageLength <= 0 || messageLength > AppConfig.ServyHostMaxAllowedMessageSizeBytes)
                return default;

            byte[] jsonBytes = new byte[messageLength];
            int payloadRead = await ReadExactAsync(stream, jsonBytes, 0, messageLength, ct).ConfigureAwait(false);
            if (payloadRead < messageLength)
                return default;

            return DeserializePayload<T>(jsonBytes);
        }

        /// <inheritdoc />
        public void Write<T>(Stream stream, T dto, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            ct.ThrowIfCancellationRequested();

            var (lengthPrefix, jsonBytes) = PrepareFrame(dto);

            stream.Write(lengthPrefix, 0, lengthPrefix.Length);
            stream.Write(jsonBytes, 0, jsonBytes.Length);
            stream.Flush();
        }

        /// <inheritdoc />
        public T Read<T>(Stream stream, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            ct.ThrowIfCancellationRequested();

            byte[] lengthPrefix = new byte[4];
            int bytesRead = ReadExact(stream, lengthPrefix, 0, 4, ct);
            if (bytesRead < 4)
                return default;

            int messageLength = BitConverter.ToInt32(lengthPrefix, 0);
            if (messageLength <= 0 || messageLength > AppConfig.ServyHostMaxAllowedMessageSizeBytes)
                return default;

            byte[] jsonBytes = new byte[messageLength];
            int payloadRead = ReadExact(stream, jsonBytes, 0, messageLength, ct);
            if (payloadRead < messageLength)
                return default;

            return DeserializePayload<T>(jsonBytes);
        }

        /// <inheritdoc />
        public ServiceDto GetByName(string serviceName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty.", nameof(serviceName));

            var request = new IpcRequestDto
            {
                Action = AppConfig.ServyHostGetByNameAction,
                ServiceName = serviceName
            };

            var response = ExecuteIpcRequest(request, ct);

            if (response != null && response.Success)
            {
                return response.Data;
            }

            string details = response?.ErrorMessage ?? "No additional error details received.";
            throw new InvalidOperationException($"Failed to retrieve configuration for service '{serviceName}'. Details: {details}");
        }

        /// <inheritdoc />
        public int Update(ServiceDto serviceDto, CancellationToken ct = default)
        {
            if (serviceDto == null) throw new ArgumentNullException(nameof(serviceDto));

            var request = new IpcRequestDto
            {
                Action = AppConfig.ServyHostUpdateAction,
                Data = serviceDto
            };

            var response = ExecuteIpcRequest(request, ct);

            if (response != null && response.Success)
            {
                return response.UpdateData.GetValueOrDefault();
            }

            string details = response?.ErrorMessage ?? "No additional error details received.";
            throw new InvalidOperationException($"Failed to update configuration for service '{serviceDto.Name}'. Details: {details}");
        }

        #region Private Helper Methods

        /// <summary>
        /// Serializes the specified payload to UTF-8 JSON bytes and constructs its 4-byte length prefix.
        /// </summary>
        /// <typeparam name="T">The type of the payload object to serialize.</typeparam>
        /// <param name="dto">The object instance to frame.</param>
        /// <returns>A tuple containing the 4-byte length prefix header and the UTF-8 serialized JSON payload bytes.</returns>
        private (byte[] LengthPrefix, byte[] JsonBytes) PrepareFrame<T>(T dto)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto));
            byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
            return (lengthPrefix, jsonBytes);
        }

        /// <summary>
        /// Deserializes a UTF-8 JSON byte array into an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type to deserialize into.</typeparam>
        /// <param name="jsonBytes">The UTF-8 encoded JSON payload byte array.</param>
        /// <returns>The deserialized object instance, or <see langword="null"/> if deserialization yields no value.</returns>
        private static T DeserializePayload<T>(byte[] jsonBytes)
        {
            string json = Encoding.UTF8.GetString(jsonBytes);
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// Establishes a synchronous Named Pipe connection to the host service, transmits an IPC request, and returns the server's response.
        /// </summary>
        /// <param name="request">The IPC request DTO payload to send.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="IpcResponseDto"/> returned by the host service, or <see langword="null"/> if no response is received.</returns>
        /// <exception cref="TimeoutException">Thrown when the connection attempt to the Named Pipe times out.</exception>
        private IpcResponseDto ExecuteIpcRequest(IpcRequestDto request, CancellationToken ct)
        {
            using (var clientStream = new NamedPipeClientStream(".", _namedPipesName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                try
                {
                    clientStream.Connect(AppConfig.ServyHostDefaultConnectTimeoutMs);
                }
                catch (TimeoutException ex)
                {
                    throw new TimeoutException($"Connection to primary Servy host named pipe '{_namedPipesName}' timed out after {AppConfig.ServyHostDefaultConnectTimeoutMs} ms.", ex);
                }

                Write(clientStream, request, ct);
                return Read<IpcResponseDto>(clientStream, ct);
            }
        }

        /// <summary>
        /// Asynchronously reads the exact number of bytes requested from the stream into the target buffer, handling partial reads.
        /// </summary>
        /// <param name="stream">The source stream to read from.</param>
        /// <param name="buffer">The target byte array to store the read bytes.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing data.</param>
        /// <param name="count">The exact number of bytes to read from the stream.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous read operation. The task result contains the total number of bytes read into the buffer.</returns>
        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                ct.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct).ConfigureAwait(false);
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }

        /// <summary>
        /// Synchronously reads the exact number of bytes requested from the stream into the target buffer, handling partial reads.
        /// </summary>
        /// <param name="stream">The source stream to read from.</param>
        /// <param name="buffer">The target byte array to store the read bytes.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing data.</param>
        /// <param name="count">The exact number of bytes to read from the stream.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        private static int ReadExact(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                ct.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }

        #endregion
    }
}
