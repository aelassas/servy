using Servy.Core.DTOs;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Represents an IPC request message sent to the primary Servy Host service over a Named Pipe.
    /// </summary>
    public class IpcRequestDto
    {
        /// <summary>
        /// Gets or sets the requested action name, one of the <c>ServyHost*Action</c> constants of
        /// <see cref="Config.AppConfig"/>.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the target service name associated with the request. The host only serves a request for a
        /// service to the process that service runs in, or to an administrator.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the runtime state to persist, for <see cref="Config.AppConfig.ServyHostUpdateRuntimeStateAction"/>.
        /// </summary>
        public ServiceRuntimeStateDto RuntimeState { get; set; }

        /// <summary>
        /// Gets or sets the restart attempts counter to persist, for
        /// <see cref="Config.AppConfig.ServyHostUpdateRestartAttemptsAction"/>.
        /// </summary>
        public int? RestartAttempts { get; set; }
    }
}
