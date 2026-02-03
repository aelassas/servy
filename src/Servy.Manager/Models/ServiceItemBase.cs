namespace Servy.Manager.Models
{
    /// <summary>
    /// Base type for all service models displayed in the UI.
    /// Provides common properties shared across different service representations,
    /// such as dependency views and performance monitoring views.
    /// </summary>
    public abstract class ServiceItemBase
    {
        /// <summary>
        /// Gets or sets the display name or system name of the Windows service.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Process Identifier (PID) of the service.
        /// Returns null when the service is not currently running or has no associated process.
        /// </summary>
        public int? Pid { get; set; }
    }
}
