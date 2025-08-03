using Servy.Core;

namespace Servy.ViewModels
{
    /// <summary>
    /// Represents a process priority with its associated display name for UI.
    /// </summary>
    public class ProcessPriorityItem
    {
        /// <summary>
        /// Gets or sets the process priority enum value.
        /// </summary>
        public ProcessPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the localized display name of the process priority.
        /// </summary>
        public string DisplayName { get; set; }
    }
}
