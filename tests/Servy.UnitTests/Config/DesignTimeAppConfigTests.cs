using Servy.Config;
using System.ComponentModel;
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
        public void DesignTimeAppConfig_PropertyChanged_DiscardsSubscribersAndNeverRaises()
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
    }
}
