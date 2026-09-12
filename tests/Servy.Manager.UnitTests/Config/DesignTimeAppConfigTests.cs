using Servy.Manager.Config;
using System.ComponentModel;
using System.Reflection;
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
            Assert.Equal(AppConfig.DefaultMonitoringRefreshIntervalInMs, config.PerformanceRefreshIntervalInMs);
            Assert.Equal(AppConfig.DefaultMonitoringRefreshIntervalInMs, config.ConsoleRefreshIntervalInMs);
            Assert.Equal(AppConfig.DefaultMonitoringRefreshIntervalInMs, config.DependenciesRefreshIntervalInMs);

            // Assert - Limits and Thresholds
            Assert.Equal(AppConfig.DefaultConsoleMaxLines, config.ConsoleMaxLines);
            Assert.Equal(AppConfig.DefaultLogsWindowDays, config.LogsWindowDays);
            Assert.Equal(AppConfig.DefaultSearchDebounceDelayMs, config.SearchDebounceDelayMs);
            Assert.Equal(AppConfig.DefaultMaxBulkOperationParallelism, config.MaxBulkOperationParallelism);

            // Assert - Paths
            Assert.Equal(AppConfig.DefaultDesktopAppPublishPath, config.DesktopAppPublishPath);
        }

        [Fact]
        public void DesignTimeAppConfig_PropertyChanged_SubscribeAndUnsubscribeRaiseNothing()
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

        [Fact]
        public void DesignTimeAppConfig_PropertyChanged_DiscardsSubscribers()
        {
            // Arrange
            // An auto-implemented event compiles to a private backing field and would retain every
            // designer subscriber; the empty add/remove accessors compile to no field at all, which
            // is the whole reason this stub declares them explicitly.

            // Act
            var backingField = typeof(DesignTimeAppConfig)
                .GetField(nameof(IAppConfiguration.PropertyChanged), BindingFlags.Instance | BindingFlags.NonPublic);

            // Assert
            Assert.Null(backingField);
        }
    }
}
