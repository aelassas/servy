using System.ComponentModel;

namespace Servy.Manager.Config
{
    /// <summary>
    /// Provides configuration settings and dynamic state properties for the Servy Manager application.
    /// </summary>
    public interface IAppConfiguration : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets a value indicating whether the companion Servy Desktop configuration application is available on the system.
        /// </summary>
        bool IsDesktopAppAvailable { get; }

        /// <summary>
        /// Gets the polling interval, in seconds, for general service status updates.
        /// </summary>
        int RefreshIntervalInSeconds { get; }

        /// <summary>
        /// Gets the polling interval, in milliseconds, for retrieving process performance metrics such as CPU and RAM usage.
        /// </summary>
        int PerformanceRefreshIntervalInMs { get; }

        /// <summary>
        /// Gets the maximum number of log lines to retain in the live console viewer buffer.
        /// </summary>
        int ConsoleMaxLines { get; }

        /// <summary>
        /// Gets the polling interval, in milliseconds, for fetching new standard output and standard error logs.
        /// </summary>
        int ConsoleRefreshIntervalInMs { get; }

        /// <summary>
        /// Gets the polling interval, in milliseconds, for refreshing the service dependencies tree.
        /// </summary>
        int DependenciesRefreshIntervalInMs { get; }

        /// <summary>
        /// Gets the file path to the published Servy Desktop application executable.
        /// </summary>
        string? DesktopAppPublishPath { get; }

        /// <summary>
        /// Gets a value indicating whether WPF software rendering is forced for the current session.
        /// </summary>
        bool ForceSoftwareRendering { get; }
    }
}