using Servy.Core.Native;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Identifies the process at the other end of a connected named pipe.
    /// </summary>
    public static class PipePeer
    {
        /// <summary>
        /// Gets the process identifier of the client connected to a server pipe.
        /// </summary>
        /// <param name="serverStream">The connected server end of the pipe.</param>
        /// <param name="processId">The client's process identifier, or 0 when it cannot be read.</param>
        /// <returns><see langword="true"/> when the identifier was read.</returns>
        [ExcludeFromCodeCoverage]
        public static bool TryGetClientProcessId(PipeStream serverStream, out int processId)
        {
            processId = 0;
            if (serverStream == null || !serverStream.IsConnected)
                return false;

            if (!NativeMethods.GetNamedPipeClientProcessId(serverStream.SafePipeHandle, out var pid) || pid == 0)
                return false;

            processId = (int)pid;
            return true;
        }

        /// <summary>
        /// Gets the process identifier of the server that owns the server end of a pipe a client is connected to.
        /// </summary>
        /// <param name="clientStream">The connected client end of the pipe.</param>
        /// <param name="processId">The server's process identifier, or 0 when it cannot be read.</param>
        /// <returns><see langword="true"/> when the identifier was read.</returns>
        [ExcludeFromCodeCoverage]
        public static bool TryGetServerProcessId(PipeStream clientStream, out int processId)
        {
            processId = 0;
            if (clientStream == null || !clientStream.IsConnected)
                return false;

            if (!NativeMethods.GetNamedPipeServerProcessId(clientStream.SafePipeHandle, out var pid) || pid == 0)
                return false;

            processId = (int)pid;
            return true;
        }
    }
}
