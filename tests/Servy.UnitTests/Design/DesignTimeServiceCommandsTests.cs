using Servy.Core.DTOs;
using Servy.Design;
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
            var dummyDto = new ServiceDto();
            var ct = CancellationToken.None;

            // Act & Assert - Boolean returning methods
            Assert.True(await commands.InstallServiceAsync(dummyDto, cancellationToken: ct));
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

        [Fact]
        public void OpenRuntimeVariablesLinkAsync_CompletesSynchronously()
        {
            // Arrange
            var commands = new DesignTimeServiceCommands();
            var ct = CancellationToken.None;

            // Act
            // Capture the task instead of awaiting it: awaiting an already-completed task asserts nothing,
            // while synchronous completion is the property the designer depends on.
            var task = commands.OpenRuntimeVariablesLinkAsync(cancellationToken: ct);

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        }
    }
}
