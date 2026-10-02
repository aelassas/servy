using Servy.Core.DTOs;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Represents an IPC request message sent to the primary Servy Host service over a Named Pipe.
    /// </summary>
    public class IpcRequestDto
    {
        /// <summary>
        /// Gets or sets the requested action name (e.g., "GetByName" or "Update").
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the target service name associated with the request.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets optional payload data for update or persistence operations.
        /// </summary>
        public ServiceDto Data { get; set; }
    }
}
