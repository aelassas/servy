using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class UninstallServiceCommandTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly Mock<IServiceRepository> _mockRepository;
        private readonly UninstallServiceCommand _command;

        public UninstallServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _mockRepository = new Mock<IServiceRepository>();
            _command = new UninstallServiceCommand(_mockServiceManager.Object, _mockRepository.Object);
        }

        [Fact]
        public async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Returns(Task.FromResult(true));

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service uninstalled successfully.", result.Message);
        }

        [Fact]
        public async Task Execute_EmptyServiceName_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "" };

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Service name is required.", result.Message);
        }

        [Fact]
        public async Task Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Returns(Task.FromResult(false));

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to uninstall service.", result.Message);
        }

        [Fact]
        public async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Throws<UnauthorizedAccessException>();

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Administrator privileges are required.", result.Message);
        }

        [Fact]
        public async Task Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            var options = new UninstallServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.UninstallService("TestService")).Throws<Exception>();

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("An unexpected error occurred.", result.Message);
        }
    }
}


