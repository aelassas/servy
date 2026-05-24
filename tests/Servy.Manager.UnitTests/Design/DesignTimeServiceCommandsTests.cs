using Servy.Manager.Design;
using Servy.Manager.Models;

namespace Servy.Manager.UnitTests.Design
{
    public class DesignTimeServiceCommandsTests
    {
        [Fact]
        public async Task DesignTimeServiceCommands_ReturnExpectedValues()
        {
            // Arrange
            var commands = new DesignTimeServiceCommands();
            var testService = new Service();

            // Act & Assert - Data retrieval
            var searchResults = await commands.SearchServicesAsync("test", false, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Empty(searchResults);

            // Act & Assert - Boolean command methods
            Assert.True(await commands.StartServiceAsync(testService, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await commands.StopServiceAsync(testService, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await commands.RestartServiceAsync(testService, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await commands.InstallServiceAsync(testService, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await commands.UninstallServiceAsync(testService, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await commands.RemoveServiceAsync(testService, cancellationToken: TestContext.Current.CancellationToken));

            // Act & Assert - Task and Dispose methods
            var exception = await Record.ExceptionAsync(async () =>
            {
                await commands.ConfigureServiceAsync(testService);
                await commands.ExportServiceToXmlAsync(testService);
                await commands.ExportServiceToJsonAsync(testService);
                await commands.ImportXmlConfigAsync(TestContext.Current.CancellationToken);
                await commands.ImportJsonConfigAsync(TestContext.Current.CancellationToken);
                await commands.CopyPid(testService);
                commands.Dispose();
            });

            Assert.Null(exception);
        }
    }
}