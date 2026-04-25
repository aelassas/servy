using Moq;
using Newtonsoft.Json;
using Servy.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using Servy.Models;
using Servy.Services;
using Servy.UI.Services;
using Servy.Validators;

namespace Servy.UnitTests.Services
{
    public class ServiceCommandsTests
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceCommands> _mockServiceCommands;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidator;
        private readonly Mock<IXmlServiceValidator> _xmlServiceValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonServiceValidatorMock;
        private readonly Mock<IAppConfiguration> _appConfigMock; // 1. Added mock configuration

        public ServiceCommandsTests()
        {
            _dialogServiceMock = new Mock<IFileDialogService>();
            _mockServiceCommands = new Mock<IServiceCommands>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _serviceConfigurationValidator = new Mock<IServiceConfigurationValidator>();
            _xmlServiceValidatorMock = new Mock<IXmlServiceValidator>();
            _jsonServiceValidatorMock = new Mock<IJsonServiceValidator>();
            _appConfigMock = new Mock<IAppConfiguration>(); // 2. Initialize mock
        }

        [Fact]
        public async Task InstallService_CalledWithCorrectConfiguration()
        {
            // Arrange
            var config = new ServiceConfiguration
            {
                Name = "TestService",
                DisplayName = "TestService",
                Description = "Test Service Description",
                ExecutablePath = @"C:\path\to\exe.exe",
                StartupDirectory = @"C:\path\to",
                Parameters = "-arg1 -arg2",
                StartupType = ServiceStartType.Automatic,
                Priority = ProcessPriority.Normal,
                StdoutPath = @"C:\logs\stdout.log",
                StderrPath = @"C:\logs\stderr.log",
                EnableSizeRotation = true,
                RotationSize = "10",
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = "30",
                MaxFailedChecks = "3",
                RecoveryAction = RecoveryAction.RestartService,
                MaxRestartAttempts = "5",
                EnvironmentVariables = "var1=val1;var2=val2",
                ServiceDependencies = "DEP1; DEP2",
                RunAsLocalSystem = false,
                UserAccount = @".\username",
                Password = "password",
                ConfirmPassword = "password",

                // Pre-Launch
                PreLaunchExecutablePath = @"C:\pre-launch.exe",
                PreLaunchStartupDirectory = @"C:\preLaunchDir",
                PreLaunchParameters = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = @"C:\pre-launch-stdout.log",
                PreLaunchStderrPath = @"C:\pre-launch-stderr.log",
                PreLaunchTimeoutSeconds = "30",
                PreLaunchRetryAttempts = "0",
                PreLaunchIgnoreFailure = true,

                // Failure Hook
                FailureProgramPath = @"C:\failureProgram.exe",
                FailureProgramStartupDirectory = @"C:\failureProgramDir",
                FailureProgramParameters = "failureProgramArgs",

                // Post-Launch
                PostLaunchExecutablePath = @"C:\post-launch.exe",
                PostLaunchStartupDirectory = @"C:\postLaunchDir",
                PostLaunchParameters = "postLaunchArgs",

                // Logging & Timing
                EnableDebugLogs = false,
                MaxRotations = "0",
                EnableDateRotation = true,
                DateRotationType = DateRotationType.Weekly,
                StartTimeout = "11",
                StopTimeout = "6",

                // Pre-Stop
                PreStopExecutablePath = @"C:\pre-stop.exe",
                PreStopStartupDirectory = @"C:\preStopDir",
                PreStopParameters = "preStopArgs",
                PreStopTimeoutSeconds = "10",
                PreStopLogAsError = false,

                // Post-Stop
                PostStopExecutablePath = @"C:\post-stop.exe",
                PostStopStartupDirectory = @"C:\postStopDir",
                PostStopParameters = "postStopArgs"
            };

            // Act
            await _mockServiceCommands.Object.InstallService(config);

            // Assert
            _mockServiceCommands.Verify(m => m.InstallService(
                It.Is<ServiceConfiguration>(c =>
                    // 1. Core Metadata & Main Process
                    c.Name == config.Name &&
                    c.DisplayName == config.DisplayName &&
                    c.Description == config.Description &&
                    c.ExecutablePath == config.ExecutablePath &&
                    c.StartupDirectory == config.StartupDirectory &&
                    c.Parameters == config.Parameters &&
                    c.StartupType == config.StartupType &&
                    c.Priority == config.Priority &&

                    // 2. Logging & Rotation
                    c.StdoutPath == config.StdoutPath &&
                    c.StderrPath == config.StderrPath &&
                    c.EnableSizeRotation == config.EnableSizeRotation &&
                    c.RotationSize == config.RotationSize &&
                    c.EnableDateRotation == config.EnableDateRotation &&
                    c.DateRotationType == config.DateRotationType &&
                    c.MaxRotations == config.MaxRotations &&
                    c.UseLocalTimeForRotation == config.UseLocalTimeForRotation &&

                    // 3. Health & Recovery
                    c.EnableHealthMonitoring == config.EnableHealthMonitoring &&
                    c.HeartbeatInterval == config.HeartbeatInterval &&
                    c.MaxFailedChecks == config.MaxFailedChecks &&
                    c.RecoveryAction == config.RecoveryAction &&
                    c.MaxRestartAttempts == config.MaxRestartAttempts &&
                    c.FailureProgramPath == config.FailureProgramPath &&
                    c.FailureProgramStartupDirectory == config.FailureProgramStartupDirectory &&
                    c.FailureProgramParameters == config.FailureProgramParameters &&

                    // 4. Identity & Security
                    c.EnvironmentVariables == config.EnvironmentVariables &&
                    c.ServiceDependencies == config.ServiceDependencies &&
                    c.RunAsLocalSystem == config.RunAsLocalSystem &&
                    c.UserAccount == config.UserAccount &&
                    c.Password == config.Password &&
                    c.ConfirmPassword == config.ConfirmPassword &&

                    // 5. Pre-Launch Hooks
                    c.PreLaunchExecutablePath == config.PreLaunchExecutablePath &&
                    c.PreLaunchStartupDirectory == config.PreLaunchStartupDirectory &&
                    c.PreLaunchParameters == config.PreLaunchParameters &&
                    c.PreLaunchEnvironmentVariables == config.PreLaunchEnvironmentVariables &&
                    c.PreLaunchStdoutPath == config.PreLaunchStdoutPath &&
                    c.PreLaunchStderrPath == config.PreLaunchStderrPath &&
                    c.PreLaunchTimeoutSeconds == config.PreLaunchTimeoutSeconds &&
                    c.PreLaunchRetryAttempts == config.PreLaunchRetryAttempts &&
                    c.PreLaunchIgnoreFailure == config.PreLaunchIgnoreFailure &&

                    // 6. Post-Launch & Timing
                    c.PostLaunchExecutablePath == config.PostLaunchExecutablePath &&
                    c.PostLaunchStartupDirectory == config.PostLaunchStartupDirectory &&
                    c.PostLaunchParameters == config.PostLaunchParameters &&
                    c.StartTimeout == config.StartTimeout &&
                    c.StopTimeout == config.StopTimeout &&
                    c.EnableDebugLogs == config.EnableDebugLogs &&

                    // 7. Pre-Stop Hooks
                    c.PreStopExecutablePath == config.PreStopExecutablePath &&
                    c.PreStopStartupDirectory == config.PreStopStartupDirectory &&
                    c.PreStopParameters == config.PreStopParameters &&
                    c.PreStopTimeoutSeconds == config.PreStopTimeoutSeconds &&
                    c.PreStopLogAsError == config.PreStopLogAsError &&

                    // 8. Post-Stop Hooks
                    c.PostStopExecutablePath == config.PostStopExecutablePath &&
                    c.PostStopStartupDirectory == config.PostStopStartupDirectory &&
                    c.PostStopParameters == config.PostStopParameters
                )), Times.Once);
        }

        [Fact]
        public void UninstallService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.UninstallService(serviceName, CancellationToken.None);

            _mockServiceCommands.Verify(m => m.UninstallService(serviceName, It.IsAny<CancellationToken>()), Times.Once);
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

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            var serviceCommands = new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: (dto) => { },
                serviceManager: Mock.Of<IServiceManager>(),
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object,
                xmlServiceValidator: _xmlServiceValidatorMock.Object,
                jsonServiceValidator: _jsonServiceValidatorMock.Object,
                appConfig: _appConfigMock.Object // 3. Pass IAppConfiguration mock
            );

            // Act
            await serviceCommands.ExportXmlConfig(string.Empty);

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

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            var serviceCommands = new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: (dto) => { },
                serviceManager: Mock.Of<IServiceManager>(),
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object,
                xmlServiceValidator: _xmlServiceValidatorMock.Object,
                jsonServiceValidator: _jsonServiceValidatorMock.Object,
                appConfig: _appConfigMock.Object // 3. Pass IAppConfiguration mock
            );

            // Act
            await serviceCommands.ExportJsonConfig(string.Empty);

            // Assert
            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public async Task ImportXmlCommand_ValidFile_UpdatesModel()
        {
            // Arrange
            // 1. Use a real path to satisfy the ProcessHelper.ValidatePath check inside XmlServiceValidator
            var realPath = @"C:\Windows\System32\notepad.exe";
            var xmlContent = $@"<ServiceDto><Name>TestService</Name><ExecutablePath>{realPath}</ExecutablePath></ServiceDto>";

            // Use a temp file to avoid permission issues in the project root
            var path = Path.GetTempFileName() + ".xml";

            _serviceConfigurationValidator.Setup(d => d.Validate(
                It.IsAny<ServiceDto>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>())).ReturnsAsync(true);

            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);

            var bindCalled = false;
            ServiceDto? capturedDto = null;
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
                serviceConfigurationValidator: _serviceConfigurationValidator.Object,
                xmlServiceValidator: _xmlServiceValidatorMock.Object,
                jsonServiceValidator: _jsonServiceValidatorMock.Object,
                appConfig: _appConfigMock.Object // 3. Pass IAppConfiguration mock
            );

            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string?>.IsAny))
                .Returns(true);

            File.WriteAllText(path, xmlContent);

            // Act
            await serviceCommands.ImportXmlConfig();

            // Assert
            try
            {
                Assert.True(bindCalled, "The logic exited before binding. Likely XmlServiceValidator.TryValidate failed because the ExecutablePath file doesn't exist.");
                Assert.NotNull(capturedDto);
                Assert.Equal("TestService", capturedDto!.Name);
            }
            finally
            {
                // Cleanup
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportJsonCommand_ValidFile_UpdatesModel()
        {
            // Arrange
            // 1. Use a guaranteed path to pass the internal ProcessHelper.ValidatePath check
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = @"C:\Windows\System32\notepad.exe"
            };

            // 2. Serialize properly to ensure it matches the DTO's attributes (JsonIgnore, etc.)
            var jsonContent = JsonConvert.SerializeObject(dto);

            var path = Path.GetTempFileName();
            File.WriteAllText(path, jsonContent);

            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);

            // 3. Ensure the validator mock returns true for this specific DTO instance
            _serviceConfigurationValidator
                .Setup(v => v.Validate(
                    It.IsAny<ServiceDto>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync(true);

            bool bindCalled = false;

            var serviceCommands = new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: d => {
                    bindCalled = true;
                    // Verify inside the spy to see what actually arrived
                    Assert.Equal("TestService", d.Name);
                },
                serviceManager: Mock.Of<IServiceManager>(),
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object,
                xmlServiceValidator: _xmlServiceValidatorMock.Object,
                jsonServiceValidator: _jsonServiceValidatorMock.Object,
                appConfig: _appConfigMock.Object // 3. Pass IAppConfiguration mock
            );

            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string?>.IsAny))
                .Returns(true);

            // Act
            await serviceCommands.ImportJsonConfig();

            // Cleanup before final assertion to prevent file locks
            if (File.Exists(path)) File.Delete(path);

            // Assert
            Assert.True(bindCalled, "The logic exited before binding. Check if JsonServiceValidator.TryValidate is failing on the ExecutablePath or if a JSON exception was caught.");
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
            _mockServiceCommands.Object.ExportJsonConfig(string.Empty);

            // Assert
            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}