using System;
using System.ComponentModel;
using Servy.Core.Enums;

namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a log entry displayed in the Logs tab of Servy Manager.
    /// </summary>
    public class LogEntryModel : INotifyPropertyChanged
    {
        // Centralized prefix for UI resource icons to ensure DRY compliance
        private const string IconBase = "pack://application:,,,/Servy.Manager;component/Resources/Icons/";

        private DateTime _time;
        private EventLogLevel _level;
        private int _eventId;
        private string _message;

        /// <summary>
        /// Gets or sets the timestamp of the log entry.
        /// </summary>
        public DateTime Time
        {
            get => _time;
            set
            {
                if (_time != value)
                {
                    _time = value;
                    OnPropertyChanged(nameof(Time));
                }
            }
        }

        /// <summary>
        /// Gets or sets the severity level of the log entry using the strongly-typed EventLogLevel enumeration.[cite: 1]
        /// </summary>
        public EventLogLevel Level
        {
            get => _level;
            set
            {
                if (_level != value)
                {
                    _level = value;
                    OnPropertyChanged(nameof(Level));
                    // LOGIC: LevelIcon is a dependent property; notify the UI to refresh it when Level changes[cite: 1]
                    OnPropertyChanged(nameof(LevelIcon));
                }
            }
        }

        /// <summary>
        /// Gets or sets the Windows Event Log identifier for the entry.[cite: 1]
        /// </summary>
        public int EventId
        {
            get => _eventId;
            set
            {
                if (_eventId != value)
                {
                    _eventId = value;
                    OnPropertyChanged(nameof(EventId));
                }
            }
        }

        /// <summary>
        /// Gets or sets the message text of the log entry.[cite: 1]
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged(nameof(Message));
                }
            }
        }

        /// <summary>
        /// Gets the absolute pack URI for the icon resource that matches the current Level. 
        /// Defaults to the Information icon for unrecognized levels.[cite: 1]
        /// </summary>
        public string LevelIcon
        {
            get
            {
                // LOG: Resolving icon path for current log level using .NET 4.8 switch syntax
                switch (Level)
                {
                    case EventLogLevel.Warning:
                        return IconBase + "Warning.png";

                    case EventLogLevel.Error:
                        return IconBase + "Error.png";

                    case EventLogLevel.Information:
                    default:
                        // LOGIC: Fallback to Information icon for default cases[cite: 1]
                        return IconBase + "Info.png";
                }
            }
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the specified property name.[cite: 1]
        /// </summary>
        /// <param name="propertyName">The name of the property that has changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}