using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.CLI.Validators;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Services;

namespace Servy.CLI.UnitTests.Commands
{
    public class InstallServiceCommandTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly Mock<IServiceInstallValidator> _mockValidator;
        private readonly InstallServiceCommand _command;

        public InstallServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _mockValidator = new Mock<IServiceInstallValidator>();
            _command = new InstallServiceCommand(_mockServiceManager.Object, _mockValidator.Object);
        }

        [Fact]
        public async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            // Updated to use the new options object signature
            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            Directory.CreateDirectory(Path.GetDirectoryName(wrapperExePath)!);
            File.WriteAllText(wrapperExePath, "dummy content");

            try
            {
                // Act
                var result = await _command.Execute(options);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(string.Format(Strings.Msg_InstallSuccess, options.ServiceName), result.Message);
            }
            finally
            {
                // Clean up the dummy file
                if (File.Exists(wrapperExePath)) File.Delete(wrapperExePath);
            }
        }

        [Fact]
        public async Task Execute_ValidationFails_ReturnsFailure()
        {
            // Arrange
            var options = new Options.InstallServiceOptions();
            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Fail("Validation error."));

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Validation error.", result.Message);
        }

        [Fact]
        public async Task Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            var options = new Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            // Updated to use the new options object signature
            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Failed to install service."));

            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            Directory.CreateDirectory(Path.GetDirectoryName(wrapperExePath)!);
            File.WriteAllText(wrapperExePath, "dummy content");

            try
            {
                // Act
                var result = await _command.Execute(options);

                // Assert
                Assert.False(result.Success);
                Assert.Equal("Failed to install service.", result.Message);
            }
            finally
            {
                if (File.Exists(wrapperExePath)) File.Delete(wrapperExePath);
            }
        }

        [Fact]
        public async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            // Updated to use the new options object signature
            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Throws<UnauthorizedAccessException>();

            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            Directory.CreateDirectory(Path.GetDirectoryName(wrapperExePath)!);
            File.WriteAllText(wrapperExePath, "dummy content");

            try
            {
                // Act
                var result = await _command.Execute(options);

                // Assert
                Assert.False(result.Success);
                Assert.Contains("Access Denied", result.Message);
            }
            finally
            {
                if (File.Exists(wrapperExePath)) File.Delete(wrapperExePath);
            }
        }

        [Fact]
        public async Task Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            var options = new Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            // Updated to use the new options object signature
            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Throws<Exception>();

            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            Directory.CreateDirectory(Path.GetDirectoryName(wrapperExePath)!);
            File.WriteAllText(wrapperExePath, "dummy content");

            try
            {
                // Act
                var result = await _command.Execute(options);

                // Assert
                Assert.False(result.Success);
                Assert.Contains($"Failed to install service '{options.ServiceName}'", result.Message);
            }
            finally
            {
                if (File.Exists(wrapperExePath)) File.Delete(wrapperExePath);
            }
        }

    }
}
