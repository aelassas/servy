namespace Servy.Core.Services
{
    /// <summary>
    /// Represents a simplified, testable abstraction over a WMI management object
    /// that exposes service-related properties.
    /// </summary>
    public interface IManagementObject
    {
        /// <summary>
        /// Gets the startup mode of the service (e.g., "Auto", "Manual", "Disabled").
        /// </summary>
        string StartMode { get; }
    }
}
