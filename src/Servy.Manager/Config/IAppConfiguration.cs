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
        int PerformanceRefreshIntervalMs { get; }

        /// <summary>
        /// Gets the maximum number of log lines to retain in the live console viewer buffer.
        /// </summary>
        int ConsoleMaxLines { get; }

        /// <summary>
        /// Gets the polling interval, in milliseconds, for fetching new standard output and standard error logs.
        /// </summary>
        int ConsoleRefreshIntervalMs { get; }

        /// <summary>
        /// Gets the polling interval, in milliseconds, for refreshing the service dependencies tree.
        /// </summary>
        int DependenciesRefreshIntervalMs { get; }

        /// <summary>
        /// Gets the file path to the published Servy Desktop application executable.
        /// </summary>
        string? DesktopAppPublishPath { get; }

        /// <summary>
        /// Gets a value indicating whether WPF software rendering is forced for the current session.
        /// </summary>
        bool ForceSoftwareRendering { get; }

        /// <summary>
        /// Gets the display language saved for this application, as a <c>--lang</c> value
        /// (<c>system</c>, <c>en</c>, <c>zh-CN</c>, <c>fr</c>, <c>de</c>, <c>es</c>, <c>ja</c>, <c>it</c>, <c>pt-PT</c> or <c>pt-BR</c>),
        /// or <see langword="null"/> when it is not known.
        /// </summary>
        /// <remarks>
        /// Passed to Servy.exe when this application launches it, so both applications show the same language
        /// even when their settings files are not in the same folder.
        /// </remarks>
        string? LanguageCode { get; }

        /// <summary>
        /// Gets the window period in days for the logs tab search, loaded from configuration.
        /// This determines the default date range applied when retrieving service log entries.
        /// </summary>
        int LogsWindowDays { get; }

        /// <summary>
        /// Gets the delay, in milliseconds, used to debounce search keystrokes before filtering.
        /// </summary>
        int SearchDebounceDelayMs { get; }

        /// <summary>
        /// Gets the maximum number of concurrent Service Control Manager (SCM) operations
        /// permitted during bulk lifecycle tasks (start, stop, or restart).
        /// </summary>
        /// <remarks>
        /// To prevent thread starvation and SCM contention, the actual degree of parallelism is
        /// throttled by a hardware-aware ceiling: <c>Math.Max(1, Math.Min(Environment.ProcessorCount * 2, MaxBulkOperationParallelism))</c>.
        /// </remarks>
        int MaxBulkOperationParallelism { get; }

        /// <summary>
        /// Gets the comma-separated list of column names that are hidden in the services table.
        /// </summary>
        string ServicesHiddenColumns { get; }

        /// <summary>
        /// Gets the comma-separated list of column names that are hidden in the logs table.
        /// </summary>
        string LogsHiddenColumns { get; }

        /// <summary>
        /// Gets a value indicating whether the display name column visibility migration has been executed.
        /// </summary>
        bool IsDisplayNameColumnMigrated { get; }

        /// <summary>
        /// Gets the persisted position, size and state of the main window, in the
        /// <c>Left,Top,Width,Height,State</c> form, or an empty string when the default placement applies.
        /// </summary>
        string MainWindowPlacement { get; }

        /// <summary>
        /// Gets the persisted column widths of the services table, as a comma-separated list of
        /// <c>Header=Width</c> entries, or an empty string when the default widths apply.
        /// </summary>
        string ServicesColumnWidths { get; }

        /// <summary>
        /// Gets the persisted column widths of the logs table, as a comma-separated list of
        /// <c>Header=Width</c> entries, or an empty string when the default widths apply.
        /// </summary>
        string LogsColumnWidths { get; }
    }
}
