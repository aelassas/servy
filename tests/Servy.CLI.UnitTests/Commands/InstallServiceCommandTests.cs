using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.CLI.Validation;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Services;
using Servy.Testing;
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
        private readonly string _backupPath;

        public InstallServiceCommandTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _mockValidator = new Mock<IServiceInstallValidator>();
            _command = new InstallServiceCommand(_mockServiceManager.Object, _mockValidator.Object);

            // Create a dummy Servy.Service.CLI.exe for the tests
            _wrapperExePath = AppConfig.GetServyCLIServicePath();
            Directory.CreateDirectory(Path.GetDirectoryName(_wrapperExePath));

            // RELEASE BUILD SAFETY GUARD: If a live installation binary already occupies the target directory,
            // squirrel it away safely inside a temporary backup file profile to prevent clobbering.
            if (File.Exists(_wrapperExePath))
            {
                _backupPath = Path.Combine(Path.GetTempPath(), $"Servy_Backup_CLI_{Guid.NewGuid():N}.bak");
                File.Copy(_wrapperExePath, _backupPath, overwrite: true);
            }

            File.WriteAllText(_wrapperExePath, "dummy content");
        }

        [Fact]
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceManager nullManager = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>("serviceManager", () => new InstallServiceCommand(nullManager, _mockValidator.Object));
            Assert.Equal("serviceManager", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullValidator_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceInstallValidator nullValidator = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>("validator", () => new InstallServiceCommand(_mockServiceManager.Object, nullValidator));
            Assert.Equal("validator", ex.ParamName);
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

            TestReflection.SetFieldStatic(typeof(InstallServiceCommand), "_bypassElevationCheck", true);

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

            TestReflection.SetFieldStatic(typeof(InstallServiceCommand), "_bypassElevationCheck", true);

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

            TestReflection.SetFieldStatic(typeof(InstallServiceCommand), "_bypassElevationCheck", true);

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

            TestReflection.SetFieldStatic(typeof(InstallServiceCommand), "_bypassElevationCheck", true);

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

            TestReflection.SetFieldStatic(typeof(InstallServiceCommand), "_bypassElevationCheck", true);

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
                try
                {
                    File.Delete(_wrapperExePath);
                }
                catch
                {
                    // Prevent disposal failures from masking core runtime assertions
                }
            }

            // RELEASE BUILD SAFETY GUARD: Restore original, verified production binary 
            // if a backup transaction was initialized during the arrangement phase.
            if (!string.IsNullOrEmpty(_backupPath) && File.Exists(_backupPath))
            {
                try
                {
                    File.Copy(_backupPath, _wrapperExePath, overwrite: true);
                    File.Delete(_backupPath);
                }
                catch
                {
                    // Best-effort recovery catch
                }
            }
        }
    }
}