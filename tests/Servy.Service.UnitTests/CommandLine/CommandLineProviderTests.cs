using Servy.Service.CommandLine;
using System;
using Xunit;

namespace Servy.Service.UnitTests.CommandLine
{
    public class CommandLineProviderTests
    {
        [Fact]
        public void GetArgs_ReturnsCurrentEnvironmentCommandLineArguments()
        {
            // Arrange
            var provider = new CommandLineProvider();
            string[] expectedArgs = Environment.GetCommandLineArgs();

            // Act
            string[] actualArgs = provider.GetArgs();

            // Assert
            // The provider is a pure delegation to Environment.GetCommandLineArgs(); compare contents.
            Assert.Equal(expectedArgs, actualArgs);
        }
    }
}
