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
            // Verifies the structural content equality to confirm the provider correctly forwards the BCL framework arrays
            Assert.Equal(expectedArgs.Length, actualArgs.Length);
            Assert.Equal(expectedArgs, actualArgs);
        }
    }
}