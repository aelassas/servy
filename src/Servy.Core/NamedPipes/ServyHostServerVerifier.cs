using Servy.Core.Config;
using Servy.Core.Services;
using System;
using System.IO.Pipes;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Accepts the server end of a pipe only when it is owned by the process the Servy host service runs in.
    /// </summary>
    public class ServyHostServerVerifier : INamedPipeServerVerifier
    {
        private readonly IWindowsServiceApi _windowsServiceApi;
        private readonly string _hostServiceName;
        private readonly Func<PipeStream, int> _serverProcessIdReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyHostServerVerifier"/> class for
        /// <see cref="AppConfig.ServyHostServiceName"/>.
        /// </summary>
        /// <param name="windowsServiceApi">The Service Control Manager API used to read the host service's process identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="windowsServiceApi"/> is <see langword="null"/>.</exception>
        public ServyHostServerVerifier(IWindowsServiceApi windowsServiceApi)
            : this(windowsServiceApi, AppConfig.ServyHostServiceName, ReadServerProcessId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyHostServerVerifier"/> class.
        /// </summary>
        /// <param name="windowsServiceApi">The Service Control Manager API used to read the host service's process identifier.</param>
        /// <param name="hostServiceName">The name of the service that must own the server end of the pipe.</param>
        /// <param name="serverProcessIdReader">Reads the server's process identifier from a connected client pipe; 0 when it cannot be read.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        internal ServyHostServerVerifier(IWindowsServiceApi windowsServiceApi, string hostServiceName, Func<PipeStream, int> serverProcessIdReader)
        {
            _windowsServiceApi = windowsServiceApi ?? throw new ArgumentNullException(nameof(windowsServiceApi));
            _hostServiceName = hostServiceName ?? throw new ArgumentNullException(nameof(hostServiceName));
            _serverProcessIdReader = serverProcessIdReader ?? throw new ArgumentNullException(nameof(serverProcessIdReader));
        }

        /// <inheritdoc />
        public void Verify(PipeStream connectedClient)
        {
            if (connectedClient == null) throw new ArgumentNullException(nameof(connectedClient));

            var serverProcessId = _serverProcessIdReader(connectedClient);
            if (serverProcessId <= 0)
                throw new UnauthorizedAccessException("The process that owns the Servy host named pipe could not be identified.");

            var hostProcessId = _windowsServiceApi.GetServiceProcessId(_hostServiceName);
            if (hostProcessId <= 0)
                throw new UnauthorizedAccessException($"The '{_hostServiceName}' service is not running, so the named pipe server cannot be trusted.");

            if (hostProcessId != serverProcessId)
                throw new UnauthorizedAccessException(
                    $"The named pipe is served by process {serverProcessId}, not by the '{_hostServiceName}' service (process {hostProcessId}). The connection was refused.");
        }

        /// <summary>
        /// Reads the server's process identifier from a connected client pipe.
        /// </summary>
        /// <param name="pipe">The connected client end of the pipe.</param>
        /// <returns>The server's process identifier, or 0 when it cannot be read.</returns>
        private static int ReadServerProcessId(PipeStream pipe)
            => PipePeer.TryGetServerProcessId(pipe, out var pid) ? pid : 0;
    }
}
