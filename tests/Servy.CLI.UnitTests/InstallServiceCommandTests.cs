using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.Interfaces;

namespace Servy.CLI.UnitTests
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
        public void Execute_ValidOptions_ReturnsSuccess()
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
                It.IsAny<Servy.Core.Enums.ServiceStartType>(),
                It.IsAny<Servy.Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Servy.Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(true);

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Program.ServyServiceExeFileName}.exe");
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Service installed successfully.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }

        [Fact]
        public void Execute_ValidationFails_ReturnsFailure()
        {
            // Arrange
            var options = new InstallServiceOptions();
            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Fail("Validation error."));

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Validation error.", result.Message);
        }

        [Fact]
        public void Execute_ServiceManagerFails_ReturnsFailure()
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
                It.IsAny<Servy.Core.Enums.ServiceStartType>(),
                It.IsAny<Servy.Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Servy.Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(false);

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Program.ServyServiceExeFileName}.exe");
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to install service.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }

        [Fact]
        public void Execute_UnauthorizedAccessException_ReturnsFailure()
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
                It.IsAny<Servy.Core.Enums.ServiceStartType>(),
                It.IsAny<Servy.Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Servy.Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Throws<UnauthorizedAccessException>();

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Program.ServyServiceExeFileName}.exe");
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Administrator privileges are required.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }

        [Fact]
        public void Execute_GenericException_ReturnsFailure()
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
                It.IsAny<Servy.Core.Enums.ServiceStartType>(),
                It.IsAny<Servy.Core.Enums.ProcessPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Servy.Core.Enums.RecoveryAction>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Throws<Exception>();

            // Create a dummy Servy.Service.exe for the test
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Program.ServyServiceExeFileName}.exe");
            File.WriteAllText(wrapperExePath, "dummy content");

            // Act
            var result = _command.Execute(options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("An unexpected error occurred.", result.Message);

            // Clean up the dummy file
            File.Delete(wrapperExePath);
        }
    }
}


