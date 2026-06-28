using Servy.Manager.Config;
using System.ComponentModel;
using Xunit;
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
            Assert.Equal(300, config.SearchDebounceDelayMs);
            Assert.Equal(AppConfig.DefaultMaxBulkOperationParallelism, config.MaxBulkOperationParallelism);

            // Assert - Paths
            Assert.Equal(AppConfig.DefaultDesktopAppPublishPath, config.DesktopAppPublishPath);
        }

        [Fact]
        public void DesignTimeAppConfig_PropertyChanged_NoOp()
        {
            // Arrange
            var config = new DesignTimeAppConfig();
            PropertyChangedEventHandler handler = (s, e) => { };

            // Act & Assert
            // Verifies that adding/removing the event handler does not throw,
            // even though the implementation is a no-op.
            var exception = Record.Exception(() =>
            {
                config.PropertyChanged += handler;
                config.PropertyChanged -= handler;
            });

            Assert.Null(exception);
        }
    }
}