using System.IO.Pipes;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Identifies the process connected to the server end of a named pipe.
    /// </summary>
    public interface IPipeCallerIdentifier
    {
        /// <summary>
        /// Pins the client of a server pipe right after the connection completed, before anything is read from it: reads
        /// the client's process identifier and opens a handle to that process, which keeps the identifier from being given
        /// to another process while the pin is held.
        /// </summary>
        /// <param name="connectedServer">The connected server end of the pipe.</param>
        /// <returns>
        /// The pinned client, whose process identifier is 0 when it could not be read or the process could not be opened;
        /// never <see langword="null"/>. The caller disposes it once the request is answered.
        /// </returns>
        PipeClientPin Pin(NamedPipeServerStream connectedServer);

        /// <summary>
        /// Identifies the client of a connected server pipe.
        /// </summary>
        /// <param name="connectedServer">The connected server end of the pipe.</param>
        /// <param name="pin">The client as pinned by <see cref="Pin"/> at connect time; its process identifier is the one identified.</param>
        /// <returns>The client's pinned process identifier and whether it is an administrator; never <see langword="null"/>.</returns>
        PipeCaller Identify(NamedPipeServerStream connectedServer, PipeClientPin pin);
    }
}
