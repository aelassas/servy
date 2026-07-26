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
            var dummyConfig = new ServiceConfiguration();
            var ct = TestContext.Current.CancellationToken;

            // Act & Assert - Boolean returning methods
            Assert.True(await commands.InstallService(dummyConfig, ct));
            Assert.True(await commands.UninstallService("testService", ct));
            Assert.True(await commands.StartService("testService", ct));
            Assert.True(await commands.StopService("testService", ct));
            Assert.True(await commands.RestartService("testService", ct));

            // Act & Assert - Task.CompletedTask returning methods
            var exception = await Record.ExceptionAsync(async () =>
            {
                await commands.ExportXmlConfig("password", cancellationToken: ct);
                await commands.ExportJsonConfig("password", cancellationToken: ct);
                await commands.ImportXmlConfig(cancellationToken: ct);
                await commands.ImportJsonConfig(cancellationToken: ct);
                await commands.OpenManager(cancellationToken: ct);
            });

            Assert.Null(exception);
        }
    }
}