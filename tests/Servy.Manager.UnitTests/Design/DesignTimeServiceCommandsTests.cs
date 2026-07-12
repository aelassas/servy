using Servy.Manager.Design;
using Servy.Manager.Models;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.Design
{
    public class DesignTimeServiceCommandsTests
    {
        [Fact]
        public async Task DesignTimeServiceCommands_ReturnsDefaultsAndDoesNotThrow()
        {
            // Arrange
            var commands = new DesignTimeServiceCommands();
            var testService = new Service();

            // Act & Assert - Data retrieval
            var searchResults = await commands.SearchServicesAsync("test", false);
            Assert.Empty(searchResults);

            // Act & Assert - Boolean command methods
            Assert.True(await commands.StartServiceAsync(testService));
            Assert.True(await commands.StopServiceAsync(testService));
            Assert.True(await commands.RestartServiceAsync(testService));
            Assert.True(await commands.InstallServiceAsync(testService));
            Assert.True(await commands.UninstallServiceAsync(testService));
            Assert.True(await commands.RemoveServiceAsync(testService));

            // Act & Assert - Task and Dispose methods
            var exception = await Record.ExceptionAsync(async () =>
            {
                await commands.ConfigureServiceAsync(testService);
                await commands.ExportServiceToXmlAsync(testService);
                await commands.ExportServiceToJsonAsync(testService);
                await commands.ImportXmlConfigAsync();
                await commands.ImportJsonConfigAsync();
                await commands.CopyPidAsync(testService);
                commands.Dispose();
            });

            Assert.Null(exception);
        }
    }
}