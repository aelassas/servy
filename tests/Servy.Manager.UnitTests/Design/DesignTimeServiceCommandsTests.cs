using Servy.Manager.Design;
using Servy.Manager.Models;

namespace Servy.Manager.UnitTests.Design
{
    public class DesignTimeServiceCommandsTests
    {
        [Fact]
        public async Task DesignTimeServiceCommands_ReturnsDefaultsAndDoesNotThrow()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var commands = new DesignTimeServiceCommands();
            var testService = new Service();

            // Act & Assert - Data retrieval
            var searchResults = await commands.SearchServicesAsync("test", false, cancellationToken: ct);
            Assert.Empty(searchResults);

            // Act & Assert - Boolean command methods
            Assert.True(await commands.StartServiceAsync(testService, cancellationToken: ct));
            Assert.True(await commands.StopServiceAsync(testService, cancellationToken: ct));
            Assert.True(await commands.RestartServiceAsync(testService, cancellationToken: ct));
            Assert.True(await commands.InstallServiceAsync(testService, cancellationToken: ct));
            Assert.True(await commands.UninstallServiceAsync(testService, cancellationToken: ct));
            Assert.True(await commands.RemoveServiceAsync(testService, cancellationToken: ct));

            // Act & Assert - Task and Dispose methods
            var exception = await Record.ExceptionAsync(async () =>
            {
                await commands.ConfigureServiceAsync(testService, ct);
                await commands.ExportServiceToXmlAsync(testService, ct);
                await commands.ExportServiceToJsonAsync(testService, ct);
                await commands.ImportXmlConfigAsync(ct);
                await commands.ImportJsonConfigAsync(ct);
                await commands.CopyPidAsync(testService, ct);
                commands.Dispose();
            });

            Assert.Null(exception);
        }
    }
}
