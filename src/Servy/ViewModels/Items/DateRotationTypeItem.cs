using Servy.Core.Enums;

namespace Servy.ViewModels.Items
{
    /// <summary>
    /// Represents an item used to display and bind a service startup type in the UI.
    /// </summary>
    public class DateRotationTypeItem
    {
        /// <summary>
        /// Gets or sets the date rotation type of the service (e.g., Daily, Weekly, Monthly).
        /// </summary>
        public DateRotationType DateRotationType { get; set; }

        /// <summary>
        /// Gets or sets the human-readable label for the date rotation type.
        /// </summary>
        public string DisplayName { get; set; }
    }
}
