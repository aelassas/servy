using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Services;
using System;
using System.Threading;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class StartServiceCommandTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly StartServiceCommand _command;

        public StartServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _command = new StartServiceCommand(_mockServiceManager.Object);
        }

        [Fact]
        public void Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartService("TestService")).Returns(true);

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service started successfully.", result.Message);
        }

        [Fact]
        public void Execute_EmptyServiceName_ReturnsFailure()
        {
            // Arrange
            var options = new StartServiceOptions { ServiceName = "" };

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
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartService("TestService")).Returns(false);

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to start service.", result.Message);
        }

        [Fact]
        public void Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartService("TestService")).Throws<UnauthorizedAccessException>();

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
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartService("TestService")).Throws<Exception>();

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("An unexpected error occurred.", result.Message);
        }
    }
}


