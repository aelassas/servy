using System.ComponentModel;
using Servy.Core.Config;

namespace Servy.Manager.Config
{
    /// <summary>
    /// A no-op implementation of <see cref="IAppConfiguration"/> designed for the XAML designer.
    /// </summary>
    /// <remarks>
    /// This prevents ArgumentNullExceptions in the MainViewModel constructor during design-time
    /// and ensures the UI remains responsive in Visual Studio or Blend.
    /// </remarks>
    public class DesignTimeAppConfig : IAppConfiguration
    {
        // UI visibility and state
        bool IAppConfiguration.IsDesktopAppAvailable => true;
        bool IAppConfiguration.ForceSoftwareRendering => false;

        // Refresh Intervals (aligned with AppConfig defaults)
        int IAppConfiguration.RefreshIntervalInSeconds => Core.Config.AppConfig.DefaultRefreshIntervalInSeconds;
        int IAppConfiguration.PerformanceRefreshIntervalInMs => Core.Config.AppConfig.DefaultPerformanceRefreshIntervalInMs;
        int IAppConfiguration.ConsoleRefreshIntervalInMs => Core.Config.AppConfig.DefaultConsoleRefreshIntervalInMs;
        int IAppConfiguration.DependenciesRefreshIntervalInMs => Core.Config.AppConfig.DefaultDependenciesRefreshIntervalInMs;

        // Limits and Thresholds
        int IAppConfiguration.ConsoleMaxLines => Core.Config.AppConfig.DefaultConsoleMaxLines;
        int IAppConfiguration.LogsWindowDays => Core.Config.AppConfig.DefaultLogsWindowDays;
        int IAppConfiguration.SearchDebounceDelayMs => 300; // Standard UI responsiveness delay
        public int MaxBulkOperationParallelism => Core.Config.AppConfig.DefaultMaxBulkOperationParallelism;

        // Paths
        string? IAppConfiguration.DesktopAppPublishPath => Core.Config.AppConfig.DefaultDesktopAppPublishPath;

        /// <summary>
        /// No-op implementation of PropertyChanged for design-time.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }
}