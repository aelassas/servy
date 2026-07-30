using System.ComponentModel;

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
        public bool IsDesktopAppAvailable => true;
        public bool ForceSoftwareRendering => false;

        public int RefreshIntervalInSeconds => Core.Config.AppConfig.DefaultRefreshIntervalInSeconds;
        public int PerformanceRefreshIntervalInMs => Core.Config.AppConfig.DefaultPerformanceRefreshIntervalInMs;
        public int ConsoleRefreshIntervalInMs => Core.Config.AppConfig.DefaultConsoleRefreshIntervalInMs;
        public int DependenciesRefreshIntervalInMs => Core.Config.AppConfig.DefaultDependenciesRefreshIntervalInMs;

        public int ConsoleMaxLines => Core.Config.AppConfig.DefaultConsoleMaxLines;
        public int LogsWindowDays => Core.Config.AppConfig.DefaultLogsWindowDays;
        public int SearchDebounceDelayMs => Core.Config.AppConfig.DefaultSearchDebounceDelayMs; // Standard UI responsiveness delay
        public int MaxBulkOperationParallelism => Core.Config.AppConfig.DefaultMaxBulkOperationParallelism;

        string? IAppConfiguration.DesktopAppPublishPath => Core.Config.AppConfig.DefaultDesktopAppPublishPath;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }
}