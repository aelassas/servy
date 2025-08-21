using System;
using System.ComponentModel;

namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a log entry displayed in the Logs tab of Servy Manager.
    /// </summary>
    public class LogEntryModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets or sets the timestamp of the log entry.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the log entry 
        /// (e.g. Information, Warning, Error).
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// Gets or sets the Windows Event Log identifier for the entry.
        /// </summary>
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the message text of the log entry.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the icon resource path that corresponds to the <see cref="Level"/>.
        /// Defaults to the information icon if the level is unrecognized.
        /// </summary>
        public string LevelIcon
        {
            get
            {
                if (Level == "Information")
                    return "pack://application:,,,/Servy.Manager;component/Resources/Icons/Info.png";
                if (Level == "Warning")
                    return "pack://application:,,,/Servy.Manager;component/Resources/Icons/Warning.png";
                if (Level == "Error")
                    return "pack://application:,,,/Servy.Manager;component/Resources/Icons/Error.png";
                return "pack://application:,,,/Servy.Manager;component/Resources/Icons/Info.png";
            }
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
