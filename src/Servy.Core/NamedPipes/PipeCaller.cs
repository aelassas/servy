namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// What the Servy host knows about the process at the client end of a connection.
    /// </summary>
    public sealed class PipeCaller
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PipeCaller"/> class.
        /// </summary>
        /// <param name="processId">The client's process identifier; 0 when it could not be read.</param>
        /// <param name="isAdministrator">Whether the client runs as Local System or as an elevated administrator.</param>
        public PipeCaller(int processId, bool isAdministrator)
        {
            ProcessId = processId;
            IsAdministrator = isAdministrator;
        }

        /// <summary>
        /// Gets the client's process identifier; 0 when it could not be read.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets a value indicating whether the client runs as Local System or as an elevated administrator.
        /// </summary>
        public bool IsAdministrator { get; }
    }
}
