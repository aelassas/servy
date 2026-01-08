using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class InstallServiceCommandTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly Mock<IServiceInstallValidator> _mockValidator;
        private readonly Mock<IServiceRepository> _mockRepository;
        private readonly InstallServiceCommand _command;

        public InstallServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _mockValidator = new Mock<IServiceInstallValidator>();
            _mockRepository = new Mock<IServiceRepository>();
            _command = new InstallServiceCommand(_mockServiceManager.Object, _mockValidator.Object);
        }

        [Fact]
        public async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));
            _mockServiceManager.Setup(sm => sm.InstallService(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Core.Enums.ServiceStartType>(),
                It.IsAny<Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),

                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<Core.Enums.DateRotationType>(),
                It.IsAny<int>(),
                It.IsAny<int>()
            )).Returns(Task.FromResult(true));

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service installed successfully.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }

        [Fact]
        public async Task Execute_ValidationFails_ReturnsFailure()
        {
            // Arrange
            var options = new InstallServiceOptions();
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
            var options = new InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));
            _mockServiceManager.Setup(sm => sm.InstallService(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Core.Enums.ServiceStartType>(),
                It.IsAny<Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),

                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<Core.Enums.DateRotationType>(),
                It.IsAny<int>(),
                It.IsAny<int>()
            )).Returns(Task.FromResult(false));

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to install service.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }

        [Fact]
        public async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));
            _mockServiceManager.Setup(sm => sm.InstallService(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Core.Enums.ServiceStartType>(),
                It.IsAny<Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),

                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<Core.Enums.DateRotationType>(),
                It.IsAny<int>(),
                It.IsAny<int>()
            )).Throws<UnauthorizedAccessException>();

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Administrator privileges are required.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }

        [Fact]
        public async Task Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));
            _mockServiceManager.Setup(sm => sm.InstallService(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Core.Enums.ServiceStartType>(),
                It.IsAny<Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),

                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<Core.Enums.DateRotationType>(),
                It.IsAny<int>(),
                It.IsAny<int>()
            )).Throws<Exception>();

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = AppConfig.GetServyCLIServicePath();
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = await _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("An unexpected error occurred.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }
    }
}
