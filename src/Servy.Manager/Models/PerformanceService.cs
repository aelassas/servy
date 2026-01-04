namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a Windows service being tracked for performance metrics.
    /// </summary>
    public class PerformanceService
    {
        /// <summary>
        /// Gets or sets the display name or system name of the service.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Process Identifier (PID). 
        /// Returns null if the service is not currently running.
        /// </summary>
        public int? Pid { get; set; }
    }
}