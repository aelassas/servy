using Servy.Design;
using Servy.Models;
using System.Threading;
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
            var ct = CancellationToken.None;

            // Act & Assert - Boolean returning methods
            Assert.True(await commands.InstallServiceAsync(dummyConfig, ct));
            Assert.True(await commands.UninstallServiceAsync("testService", ct));
            Assert.True(await commands.StartServiceAsync("testService", ct));
            Assert.True(await commands.StopServiceAsync("testService", ct));
            Assert.True(await commands.RestartServiceAsync("testService", ct));

            // Act & Assert - Task.CompletedTask returning methods
            var exception = await Record.ExceptionAsync(async () =>
            {
                await commands.ExportXmlConfigAsync("password", cancellationToken: ct);
                await commands.ExportJsonConfigAsync("password", cancellationToken: ct);
                await commands.ImportXmlConfigAsync(cancellationToken: ct);
                await commands.ImportJsonConfigAsync(cancellationToken: ct);
                await commands.OpenManagerAsync(cancellationToken: ct);
            });

            Assert.Null(exception);
        }
    }
}