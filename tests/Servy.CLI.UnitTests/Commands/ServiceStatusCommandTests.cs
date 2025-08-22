using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Services;
using System;
using System.ServiceProcess;
using System.Threading;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class ServiceStatusCommandTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly ServiceStatusCommand _command;

        public ServiceStatusCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _command = new ServiceStatusCommand(_mockServiceManager.Object);
        }

        [Fact]
        public void Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new ServiceStatusOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service status: Running", result.Message);
        }

        [Fact]
        public void Execute_EmptyServiceName_ReturnsFailure()
        {
            // Arrange
            var options = new ServiceStatusOptions { ServiceName = "" };

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
            var options = new ServiceStatusOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Throws<ArgumentException>();

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to get service status.", result.Message);
        }

    }
}


