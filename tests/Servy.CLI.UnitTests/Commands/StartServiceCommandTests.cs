using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Common;
using Servy.Core.Services;

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
        public async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service 'TestService' started successfully.", result.Message);
        }

        [Fact]
        public async Task Execute_EmptyServiceName_ReturnsFailure()
        {
            // Arrange
            var options = new StartServiceOptions { ServiceName = "" };

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
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to start service."));

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to start service.", result.Message);
        }

        [Fact]
        public async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).Throws<UnauthorizedAccessException>();

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
            var options = new StartServiceOptions { ServiceName = "TestService" };
            _mockServiceManager.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _mockServiceManager.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(Core.Enums.ServiceStartType.Automatic);
            _mockServiceManager.Setup(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).Throws<Exception>();

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Failed to start service 'TestService'", result.Message);
        }
    }
}
