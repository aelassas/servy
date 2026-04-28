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

            // Setup CursorService to return a dummy disposable for 'using' blocks
            _cursorServiceMock.Setup(c => c.SetWaitCursor()).Returns(Mock.Of<IDisposable>());
        }

        private ServiceCommands CreateSut(Action<ServiceDto> bindSpy = null)
        {
            return new ServiceCommands(
                modelToServiceDto: () => new ServiceDto(),
                bindServiceDtoToModel: bindSpy ?? (dto => { }),
                serviceManager: _serviceManagerMock.Object,
                messageBoxService: _messageBoxService.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidator.Object,
                xmlServiceValidator: _xmlServiceValidatorMock.Object,
                jsonServiceValidator: _jsonServiceValidatorMock.Object,
                appConfig: _appConfigMock.Object,
                cursorService: _cursorServiceMock.Object
            );
        }

        [Fact]
        public async Task InstallService_CalledWithCorrectConfiguration()
        {
            // Arrange
            var sut = CreateSut();
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
                PostStopParameters = "postStopArgs"
            };

            // CRITICAL FIX: Ensure the wrapper executable exists for the test to bypass the early exit check
            var wrapperPath = Core.Config.AppConfig.GetServyUIServicePath();
            try
            {
                var dir = Path.GetDirectoryName(wrapperPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(wrapperPath)) File.WriteAllText(wrapperPath, "dummy");
            }
            catch { /* Ignore creation errors if running in restricted environments */ }

            _serviceConfigurationValidator
                .Setup(v => v.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await sut.InstallService(config);

            // Assert
            Assert.True(result, "InstallService returned false. The validation or File.Exists check failed.");

            _serviceManagerMock.Verify(m => m.InstallServiceAsync(
                It.Is<InstallServiceOptions>(c =>
                    c.ServiceName == config.Name &&
                    c.DisplayName == config.DisplayName &&
                    c.Description == config.Description &&
                    c.RealExePath == config.ExecutablePath &&
                    c.WorkingDirectory == config.StartupDirectory &&
                    c.RealArgs == config.Parameters &&
                    c.StartType == config.StartupType &&
                    c.ProcessPriority == config.Priority &&

                    c.StdoutPath == config.StdoutPath &&
                    c.StderrPath == config.StderrPath &&
                    c.EnableSizeRotation == config.EnableSizeRotation &&
                    c.RotationSizeInBytes == (ulong.Parse(config.RotationSize) * 1024 * 1024) &&
                    c.EnableDateRotation == config.EnableDateRotation &&
                    c.DateRotationType == config.DateRotationType &&
                    c.MaxRotations == int.Parse(config.MaxRotations) &&
                    c.UseLocalTimeForRotation == config.UseLocalTimeForRotation &&

                    c.EnableHealthMonitoring == config.EnableHealthMonitoring &&
                    c.HeartbeatInterval == int.Parse(config.HeartbeatInterval) &&
                    c.MaxFailedChecks == int.Parse(config.MaxFailedChecks) &&
                    c.RecoveryAction == config.RecoveryAction &&
                    c.MaxRestartAttempts == int.Parse(config.MaxRestartAttempts) &&
                    c.FailureProgramPath == config.FailureProgramPath &&
                    c.FailureProgramWorkingDirectory == config.FailureProgramStartupDirectory &&
                    c.FailureProgramArgs == config.FailureProgramParameters &&

                    // CRITICAL FIX: The production code normalizes these strings, so the mock must expect the normalized version
                    c.EnvironmentVariables == StringHelper.NormalizeString(config.EnvironmentVariables) &&
                    c.ServiceDependencies == config.ServiceDependencies &&
                    c.Username == config.UserAccount &&
                    c.Password == config.Password &&

                    c.PreLaunchExePath == config.PreLaunchExecutablePath &&
                    c.PreLaunchWorkingDirectory == config.PreLaunchStartupDirectory &&
                    c.PreLaunchArgs == config.PreLaunchParameters &&

                    // CRITICAL FIX: Same normalization needed here
                    c.PreLaunchEnvironmentVariables == StringHelper.NormalizeString(config.PreLaunchEnvironmentVariables) &&
                    c.PreLaunchStdoutPath == config.PreLaunchStdoutPath &&
                    c.PreLaunchStderrPath == config.PreLaunchStderrPath &&
                    c.PreLaunchTimeout == int.Parse(config.PreLaunchTimeoutSeconds) &&
                    c.PreLaunchRetryAttempts == int.Parse(config.PreLaunchRetryAttempts) &&
                    c.PreLaunchIgnoreFailure == config.PreLaunchIgnoreFailure &&

                    c.PostLaunchExePath == config.PostLaunchExecutablePath &&
                    c.PostLaunchWorkingDirectory == config.PostLaunchStartupDirectory &&
                    c.PostLaunchArgs == config.PostLaunchParameters &&
                    c.StartTimeout == int.Parse(config.StartTimeout) &&
                    c.StopTimeout == int.Parse(config.StopTimeout) &&
                    c.EnableDebugLogs == config.EnableDebugLogs &&

                    c.PreStopExePath == config.PreStopExecutablePath &&
                    c.PreStopWorkingDirectory == config.PreStopStartupDirectory &&
                    c.PreStopArgs == config.PreStopParameters &&
                    c.PreStopTimeout == int.Parse(config.PreStopTimeoutSeconds) &&
                    c.PreStopLogAsError == config.PreStopLogAsError &&

                    c.PostStopExePath == config.PostStopExecutablePath &&
                    c.PostStopWorkingDirectory == config.PostStopStartupDirectory &&
                    c.PostStopArgs == config.PostStopParameters
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
            var sut = CreateSut();
            var path = "export.xml";
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>())).ReturnsAsync(true);

            var task = sut.ExportXmlConfig(string.Empty) as Task;
            if (task != null) await task;

            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public async Task ExportJsonCommand_ValidModel_ShowsSuccessMessage()
        {
            var sut = CreateSut();
            var path = "export.json";
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>())).ReturnsAsync(true);

            var task = sut.ExportJsonConfig(string.Empty) as Task;
            if (task != null) await task;

            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public async Task ImportXmlCommand_ValidFile_UpdatesModel()
        {
            var realPath = @"C:\Windows\System32\notepad.exe";
            var xmlContent = $@"<ServiceDto><Name>TestService</Name><ExecutablePath>{realPath}</ExecutablePath></ServiceDto>";
            var path = Path.GetTempFileName() + ".xml";

            _serviceConfigurationValidator.Setup(d => d.Validate(
                It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>())).ReturnsAsync(true);

            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);

            var bindCalled = false;
            ServiceDto capturedDto = null;
            Action<ServiceDto> bindSpy = dto =>
            {
                bindCalled = true;
                capturedDto = dto;
            };

            var sut = CreateSut(bindSpy);

            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(true);

            File.WriteAllText(path, xmlContent);

            var task = sut.ImportXmlConfig() as Task;
            if (task != null) await task;

            try
            {
                Assert.True(bindCalled, "The logic exited before binding. Likely XmlServiceValidator.TryValidate failed.");
                Assert.NotNull(capturedDto);
                Assert.Equal("TestService", capturedDto.Name);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportJsonCommand_ValidFile_UpdatesModel()
        {
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };
            var jsonContent = JsonConvert.SerializeObject(dto);
            var path = Path.GetTempFileName();
            File.WriteAllText(path, jsonContent);

            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);

            _serviceConfigurationValidator.Setup(v => v.Validate(
                It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>())).ReturnsAsync(true);

            bool bindCalled = false;
            var sut = CreateSut(d => {
                bindCalled = true;
                Assert.Equal("TestService", d.Name);
            });

            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(true);

            var task = sut.ImportJsonConfig() as Task;
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