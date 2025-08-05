using Servy.Core;

namespace Servy.ViewModels
{
    /// <summary>
    /// Represents a recovery action with its associated display name for UI.
    /// </summary>
    public class RecoveryActionItem
    {
        /// <summary>
        /// Gets or sets the recovery action enum value.
        /// </summary>
        public RecoveryAction RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets the localized display name of the recovery action.
        /// </summary>
        public string DisplayName { get; set; }
    }
}
