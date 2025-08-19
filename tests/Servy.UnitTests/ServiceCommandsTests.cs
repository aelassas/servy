using Moq;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using Servy.Helpers;
using Servy.Services;
using Servy.UI.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UnitTests
{
    public class ServiceCommandsTests
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceCommands> _mockServiceCommands;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidator;

        public ServiceCommandsTests()
        {
            _dialogServiceMock = new Mock<IFileDialogService>();
            _mockServiceCommands = new Mock<IServiceCommands>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _serviceConfigurationValidator = new Mock<IServiceConfigurationValidator>();

        }

        [Fact]
        public void InstallService_CalledWithCorrectParameters()
        {
            // Arrange
            var serviceName = "TestService";
            var serviceDescription = "Test Service Description";
            var processPath = @"C:\path\to\exe.exe";
            var startupDirectory = @"C:\path\to";
            var processParameters = "-arg1 -arg2";
            var startupType = ServiceStartType.Automatic;
            var processPriority = ProcessPriority.Normal;
            var stdoutPath = @"C:\logs\stdout.log";
            var stderrPath = @"C:\logs\stderr.log";
            var enableRotation = true;
            var rotationSize = "10485760"; // 10 MB
            var enableHealthMonitoring = true;
            var heartbeatInterval = "30";
            var maxFailedChecks = "3";
            var recoveryAction = RecoveryAction.RestartService;
            var maxRestartAttempts = "5";
            var envVars = "var1=val1;var2=val2";
            var dependencies = "DEP1; DEP2";
            var runAsLocalSystem = false;
            var username = @".\username";
            var password = "password";
            var confirmPassword = "password";

            var preLaunchExe = @"C:\pre-launch.exe";
            var preLaunchDir = @"C:\preLaunchDir";
            var preLaunchArgs = "preLaunchArgs";
            var preLaunchVars = "var1=val1;var2=val2;";
            var preLaunchStdoutPath = @"C:\pre-launch-stdout.log";
            var preLaunchStderrPath = @"C:\pre-launch-stderr.log";
            var preLaunchTimeout = "30";
            var preLaunchRetryAttempts = "0";
            var preLaunchIgnoreError = true;


            // Act
            _mockServiceCommands.Object.InstallService(
                serviceName,
                serviceDescription,
                processPath,
                startupDirectory,
                processParameters,
                startupType,
                processPriority,
                stdoutPath,
                stderrPath,
                enableRotation,
                rotationSize,
                enableHealthMonitoring,
                heartbeatInterval,
                maxFailedChecks,
                recoveryAction,
                maxRestartAttempts,
                envVars,
                dependencies,
                runAsLocalSystem,
                username,
                password,
                confirmPassword,

                preLaunchExe,
                preLaunchDir,
                preLaunchArgs,
                preLaunchVars,
                preLaunchStdoutPath,
                preLaunchStderrPath,
                preLaunchTimeout,
                preLaunchRetryAttempts,
                preLaunchIgnoreError
                );

            // Assert
            _mockServiceCommands.Verify(m => m.InstallService(
                serviceName,
                serviceDescription,
                processPath,
                startupDirectory,
                processParameters,
                startupType,
                processPriority,
                stdoutPath,
                stderrPath,
                enableRotation,
                rotationSize,
                enableHealthMonitoring,
                heartbeatInterval,
                maxFailedChecks,
                recoveryAction,
                maxRestartAttempts,
                envVars,
                dependencies,
                runAsLocalSystem,
                username,
                password,
                confirmPassword,

                preLaunchExe,
                preLaunchDir,
                preLaunchArgs,
                preLaunchVars,
                preLaunchStdoutPath,
                preLaunchStderrPath,
                preLaunchTimeout,
                preLaunchRetryAttempts,
                preLaunchIgnoreError
                ), Times.Once);
        }

        [Fact]
        public void UninstallService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.UninstallService(serviceName);

            _mockServiceCommands.Verify(m => m.UninstallService(serviceName), Times.Once);
        }

        [Fact]
        public void StartService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.StartService(serviceName);

            _mockServiceCommands.Verify(m => m.StartService(serviceName), Times.Once);
        }

        [Fact]
        public void StopService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.StopService(serviceName);

            _mockServiceCommands.Verify(m => m.StopService(serviceName), Times.Once);
        }

        [Fact]
        public void RestartService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.RestartService(serviceName);

            _mockServiceCommands.Verify(m => m.RestartService(serviceName), Times.Once);
        }

        [Fact]
        public async Task ExportXmlCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var path = "export.xml";
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);


            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            var serviceCommands = new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: (dto) => { },
                serviceManager: Mock.Of<IServiceManager>(),
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object
            );

            // Act
            await serviceCommands.ExportXmlConfig();

            // Assert
            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public async Task ExportJsonCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var path = "export.json";
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            var serviceCommands = new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: (dto) => { },
                serviceManager: Mock.Of<IServiceManager>(),
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object
            );

            // Act
            await serviceCommands.ExportJsonConfig();

            // Assert
            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public async Task ImportXmlCommand_ValidFile_UpdatesModel()
        {
            // Arrange
            var xmlContent = @"<ServiceDto><Name>TestService</Name><ExecutablePath>C:\MyApp.exe</ExecutablePath></ServiceDto>";
            var path = "test.xml";

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);

            var bindCalled = false;
            ServiceDto capturedDto = null;
            Action<ServiceDto> bindSpy = dto =>
            {
                bindCalled = true;
                capturedDto = dto;
            };

            var serviceCommands = new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: bindSpy,
                serviceManager: Mock.Of<IServiceManager>(),
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object
            );

            File.WriteAllText(path, xmlContent); // alternatively, mock File.ReadAllText

            

            // Act
            await serviceCommands.ImportXmlConfig();

            // Assert
            Assert.True(bindCalled);
            Assert.NotNull(capturedDto);
            Assert.Equal("TestService", capturedDto.Name);
            File.Delete(path);
        }

        [Fact]
        public async Task ImportJsonCommand_ValidFile_UpdatesModel()
        {
            // Arrange
            var jsonContent = "{\"Name\":\"TestService\", \"ExecutablePath\":\"C:\\\\MyApp.exe\"}";
            var path = "test.json";

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);

            var bindCalled = false;
            ServiceDto capturedDto = null;
            Action<ServiceDto> bindSpy = dto =>
            {
                bindCalled = true;
                capturedDto = dto;
            };

            var serviceCommands = new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: bindSpy,
                serviceManager: Mock.Of<IServiceManager>(),
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object
            );

            File.WriteAllText(path, jsonContent);

            // Act
            await serviceCommands.ImportJsonConfig();

            // Assert
            Assert.True(bindCalled);
            Assert.NotNull(capturedDto);
            Assert.Equal("TestService", capturedDto.Name);
            File.Delete(path);
        }


        [Fact]
        public void ImportXmlCommand_UserCancels_ShowsNothing()
        {
            // Arrange
            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(string.Empty);

            // Act
            _mockServiceCommands.Object.ImportXmlConfig();

            // Assert
            _messageBoxService.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ExportJsonCommand_UserCancels_ShowsNothing()
        {
            // Arrange
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(string.Empty);

            // Act
            _mockServiceCommands.Object.ExportJsonConfig();

            // Assert
            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
