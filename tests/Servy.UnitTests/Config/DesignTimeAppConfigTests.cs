using Servy.Config;
using System.ComponentModel;
using System.Reflection;
using AppConfig = Servy.Core.Config.AppConfig;

namespace Servy.UnitTests.Config
{
    public class DesignTimeAppConfigTests
    {
        [Fact]
        public void DesignTimeAppConfig_Properties_ReturnExpectedValues()
        {
            // Arrange
            var config = new DesignTimeAppConfig();

            // Assert
            Assert.True(config.IsManagerAppAvailable);
            Assert.Equal(AppConfig.DefaultManagerAppPublishPath, config.ManagerAppPublishPath);
            Assert.False(config.ForceSoftwareRendering);
        }

        [Fact]
        public void DesignTimeAppConfig_PropertyChanged_SubscribeAndUnsubscribeRaiseNothing()
        {
            // Arrange
            IAppConfiguration config = new DesignTimeAppConfig();
            var raisedCount = 0;
            PropertyChangedEventHandler handler = (s, e) => raisedCount++;

            // Act
            config.PropertyChanged += handler;
            _ = config.IsManagerAppAvailable;
            _ = config.ManagerAppPublishPath;
            _ = config.ForceSoftwareRendering;
            config.PropertyChanged -= handler;

            // Assert
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
