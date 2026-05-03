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

        /// <summary>
        /// Gets the window period in days for the logs tab search, loaded from configuration. 
        /// This determines the default date range applied when retrieving service log entries.
        /// </summary>
        int LogsWindowDays { get; }

        /// <summary>
        /// Delay in milliseconds to debounce search keystrokes before filtering in console tab.
        /// </summary>
        int SearchDebounceDelayMs { get; }

        /// <summary>
        /// Maximum number of concurrent Service Control Manager (SCM) operations 
        /// permitted during bulk lifecycle tasks (start, stop, or restart).
        /// </summary>
        /// <remarks>
        /// To prevent thread starvation and SCM contention, the actual degree of parallelism is 
        /// throttled by a hardware-aware ceiling: <c>Math.Min(Environment.ProcessorCount * 2, MaxBulkOperationParallelism)</c>.
        /// </remarks>
        int MaxBulkOperationParallelism { get; }
    }
}