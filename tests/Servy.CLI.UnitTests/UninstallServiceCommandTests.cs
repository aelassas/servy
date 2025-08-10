using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Interfaces;

namespace Servy.CLI.UnitTests
{
    public class UninstallServiceCommandTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly UninstallServiceCommand _command;

        public UninstallServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _command = new UninstallServiceCommand(_mockServiceManager.Object);
        }

        [Fact]
        public void Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Returns(true);

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service uninstalled successfully.", result.Message);
        }

        [Fact]
        public void Execute_EmptyServiceName_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "" };

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Service name is required.", result.Message);
        }

        [Fact]
        public void Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Returns(false);

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to uninstall service.", result.Message);
        }

        [Fact]
        public void Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Throws<UnauthorizedAccessException>();

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Administrator privileges are required.", result.Message);
        }

        [Fact]
        public void Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Throws<Exception>();

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("An unexpected error occurred.", result.Message);
        }
    }
}


