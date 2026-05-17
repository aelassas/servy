using Moq;
using Newtonsoft.Json;
using Servy.Config;
using Servy.Core.Common;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Models;
using Servy.Services;
using Servy.UI.Services;
using Servy.Validators;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UnitTests.Services
{
    public class ServiceCommandsTests
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidator;
        private readonly Mock<IXmlServiceValidator> _xmlServiceValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonServiceValidatorMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<Func<ServiceDto>> _modelToServiceDtoMock;
        private readonly Mock<IXmlServiceSerializer> _xmlServiceSerializerMock;
        private readonly Mock<IJsonServiceSerializer> _jsonServiceSerializerMock;

        public ServiceCommandsTests()
        {
            _dialogServiceMock = new Mock<IFileDialogService>();
            _serviceManagerMock = new Mock<IServiceManager>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _serviceConfigurationValidator = new Mock<IServiceConfigurationValidator>();
            _xmlServiceValidatorMock = new Mock<IXmlServiceValidator>();
            _jsonServiceValidatorMock = new Mock<IJsonServiceValidator>();
            _appConfigMock = new Mock<IAppConfiguration>();
            _cursorServiceMock = new Mock<ICursorService>();
            _xmlServiceSerializerMock = new Mock<IXmlServiceSerializer>();
            _jsonServiceSerializerMock = new Mock<IJsonServiceSerializer>();

            _modelToServiceDtoMock = new Mock<Func<ServiceDto>>();

            // Setup safe defaults for ServiceManager to prevent NullReferenceExceptions internally
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            _serviceManagerMock.Setup(m => m.StartServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            _serviceManagerMock.Setup(m => m.StopServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            _serviceManagerMock.Setup(m => m.RestartServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());
        }

        private ServiceCommands CreateSut(Action<ServiceDto> bindSpy = null)
        {
            return new ServiceCommands(
                modelToServiceDto: _modelToServiceDtoMock.Object,
                bindServiceDtoToModel: bindSpy ?? (dto => { }),
                serviceManager: _serviceManagerMock.Object,
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object,
                xmlServiceValidator: _xmlServiceValidatorMock.Object,
                jsonServiceValidator: _jsonServiceValidatorMock.Object,
                appConfig: _appConfigMock.Object,
                cursorService: _cursorServiceMock.Object,
                xmlServiceSerializer: _xmlServiceSerializerMock.Object,
                jsonServiceSerializer: _jsonServiceSerializerMock.Object
            );
        }

        [Fact]
        public async Task InstallService_CalledWithCorrectConfiguration()
        {
            // Arrange
            var sut = CreateSut();

            // 1. The raw UI configuration passed into the method
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

                PreLaunchExecutablePath = @"C:\pre-launch.exe",
                PreLaunchStartupDirectory = @"C:\preLaunchDir",
                PreLaunchParameters = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = @"C:\pre-launch-stdout.log",
                PreLaunchStderrPath = @"C:\pre-launch-stderr.log",
                PreLaunchTimeoutSeconds = "30",
                PreLaunchRetryAttempts = "0",
                PreLaunchIgnoreFailure = true,

                FailureProgramPath = @"C:\failureProgram.exe",
                FailureProgramStartupDirectory = @"C:\failureProgramDir",
                FailureProgramParameters = "failureProgramArgs",

                PostLaunchExecutablePath = @"C:\post-launch.exe",
                PostLaunchStartupDirectory = @"C:\postLaunchDir",
                PostLaunchParameters = "postLaunchArgs",

                EnableDebugLogs = false,
                MaxRotations = "0",
                EnableDateRotation = true,
                DateRotationType = DateRotationType.Weekly,
                StartTimeout = "11",
                StopTimeout = "6",

                PreStopExecutablePath = @"C:\pre-stop.exe",
                PreStopStartupDirectory = @"C:\preStopDir",
                PreStopParameters = "preStopArgs",
                PreStopTimeoutSeconds = "10",
                PreStopLogAsError = false,

                PostStopExecutablePath = @"C:\post-stop.exe",
                PostStopStartupDirectory = @"C:\postStopDir",
                PostStopParameters = "postStopArgs",
            };

            // 2. The canonical DTO expected to be returned by the ViewModel mapper
            var expectedDto = new ServiceDto
            {
                Name = config.Name,
                DisplayName = config.DisplayName,
                Description = config.Description,
                ExecutablePath = config.ExecutablePath,
                StartupDirectory = config.StartupDirectory,
                Parameters = config.Parameters,
                StartupType = (int)config.StartupType,
                Priority = (int)config.Priority,
                StdoutPath = config.StdoutPath,
                StderrPath = config.StderrPath,
                EnableSizeRotation = config.EnableSizeRotation,
                RotationSize = int.Parse(config.RotationSize),
                UseLocalTimeForRotation = config.UseLocalTimeForRotation,
                EnableHealthMonitoring = config.EnableHealthMonitoring,
                HeartbeatInterval = int.Parse(config.HeartbeatInterval),
                MaxFailedChecks = int.Parse(config.MaxFailedChecks),
                RecoveryAction = (int)config.RecoveryAction,
                MaxRestartAttempts = int.Parse(config.MaxRestartAttempts),

                // Emulate the normalization done by the ViewModel mapping
                EnvironmentVariables = StringHelper.NormalizeString(config.EnvironmentVariables),
                ServiceDependencies = StringHelper.NormalizeString(config.ServiceDependencies),

                UserAccount = config.UserAccount,
                Password = config.Password,

                PreLaunchExecutablePath = config.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = config.PreLaunchStartupDirectory,
                PreLaunchParameters = config.PreLaunchParameters,
                PreLaunchEnvironmentVariables = StringHelper.NormalizeString(config.PreLaunchEnvironmentVariables),
                PreLaunchStdoutPath = config.PreLaunchStdoutPath,
                PreLaunchStderrPath = config.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = int.Parse(config.PreLaunchTimeoutSeconds),
                PreLaunchRetryAttempts = int.Parse(config.PreLaunchRetryAttempts),
                PreLaunchIgnoreFailure = config.PreLaunchIgnoreFailure,

                FailureProgramPath = config.FailureProgramPath,
                FailureProgramStartupDirectory = config.FailureProgramStartupDirectory,
                FailureProgramParameters = config.FailureProgramParameters,

                PostLaunchExecutablePath = config.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = config.PostLaunchStartupDirectory,
                PostLaunchParameters = config.PostLaunchParameters,

                MaxRotations = int.Parse(config.MaxRotations),
                EnableDateRotation = config.EnableDateRotation,
                DateRotationType = (int)config.DateRotationType,
                StartTimeout = int.Parse(config.StartTimeout),
                StopTimeout = int.Parse(config.StopTimeout),

                PreStopExecutablePath = config.PreStopExecutablePath,
                PreStopStartupDirectory = config.PreStopStartupDirectory,
                PreStopParameters = config.PreStopParameters,
                PreStopTimeoutSeconds = int.Parse(config.PreStopTimeoutSeconds),
                PreStopLogAsError = config.PreStopLogAsError,

                PostStopExecutablePath = config.PostStopExecutablePath,
                PostStopStartupDirectory = config.PostStopStartupDirectory,
                PostStopParameters = config.PostStopParameters
            };

            // CRITICAL FIX 1: Mock the delegate to return the DTO so InstallService doesn't fail early.
            // Replace `_modelToServiceDtoMock` with the actual name of the mock field.
            _modelToServiceDtoMock.Setup(m => m()).Returns(expectedDto);

            // CRITICAL FIX 2: Ensure the wrapper executable exists for the test to bypass the early exit check
            var wrapperPath = Core.Config.AppConfig.GetServyUIServicePath();
            try
            {
                var dir = Path.GetDirectoryName(wrapperPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(wrapperPath)) File.WriteAllText(wrapperPath, "dummy");
            }
            catch { /* Ignore creation errors if running in restricted environments */ }

            _serviceConfigurationValidator
                .Setup(v => v.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await sut.InstallService(config);

            // Assert
            Assert.True(result, "InstallService returned false. The validation or File.Exists check failed.");

            // CRITICAL FIX 3: Verify the options against the DTO, not the raw strings from the config
            _serviceManagerMock.Verify(m => m.InstallServiceAsync(
                It.Is<InstallServiceOptions>(c =>
                    c.ServiceName == expectedDto.Name &&
                    c.DisplayName == expectedDto.DisplayName &&
                    c.Description == expectedDto.Description &&
                    c.RealExePath == expectedDto.ExecutablePath &&
                    c.WorkingDirectory == expectedDto.StartupDirectory &&
                    c.RealArgs == expectedDto.Parameters &&
                    c.StartType == (ServiceStartType)expectedDto.StartupType &&
                    c.ProcessPriority == (ProcessPriority)expectedDto.Priority &&

                    c.StdoutPath == expectedDto.StdoutPath &&
                    c.StderrPath == expectedDto.StderrPath &&
                    c.EnableSizeRotation == expectedDto.EnableSizeRotation &&
                    c.RotationSizeInBytes == (expectedDto.RotationSize * 1024L * 1024L) &&
                    c.EnableDateRotation == expectedDto.EnableDateRotation &&
                    c.DateRotationType == (DateRotationType)expectedDto.DateRotationType &&
                    c.MaxRotations == expectedDto.MaxRotations &&
                    c.UseLocalTimeForRotation == expectedDto.UseLocalTimeForRotation &&

                    c.EnableHealthMonitoring == expectedDto.EnableHealthMonitoring &&
                    c.HeartbeatInterval == expectedDto.HeartbeatInterval &&
                    c.MaxFailedChecks == expectedDto.MaxFailedChecks &&
                    c.RecoveryAction == (RecoveryAction)expectedDto.RecoveryAction &&
                    c.MaxRestartAttempts == expectedDto.MaxRestartAttempts &&
                    c.FailureProgramPath == expectedDto.FailureProgramPath &&
                    c.FailureProgramWorkingDirectory == expectedDto.FailureProgramStartupDirectory &&
                    c.FailureProgramArgs == expectedDto.FailureProgramParameters &&

                    c.EnvironmentVariables == expectedDto.EnvironmentVariables &&
                    c.ServiceDependencies == expectedDto.ServiceDependencies &&
                    c.Username == expectedDto.UserAccount &&
                    c.Password == expectedDto.Password &&

                    c.PreLaunchExePath == expectedDto.PreLaunchExecutablePath &&
                    c.PreLaunchWorkingDirectory == expectedDto.PreLaunchStartupDirectory &&
                    c.PreLaunchArgs == expectedDto.PreLaunchParameters &&

                    c.PreLaunchEnvironmentVariables == expectedDto.PreLaunchEnvironmentVariables &&
                    c.PreLaunchStdoutPath == expectedDto.PreLaunchStdoutPath &&
                    c.PreLaunchStderrPath == expectedDto.PreLaunchStderrPath &&
                    c.PreLaunchTimeout == expectedDto.PreLaunchTimeoutSeconds &&
                    c.PreLaunchRetryAttempts == expectedDto.PreLaunchRetryAttempts &&
                    c.PreLaunchIgnoreFailure == expectedDto.PreLaunchIgnoreFailure &&

                    c.PostLaunchExePath == expectedDto.PostLaunchExecutablePath &&
                    c.PostLaunchWorkingDirectory == expectedDto.PostLaunchStartupDirectory &&
                    c.PostLaunchArgs == expectedDto.PostLaunchParameters &&
                    c.StartTimeout == expectedDto.StartTimeout &&
                    c.StopTimeout == expectedDto.StopTimeout &&

                    // Note: EnableDebugLogs still comes from the config directly in the refactored method
                    c.EnableDebugLogs == config.EnableDebugLogs &&

                    c.PreStopExePath == expectedDto.PreStopExecutablePath &&
                    c.PreStopWorkingDirectory == expectedDto.PreStopStartupDirectory &&
                    c.PreStopArgs == expectedDto.PreStopParameters &&
                    c.PreStopTimeout == expectedDto.PreStopTimeoutSeconds &&
                    c.PreStopLogAsError == expectedDto.PreStopLogAsError &&

                    c.PostStopExePath == expectedDto.PostStopExecutablePath &&
                    c.PostStopWorkingDirectory == expectedDto.PostStopStartupDirectory &&
                    c.PostStopArgs == expectedDto.PostStopParameters
                ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UninstallService_CalledWithCorrectServiceName()
        {
            var sut = CreateSut();
            var serviceName = "TestService";

            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName)).Returns(true);

            await sut.UninstallService(serviceName);

            _serviceManagerMock.Verify(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartService_CalledWithCorrectServiceName()
        {
            var sut = CreateSut();
            var serviceName = "TestService";

            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName)).Returns(true);
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);

            await sut.StartService(serviceName);

            _serviceManagerMock.Verify(m => m.StartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopService_CalledWithCorrectServiceName()
        {
            var sut = CreateSut();
            var serviceName = "TestService";

            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName)).Returns(true);

            await sut.StopService(serviceName);

            _serviceManagerMock.Verify(m => m.StopServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RestartService_CalledWithCorrectServiceName()
        {
            var sut = CreateSut();
            var serviceName = "TestService";

            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName)).Returns(true);
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);

            await sut.RestartService(serviceName);

            _serviceManagerMock.Verify(m => m.RestartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExportXmlCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var sut = CreateSut();

            // Use a unique temporary path to ensure thread-safety during parallel test execution
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");

            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            // FIX 1: Setup the parameterless delegate. 
            // In the refactored ServiceCommands, this is the source of the DTO.
            _modelToServiceDtoMock.Setup(m => m()).Returns(new ServiceDto { Name = "Service" });

            // FIX 2: Match the 3-parameter validator signature (ServiceDto, string, string).
            // ExportConfigAsync passes 'null' for the wrapper path.
            _serviceConfigurationValidator.Setup(d => d.Validate(
                It.IsAny<ServiceDto>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .ReturnsAsync(true);

            try
            {
                // Act - Direct await ensures exceptions are caught by the test runner
                await sut.ExportXmlConfig(string.Empty);

                // Assert
                _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                Assert.True(File.Exists(path), "The XML file was not physically written to disk.");
            }
            finally
            {
                // Cleanup the physical file generated by the static ServiceExporter
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ExportJsonCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var sut = CreateSut();

            // Use a temporary path to avoid permission issues and ensure a clean environment
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            // FIX 1: Ensure the validator mock matches the 3-parameter signature used in ExportConfigAsync.
            // We use 'string' to allow the 'null' wrapperExePath passed during exports.
            _serviceConfigurationValidator.Setup(d => d.Validate(
                It.IsAny<ServiceDto>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .ReturnsAsync(true);

            // FIX 2: Correct the delegate setup. It is a parameterless Func<ServiceDto>.
            _modelToServiceDtoMock.Setup(m => m()).Returns(new ServiceDto { Name = "Service" });

            try
            {
                // Act - Direct await is cleaner than the 'as Task' casting
                await sut.ExportJsonConfig(string.Empty);

                // Assert
                _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);

                // Verify the static ServiceExporter actually wrote the file
                Assert.True(File.Exists(path), "The JSON export file was not created.");
            }
            finally
            {
                // Cleanup the physical file created by ServiceExporter.ExportJson
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportXmlCommand_ValidFile_UpdatesModel()
        {
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };
            var path = Path.GetTempFileName();

            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);

            _serviceConfigurationValidator.Setup(v => v.Validate(
                It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            bool bindCalled = false;
            var sut = CreateSut(d =>
            {
                bindCalled = true;
                Assert.Equal("TestService", d.Name);
            });

            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(true);

            _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);

            var task = sut.ImportXmlConfig();
            if (task != null) await task;

            if (File.Exists(path)) File.Delete(path);

            Assert.True(bindCalled, "The logic exited before binding.");
        }

        [Fact]
        public async Task ImportJsonCommand_ValidFile_UpdatesModel()
        {
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };
            var path = Path.GetTempFileName();

            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);

            _serviceConfigurationValidator.Setup(v => v.Validate(
                It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            bool bindCalled = false;
            var sut = CreateSut(d =>
            {
                bindCalled = true;
                Assert.Equal("TestService", d.Name);
            });

            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(true);

            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);

            var task = sut.ImportJsonConfig();
            if (task != null) await task;

            if (File.Exists(path)) File.Delete(path);

            Assert.True(bindCalled, "The logic exited before binding.");
        }

        [Fact]
        public void ImportXmlCommand_UserCancels_ShowsNothing()
        {
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(string.Empty);

            sut.ImportXmlConfig();

            _messageBoxService.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ExportJsonCommand_UserCancels_ShowsNothing()
        {
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(string.Empty);

            sut.ExportJsonConfig(string.Empty);

            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}