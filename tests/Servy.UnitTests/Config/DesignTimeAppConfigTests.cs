using Servy.Config;
using System.ComponentModel;
using Xunit;
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
        public void DesignTimeAppConfig_PropertyChanged_NoOp()
        {
            // Arrange
            var config = new DesignTimeAppConfig();
            PropertyChangedEventHandler handler = (s, e) => { };

            // Act & Assert
            // Verifies the no-op implementation handles add/remove without exceptions
            var exception = Record.Exception(() =>
            {
                config.PropertyChanged += handler;
                config.PropertyChanged -= handler;
            });

            Assert.Null(exception);
        }
    }
}