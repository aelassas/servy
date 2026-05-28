using Servy.Design;
using Servy.Models;

namespace Servy.UnitTests.Design
{
    public class DesignTimeServiceCommandsTests
    {
        [Fact]
        public async Task DesignTimeServiceCommands_ReturnExpectedCompletedTasks()
        {
            // Arrange
            var commands = new DesignTimeServiceCommands();
            var dummyConfig = new ServiceConfiguration(); // Assuming ServiceConfiguration has a default constructor

            // Act & Assert - Boolean returning methods
            Assert.True(await commands.InstallService(dummyConfig, TestContext.Current.CancellationToken));
            Assert.True(await commands.UninstallService("testService", TestContext.Current.CancellationToken));
            Assert.True(await commands.StartService("testService", TestContext.Current.CancellationToken));
            Assert.True(await commands.StopService("testService", TestContext.Current.CancellationToken));
            Assert.True(await commands.RestartService("testService", TestContext.Current.CancellationToken));

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