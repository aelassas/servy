using Servy.Core.Enums;

namespace Servy.ViewModels.Items
{
    /// <summary>
    /// Represents an item used to display and bind a service startup type in the UI.
    /// </summary>
    public class StartupTypeItem
    {
        /// <summary>
        /// Gets or sets the startup type of the service (e.g., Automatic, Manual, Disabled).
        /// </summary>
        public ServiceStartType StartupType { get; set; }

        /// <summary>
        /// Gets or sets the human-readable label for the startup type.
        /// </summary>
        public string DisplayName { get; set; }
    }
}
