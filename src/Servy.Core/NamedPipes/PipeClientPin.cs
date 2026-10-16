using System;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// The client of a server pipe, pinned at connect time: its process identifier, read right after the connection
    /// completed and before anything was read from the pipe, and an open handle to that process.
    /// </summary>
    /// <remarks>
    /// Windows does not give a process identifier to a new process while a handle to the old process is open, so for as
    /// long as the pin is held, <see cref="ProcessId"/> is the process that connected and cannot have passed to another
    /// process, such as a service started after the client exited. The Servy host takes the pin before it reads the
    /// request and disposes it once the request is answered.
    /// </remarks>
    public sealed class PipeClientPin : IDisposable
    {
        private IDisposable _processHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeClientPin"/> class.
        /// </summary>
        /// <param name="processId">The client's process identifier; 0 when it could not be read or the process could not be opened.</param>
        /// <param name="processHandle">
        /// An open handle to the client process, which keeps <paramref name="processId"/> from being reused; disposed with
        /// the pin. <see langword="null"/> when nothing is held.
        /// </param>
        public PipeClientPin(int processId, IDisposable processHandle)
        {
            ProcessId = processId;
            _processHandle = processHandle;
        }

        /// <summary>
        /// Gets the client's process identifier as read at connect time; 0 when it could not be read or the process could
        /// not be opened.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Releases the handle to the client process. Calling it again does nothing.
        /// </summary>
        public void Dispose()
        {
            var processHandle = _processHandle;
            _processHandle = null;
            processHandle?.Dispose();
        }
    }
}
