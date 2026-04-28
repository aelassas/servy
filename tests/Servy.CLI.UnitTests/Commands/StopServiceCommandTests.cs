using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Common;
using Servy.Core.Services;

namespace Servy.CLI.UnitTests.Commands
{
    public class StopServiceCommandTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly StopServiceCommand _command;

        public StopServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _command = new StopServiceCommand(_mockServiceManager.Object);
        }

        [Fact]
        public async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new StopServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service 'TestService' stopped successfully.", result.Message);
        }

        [Fact]
        public async Task Execute_EmptyServiceName_ReturnsFailure()
        {
            // Arrange
            var options = new StopServiceOptions { ServiceName = "" };

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
            var options = new StopServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to stop service."));

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to stop service.", result.Message);
        }

        [Fact]
        public async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new StopServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).Throws<UnauthorizedAccessException>();

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Access Denied", result.Message);
        }

        [Fact]
        public async Task Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            var options = new StopServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).Throws<Exception>();

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Failed to stop service 'TestService'", result.Message);
        }
    }
}
