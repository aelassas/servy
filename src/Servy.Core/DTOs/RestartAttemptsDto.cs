namespace Servy.Core.DTOs
{
    /// <summary>
    /// A service's persisted restart attempts counter, as the Servy host returns it over the named pipe.
    /// </summary>
    public class RestartAttemptsDto
    {
        /// <summary>
        /// Gets or sets the number of restart attempts recorded for the service.
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Gets or sets the UTC time the counter was last written, or <see langword="null"/> when it never was.
        /// </summary>
        /// <remarks>
        /// The wrapper compares it with the system boot time to tell a restart within the same OS session from the
        /// first start after a reboot, as it compared the last write time of the counter file before.
        /// </remarks>
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
