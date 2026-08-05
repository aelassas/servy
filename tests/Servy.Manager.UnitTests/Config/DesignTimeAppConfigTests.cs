using Servy.Manager.Config;
using System.ComponentModel;
using AppConfig = Servy.Core.Config.AppConfig;

namespace Servy.Manager.UnitTests.Config
{
    public class DesignTimeAppConfigTests
    {
        [Fact]
        public void DesignTimeAppConfig_Properties_ReturnExpectedValues()
        {
            // Arrange
            IAppConfiguration config = new DesignTimeAppConfig();

            // Assert - UI visibility and state
            Assert.True(config.IsDesktopAppAvailable);
            Assert.False(config.ForceSoftwareRendering);

            // Assert - Refresh Intervals
            Assert.Equal(AppConfig.DefaultRefreshIntervalInSeconds, config.RefreshIntervalInSeconds);
            Assert.Equal(AppConfig.DefaultPerformanceRefreshIntervalInMs, config.PerformanceRefreshIntervalInMs);
            Assert.Equal(AppConfig.DefaultConsoleRefreshIntervalInMs, config.ConsoleRefreshIntervalInMs);
            Assert.Equal(AppConfig.DefaultDependenciesRefreshIntervalInMs, config.DependenciesRefreshIntervalInMs);

            // Assert - Limits and Thresholds
            Assert.Equal(AppConfig.DefaultConsoleMaxLines, config.ConsoleMaxLines);
            Assert.Equal(AppConfig.DefaultLogsWindowDays, config.LogsWindowDays);
            Assert.Equal(AppConfig.DefaultSearchDebounceDelayMs, config.SearchDebounceDelayMs);
            Assert.Equal(AppConfig.DefaultMaxBulkOperationParallelism, config.MaxBulkOperationParallelism);

            // Assert - Paths
            Assert.Equal(AppConfig.DefaultDesktopAppPublishPath, config.DesktopAppPublishPath);
        }

        [Fact]
        public void DesignTimeAppConfig_PropertyChanged_DiscardsSubscribersAndNeverRaises()
        {
            // Arrange
            IAppConfiguration config = new DesignTimeAppConfig();
            var raisedCount = 0;
            PropertyChangedEventHandler handler = (s, e) => raisedCount++;

            // Act: Subscribe, read multiple configuration properties, and unsubscribe
            config.PropertyChanged += handler;
            _ = config.RefreshIntervalInSeconds;
            _ = config.ConsoleMaxLines;
            _ = config.DesktopAppPublishPath;
            config.PropertyChanged -= handler;

            // Assert: Verify that adding/removing the event handler is a complete no-op and never notifies subscribers
            Assert.Equal(0, raisedCount);
        }
    }
}
