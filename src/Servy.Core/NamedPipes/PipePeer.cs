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
        /// <summary>
        /// Determines whether the client of a connected server end runs on another computer, i.e. reached the pipe over
        /// the network (SMB) rather than locally.
        /// </summary>
        /// <param name="serverStream">The connected server end of the pipe.</param>
        /// <param name="computerName">The client's computer name when it is remote; otherwise empty.</param>
        /// <returns><see langword="true"/> when the client is remote.</returns>
        [ExcludeFromCodeCoverage]
        public static bool IsRemoteClient(PipeStream serverStream, out string computerName)
        {
            computerName = string.Empty;
            if (serverStream == null || !serverStream.IsConnected)
                return false;

            var buffer = new System.Text.StringBuilder(256);
            if (!NativeMethods.GetNamedPipeClientComputerName(serverStream.SafePipeHandle, buffer, (uint)buffer.Capacity))
                return false; // ERROR_PIPE_LOCAL: the client is on this computer

            computerName = buffer.ToString();
            return true;
        }

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
