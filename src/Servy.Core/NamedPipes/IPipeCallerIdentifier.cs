using System.IO.Pipes;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Identifies the process connected to the server end of a named pipe.
    /// </summary>
    public interface IPipeCallerIdentifier
    {
        /// <summary>
        /// Identifies the client of a connected server pipe.
        /// </summary>
        /// <param name="connectedServer">The connected server end of the pipe.</param>
        /// <returns>The client's process identifier and whether it is an administrator; never <see langword="null"/>.</returns>
        PipeCaller Identify(NamedPipeServerStream connectedServer);
    }
}
