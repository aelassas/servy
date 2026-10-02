using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Services;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Provides synchronous and asynchronous serialization and IPC communication helpers over Named Pipe streams.
    /// </summary>
    public class NamedPipesService : INamedPipesService
    {
        /// <summary>
        /// The JSON settings of every frame: no type names (a frame can never choose the type it is read as), a bounded
        /// depth, and UTC dates.
        /// </summary>
        private static readonly JsonSerializerSettings FrameJsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 32,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        private readonly string _namedPipesName;
        private readonly INamedPipeServerVerifier _serverVerifier;
        private readonly int _connectTimeoutMs;
        private readonly int _requestTimeoutMs;

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipesService"/> class using default configuration: the
        /// <see cref="AppConfig.ServyHostNamedPipeName"/> pipe, served by the <see cref="AppConfig.ServyHostServiceName"/> service.
        /// </summary>
        public NamedPipesService()
            : this(AppConfig.ServyHostNamedPipeName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipesService"/> class with a specified named pipe name,
        /// served by the <see cref="AppConfig.ServyHostServiceName"/> service.
        /// </summary>
        /// <param name="namedPipesName">The name of the target named pipe.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="namedPipesName"/> is <see langword="null"/>.</exception>
        public NamedPipesService(string namedPipesName)
            : this(namedPipesName, new ServyHostServerVerifier(new WindowsServiceApi()))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipesService"/> class.
        /// </summary>
        /// <param name="namedPipesName">The name of the target named pipe.</param>
        /// <param name="serverVerifier">Verifies the server end of every connection before a request is written to it.</param>
        /// <param name="connectTimeoutMs">The time to wait for the pipe to accept the connection.</param>
        /// <param name="requestTimeoutMs">The time to wait for the answer once connected.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="namedPipesName"/> or <paramref name="serverVerifier"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a timeout is not positive.</exception>
        public NamedPipesService(
            string namedPipesName,
            INamedPipeServerVerifier serverVerifier,
            int connectTimeoutMs = AppConfig.ServyHostDefaultConnectTimeoutMs,
            int requestTimeoutMs = AppConfig.ServyHostDefaultRequestTimeoutMs)
        {
            _namedPipesName = namedPipesName ?? throw new ArgumentNullException(nameof(namedPipesName));
            _serverVerifier = serverVerifier ?? throw new ArgumentNullException(nameof(serverVerifier));
            if (connectTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(connectTimeoutMs), "The connect timeout must be positive.");
            if (requestTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(requestTimeoutMs), "The request timeout must be positive.");
            _connectTimeoutMs = connectTimeoutMs;
            _requestTimeoutMs = requestTimeoutMs;
        }

        /// <inheritdoc />
        public async Task WriteAsync<T>(Stream stream, T dto, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            ct.ThrowIfCancellationRequested();

            var (lengthPrefix, jsonBytes) = PrepareFrame(dto);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length, ct);
            await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length, ct);
            await stream.FlushAsync(ct);
        }

        /// <inheritdoc />
        public async Task<T?> ReadAsync<T>(Stream stream, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            ct.ThrowIfCancellationRequested();

            byte[] lengthPrefix = new byte[4];
            int bytesRead = await ReadExactAsync(stream, lengthPrefix, 0, 4, ct);
            if (bytesRead < 4)
                return default;

            int messageLength = BitConverter.ToInt32(lengthPrefix, 0);
            if (!IsValidMessageLength(messageLength))
                return default;

            byte[] jsonBytes = new byte[messageLength];
            int payloadRead = await ReadExactAsync(stream, jsonBytes, 0, messageLength, ct);
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
        public T? Read<T>(Stream stream, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            ct.ThrowIfCancellationRequested();

            byte[] lengthPrefix = new byte[4];
            int bytesRead = ReadExact(stream, lengthPrefix, 0, 4, ct);
            if (bytesRead < 4)
                return default;

            int messageLength = BitConverter.ToInt32(lengthPrefix, 0);
            if (!IsValidMessageLength(messageLength))
                return default;

            byte[] jsonBytes = new byte[messageLength];
            int payloadRead = ReadExact(stream, jsonBytes, 0, messageLength, ct);
            if (payloadRead < messageLength)
                return default;

            return DeserializePayload<T>(jsonBytes);
        }

        /// <inheritdoc />
        public ServiceDto? GetByName(string serviceName, CancellationToken ct = default)
        {
            ValidateServiceName(serviceName);

            var request = new IpcRequestDto
            {
                Action = AppConfig.ServyHostGetByNameAction,
                ServiceName = serviceName
            };

            var response = Send(request, ct);
            EnsureSuccess(response, $"Failed to retrieve configuration for service '{serviceName}'.");
            return response!.Data;
        }

        /// <inheritdoc />
        public int UpdateRuntimeState(string serviceName, ServiceRuntimeStateDto state, CancellationToken ct = default)
        {
            ValidateServiceName(serviceName);
            if (state == null) throw new ArgumentNullException(nameof(state));

            var request = new IpcRequestDto
            {
                Action = AppConfig.ServyHostUpdateRuntimeStateAction,
                ServiceName = serviceName,
                RuntimeState = state
            };

            var response = Send(request, ct);
            EnsureSuccess(response, $"Failed to update the runtime state of service '{serviceName}'.");
            return response!.UpdateData.GetValueOrDefault();
        }

        /// <inheritdoc />
        public async Task<RestartAttemptsDto> GetRestartAttemptsAsync(string serviceName, CancellationToken ct = default)
        {
            ValidateServiceName(serviceName);

            var request = new IpcRequestDto
            {
                Action = AppConfig.ServyHostGetRestartAttemptsAction,
                ServiceName = serviceName
            };

            var response = await ExecuteIpcRequestAsync(request, ct);
            EnsureSuccess(response, $"Failed to read the restart attempts of service '{serviceName}'.");
            return response!.RestartAttempts ?? new RestartAttemptsDto();
        }

        /// <inheritdoc />
        public async Task<int> UpdateRestartAttemptsAsync(string serviceName, int attempts, CancellationToken ct = default)
        {
            ValidateServiceName(serviceName);
            if (attempts < 0) throw new ArgumentOutOfRangeException(nameof(attempts), "The restart attempts counter cannot be negative.");

            var request = new IpcRequestDto
            {
                Action = AppConfig.ServyHostUpdateRestartAttemptsAction,
                ServiceName = serviceName,
                RestartAttempts = attempts
            };

            var response = await ExecuteIpcRequestAsync(request, ct);
            EnsureSuccess(response, $"Failed to save the restart attempts of service '{serviceName}'.");
            return response!.UpdateData.GetValueOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> RefreshPipeAccessAsync(CancellationToken ct = default)
        {
            var request = new IpcRequestDto { Action = AppConfig.ServyHostRefreshPipeAccessAction };

            try
            {
                var response = await ExecuteIpcRequestAsync(request, ct);
                return response != null && response.Success;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is TimeoutException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // The host is not running (or not reachable): it rebuilds the DACL from the database when it starts.
                return false;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Validates a service name argument.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null or blank.</exception>
        private static void ValidateServiceName(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty.", nameof(serviceName));
        }

        /// <summary>
        /// Throws when a response is missing or reports a failure.
        /// </summary>
        /// <param name="response">The host's response, or <see langword="null"/> when it sent none.</param>
        /// <param name="failureMessage">The first sentence of the exception message.</param>
        /// <exception cref="InvalidOperationException">Thrown when the response is missing or not successful.</exception>
        private static void EnsureSuccess(IpcResponseDto? response, string failureMessage)
        {
            if (response != null && response.Success)
                return;

            string details = response?.ErrorMessage ?? "No additional error details received.";
            throw new InvalidOperationException($"{failureMessage} Details: {details}");
        }

        /// <summary>
        /// Determines whether a frame's length prefix is acceptable.
        /// </summary>
        /// <param name="messageLength">The length read from the prefix.</param>
        /// <returns><see langword="true"/> for a positive length no larger than <see cref="AppConfig.ServyHostMaxAllowedMessageSizeBytes"/>.</returns>
        private static bool IsValidMessageLength(int messageLength)
            => messageLength > 0 && messageLength <= AppConfig.ServyHostMaxAllowedMessageSizeBytes;

        /// <summary>
        /// Serializes the specified payload to UTF-8 JSON bytes and constructs its 4-byte length prefix.
        /// </summary>
        /// <typeparam name="T">The type of the payload object to serialize.</typeparam>
        /// <param name="dto">The object instance to frame.</param>
        /// <returns>A tuple containing the 4-byte length prefix header and the UTF-8 serialized JSON payload bytes.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the serialized payload is larger than <see cref="AppConfig.ServyHostMaxAllowedMessageSizeBytes"/>,
        /// which the reading end would refuse.</exception>
        private static (byte[] LengthPrefix, byte[] JsonBytes) PrepareFrame<T>(T dto)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto, FrameJsonSettings));
            if (jsonBytes.Length > AppConfig.ServyHostMaxAllowedMessageSizeBytes)
                throw new InvalidOperationException($"The IPC message is {jsonBytes.Length} bytes, above the {AppConfig.ServyHostMaxAllowedMessageSizeBytes}-byte limit.");

            byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
            return (lengthPrefix, jsonBytes);
        }

        /// <summary>
        /// Deserializes a UTF-8 JSON byte array into an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type to deserialize into.</typeparam>
        /// <param name="jsonBytes">The UTF-8 encoded JSON payload byte array.</param>
        /// <returns>The deserialized object instance, or <see langword="null"/> if deserialization yields no value or the payload is not valid JSON.</returns>
        private static T? DeserializePayload<T>(byte[] jsonBytes)
        {
            try
            {
                string json = Encoding.UTF8.GetString(jsonBytes);
                return JsonConvert.DeserializeObject<T>(json, FrameJsonSettings);
            }
            catch (JsonException)
            {
                // A malformed frame is treated like a truncated one: no message
                return default;
            }
        }

        /// <summary>
        /// Sends a request synchronously; see <see cref="ExecuteIpcRequestAsync"/>.
        /// </summary>
        /// <param name="request">The request to send.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The host's response, or <see langword="null"/> if none was received.</returns>
        private IpcResponseDto? Send(IpcRequestDto request, CancellationToken ct)
        {
            // Every await below runs with ConfigureAwait(false) (ConfigureAwait.Fody), so blocking here cannot deadlock
            // on a captured synchronization context.
            return ExecuteIpcRequestAsync(request, ct).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Connects to the host's named pipe, verifies that the Servy host owns it, transmits an IPC request, and
        /// returns the server's response.
        /// </summary>
        /// <param name="request">The IPC request DTO payload to send.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="IpcResponseDto"/> returned by the host service, or <see langword="null"/> if no response is received.</returns>
        /// <exception cref="TimeoutException">Thrown when the connection attempt times out or the host does not answer within the request timeout.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the pipe is not served by the expected process.</exception>
        private async Task<IpcResponseDto?> ExecuteIpcRequestAsync(IpcRequestDto request, CancellationToken ct)
        {
            // Identification lets the host read who is calling without being able to act as the caller.
            using (var clientStream = new NamedPipeClientStream(".", _namedPipesName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
            {
                try
                {
                    await clientStream.ConnectAsync(_connectTimeoutMs, ct);
                }
                catch (TimeoutException ex)
                {
                    throw new TimeoutException($"Connection to primary Servy host named pipe '{_namedPipesName}' timed out after {_connectTimeoutMs} ms.", ex);
                }

                _serverVerifier.Verify(clientStream);

                using (var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                // A pending pipe read does not observe its token on every runtime (.NET Framework starts the overlapped
                // read and never cancels it); closing the pipe aborts it everywhere.
                using (requestCts.Token.Register(() => { try { clientStream.Dispose(); } catch (Exception) { /* already closed */ } }))
                {
                    requestCts.CancelAfter(_requestTimeoutMs);
                    try
                    {
                        await WriteAsync(clientStream, request, requestCts.Token);
                        var response = await ReadAsync<IpcResponseDto>(clientStream, requestCts.Token);

                        // A pipe closed by the timeout can end the read as an empty answer rather than an exception
                        requestCts.Token.ThrowIfCancellationRequested();
                        return response;
                    }
                    catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
                    {
                        throw new TimeoutException($"The Servy host did not answer the '{request.Action}' request within {_requestTimeoutMs} ms.", ex);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException) && requestCts.IsCancellationRequested)
                    {
                        // The pipe was closed under the pending read or write by the timeout or the caller
                        if (ct.IsCancellationRequested)
                            throw new OperationCanceledException("The Servy host request was cancelled.", ex, ct);

                        throw new TimeoutException($"The Servy host did not answer the '{request.Action}' request within {_requestTimeoutMs} ms.", ex);
                    }
                }
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
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
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
