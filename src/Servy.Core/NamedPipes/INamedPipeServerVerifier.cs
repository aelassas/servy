using System.IO.Pipes;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Verifies, once a client is connected, that the server end of the pipe belongs to the expected process.
    /// </summary>
    /// <remarks>
    /// Without this check any process that creates the pipe name first (pipe squatting) would receive the service's
    /// requests and could answer them with a configuration of its choosing, which the wrapper would then run under the
    /// service account.
    /// </remarks>
    public interface INamedPipeServerVerifier
    {
        /// <summary>
        /// Verifies the server end of a connected pipe.
        /// </summary>
        /// <param name="connectedClient">The connected client end of the pipe.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the server is not the expected process.</exception>
        void Verify(PipeStream connectedClient);
    }
}
