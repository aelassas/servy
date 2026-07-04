using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.CLI.Validation;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class InstallServiceCommandTests : IDisposable
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly Mock<IServiceInstallValidator> _mockValidator;
        private readonly InstallServiceCommand _command;
        private readonly string _wrapperExePath;

        public InstallServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _mockValidator = new Mock<IServiceInstallValidator>();
            _command = new InstallServiceCommand(_mockServiceManager.Object, _mockValidator.Object);

            // Create a dummy Servy.Service.CLI.Net48.exe for the tests
            _wrapperExePath = AppConfig.GetServyCLIServicePath();
            Directory.CreateDirectory(Path.GetDirectoryName(_wrapperExePath));
            File.WriteAllText(_wrapperExePath, "dummy content");
        }

        [Fact]
        public async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.ExecuteAsync(options, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_InstallSuccess, options.ServiceName), result.Message);
        }

        [Fact]
        public async Task Execute_ValidationFails_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions();
            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Fail("Validation error."));

            // Act
            var result = await _command.ExecuteAsync(options, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Validation error.", result.Message);
        }

        [Fact]
        public async Task Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Failed to install service."));

            // Act
            var result = await _command.ExecuteAsync(options, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to install service.", result.Message);
        }

        [Fact]
        public async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Throws<UnauthorizedAccessException>();

            // Act
            var result = await _command.ExecuteAsync(options, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Access Denied", result.Message);
        }

        [Fact]
        public async Task Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Throws<Exception>();

            // Act
            var result = await _command.ExecuteAsync(options, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(string.Format(Strings.Msg_InstallServiceAction, options.ServiceName), result.Message);
        }

        public void Dispose()
        {
            // Clean up the dummy file
            if (File.Exists(_wrapperExePath))
            {
                File.Delete(_wrapperExePath);
            }
        }
    }
}