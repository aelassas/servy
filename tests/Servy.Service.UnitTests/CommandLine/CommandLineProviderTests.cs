using Xunit;
using Servy.Service.CommandLine;

namespace Servy.Service.UnitTests.CommandLine
{
    public class CommandLineProviderTests
    {
        [Fact]
        public void GetArgs_ReturnsNonNullArray()
        {
            // Arrange
            var provider = new CommandLineProvider();

            // Act
            var args = provider.GetArgs();

            // Assert
            Assert.NotNull(args);
            Assert.IsType<string[]>(args);
        }
    }
}