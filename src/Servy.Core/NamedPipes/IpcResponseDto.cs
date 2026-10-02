using Servy.Core.DTOs;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Represents an IPC response message returned by the primary Servy Host service over a Named Pipe.
    /// </summary>
    public class IpcResponseDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether the IPC request completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets an optional error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the service configuration payload returned by the host service.
        /// </summary>
        public ServiceDto? Data { get; set; }

        /// <summary>
        /// Gets or sets the number of records updated by the host service, if applicable.
        /// </summary>
        public int? UpdateData { get; set; }
    }
}
