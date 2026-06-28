using Servy.Design;
using Servy.Models;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UnitTests.Design
{
    public class DesignTimeServiceCommandsTests
    {
        [Fact]
        public async Task DesignTimeServiceCommands_ReturnExpectedCompletedTasks()
        {
            // Arrange
            var commands = new DesignTimeServiceCommands();
            var dummyConfig = new ServiceConfiguration();

            // Act & Assert - Boolean returning methods
            Assert.True(await commands.InstallService(dummyConfig));
            Assert.True(await commands.UninstallService("testService"));
            Assert.True(await commands.StartService("testService"));
            Assert.True(await commands.StopService("testService"));
            Assert.True(await commands.RestartService("testService"));

            // Act & Assert - Task.CompletedTask returning methods
            var exception = await Record.ExceptionAsync(async () =>
            {
                await commands.ExportXmlConfig("password");
                await commands.ExportJsonConfig("password");
                await commands.ImportXmlConfig();
                await commands.ImportJsonConfig();
                await commands.OpenManager();
            });

            Assert.Null(exception);
        }
    }
}