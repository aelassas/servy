using System.ComponentModel;

namespace Servy.Config
{
    /// <summary>
    /// Provides configuration settings and dynamic state properties for the Servy application.
    /// </summary>
    public interface IAppConfiguration : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets a value indicating whether the companion Servy Manager application is available on the system.
        /// </summary>
        bool IsManagerAppAvailable { get; }

        /// <summary>
        /// Gets the file path to the published Servy Manager application executable.
        /// </summary>
        string ManagerAppPublishPath { get; }

        /// <summary>
        /// Gets a value indicating whether WPF software rendering is forced for the current session.
        /// </summary>
        bool ForceSoftwareRendering { get; }
    }
}