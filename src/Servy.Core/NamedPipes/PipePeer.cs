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
        /// The only <c>GetNamedPipeClientComputerName</c> failure that proves the client is on this computer.
        /// </summary>
        private const int ErrorPipeLocal = 229;

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
        /// <param name="computerName">The client's computer name when it was read; otherwise empty.</param>
        /// <returns>
        /// <see langword="false"/> only when the client is proven to be on this computer; <see langword="true"/> when it
        /// is remote and when its location cannot be determined.
        /// </returns>
        [ExcludeFromCodeCoverage]
        public static bool IsRemoteClient(PipeStream serverStream, out string computerName)
        {
            computerName = string.Empty;
            if (serverStream == null || !serverStream.IsConnected)
                return false;

            // The API takes the buffer size in BYTES, so a StringBuilder capacity of 256 characters is 512
            var buffer = new System.Text.StringBuilder(256);
            if (NativeMethods.GetNamedPipeClientComputerName(serverStream.SafePipeHandle, buffer, (uint)(buffer.Capacity * sizeof(char))))
            {
                computerName = buffer.ToString();
                return true;
            }

            // Only ERROR_PIPE_LOCAL proves the client is on this computer. Any other failure leaves the answer
            // unknown, and an unknown client must not be served as a local one.
            return System.Runtime.InteropServices.Marshal.GetLastWin32Error() != ErrorPipeLocal;
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
