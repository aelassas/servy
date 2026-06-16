using Servy.Core.Enums;
using Servy.UI.ViewModels; // Inheriting from the centralized UI ViewModel base

namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a log entry displayed in the Logs tab of Servy Manager.
    /// Implements INotifyPropertyChanged via ViewModelBase to support UI bindings.
    /// </summary>
    public class LogEntryModel : ViewModelBase
    {
        // Centralized prefix for UI resource icons to ensure DRY compliance
        private const string IconBase = "pack://application:,,,/Servy.Manager;component/Resources/Icons/";

        private DateTime _time;
        private EventLogLevel _level;
        private int _eventId;
        private string? _message;

        /// <summary>
        /// Gets or sets the timestamp of the log entry.
        /// </summary>
        public DateTime Time
        {
            get => _time;
            set => Set(ref _time, value);
        }

        /// <summary>
        /// Gets or sets the severity level of the log entry using the strongly-typed EventLogLevel enumeration.
        /// </summary>
        public EventLogLevel Level
        {
            get => _level;
            set
            {
                // Use the return value of Set to trigger dependent property notifications only on change
                if (Set(ref _level, value))
                {
                    // LevelIcon is a dependent property; notify the UI to refresh it when Level changes
                    OnPropertyChanged(nameof(LevelIcon));
                }
            }
        }

        /// <summary>
        /// Gets or sets the Windows Event Log identifier for the entry.
        /// </summary>
        public int EventId
        {
            get => _eventId;
            set => Set(ref _eventId, value);
        }

        /// <summary>
        /// Gets or sets the message text of the log entry.
        /// </summary>
        public string? Message
        {
            get => _message;
            set => Set(ref _message, value);
        }

        /// <summary>
        /// Gets the absolute pack URI for the icon resource that matches the current Level. 
        /// Defaults to the Information icon for unrecognized levels.
        /// </summary>
        public string LevelIcon
        {
            get
            {
                switch (Level)
                {
                    case EventLogLevel.Critical:
                        return IconBase + "Error.png"; // or "Critical.png" if a dedicated asset is added

                    case EventLogLevel.Warning:
                        return IconBase + "Warning.png";

                    case EventLogLevel.Error:
                        return IconBase + "Error.png";

                    case EventLogLevel.Information:
                    case EventLogLevel.Verbose:
                    default:
                        // Fallback to Information icon for default cases
                        return IconBase + "Info.png";
                }
            }
        }
    }
}