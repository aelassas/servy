using Moq;
using Servy.Config;
using Servy.Core.Common;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using Servy.Models;
using Servy.Services;
using Servy.UI.Services;
using Servy.Validation;

namespace Servy.UnitTests.Services
{
    public class ServiceCommandsTests : IDisposable
    {
        private readonly string _wrapperPath = Core.Config.AppConfig.GetServyUIServicePath();
        private bool _createdWrapperFile = false;

        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidator;
        private readonly Mock<IXmlServiceValidator> _xmlServiceValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonServiceValidatorMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<Func<ServiceDto?>> _modelToServiceDtoMock;
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
            _modelToServiceDtoMock = new Mock<Func<ServiceDto?>>();

            // Setup functional operational defaults for safe falling executions
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

            SetupDummyWrapperExe();
        }

        private ServiceCommands CreateSut(Action<ServiceDto>? bindSpy = null)
        {
            return new ServiceCommands(
                modelToServiceDto: _modelToServiceDtoMock.Object,
                bindServiceDtoToModel: bindSpy ?? (dto => { }),
                serviceManager: _serviceManagerMock.Object,
                messageBoxService: _messageBoxServiceObject,
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

        private void SetupDummyWrapperExe()
        {
            try
            {
                var dir = Path.GetDirectoryName(_wrapperPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(_wrapperPath))
                {
                    File.WriteAllText(_wrapperPath, "dummy-binary-payload");
                    _createdWrapperFile = true; // Track that we actually created it
                }
            }
            catch (Exception ex)
            {
                // Don't just catch; log it. Setup failure should be visible.
                throw new InvalidOperationException($"Critical: Failed to setup dummy wrapper at {_wrapperPath}", ex);
            }
        }

        private IMessageBoxService _messageBoxServiceObject => _messageBoxService.Object;

        #region InstallService Branch and Catch Block Tests

        [Fact]
        public async Task InstallService_MissingWrapperExe_ReturnsFalseAndDisplaysError()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "BrokenWrapperService" };

            // Delete wrapper intentionally to force branch path execution
            var wrapperPath = Core.Config.AppConfig.GetServyUIServicePath();
            if (File.Exists(wrapperPath)) File.Delete(wrapperPath);

            // Act
            var result = await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_InvalidWrapperExePath, UiAppConfig.Caption), Times.Once);

            // Re-initialize for subsequent runs
            SetupDummyWrapperExe();
        }

        [Fact]
        public async Task InstallService_DtoNullFallback_ReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "NullDtoService" };
            _modelToServiceDtoMock.Setup(m => m()).Returns((ServiceDto?)null);

            // Act
            var result = await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task InstallService_RunAsLocalSystem_MasksUserAccountAndCredentials()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "LocalSysService", RunAsLocalSystem = true, ConfirmPassword = "abc" };
            var dto = new ServiceDto { Name = "LocalSysService", UserAccount = "OldUser", Password = "OldPassword" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), "abc", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.Null(dto.UserAccount);
            Assert.Null(dto.Password);
        }

        [Fact]
        public async Task InstallService_ValidationError_ReturnsFalseWithoutInstalling()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "InvalidService" };
            var dto = new ServiceDto { Name = "InvalidService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.False(result);
            _serviceManagerMock.Verify(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_ServiceExists_UserAbortsOverwrite_ReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "ExistingService" };
            var dto = new ServiceDto { Name = "ExistingService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.IsServiceInstalled("ExistingService", It.IsAny<CancellationToken>())).Returns(true);
            _messageBoxService.Setup(m => m.ShowConfirmAsync(Resources.Strings.Msg_ServiceAlreadyExists, UiAppConfig.Caption)).ReturnsAsync(false);

            // Act
            var result = await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.False(result);
            _serviceManagerMock.Verify(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_ManagerReturnsFailure_DisplaysErrorMessageBox()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "FailingInstallation" };
            var dto = new ServiceDto { Name = "FailingInstallation" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Access Denied OS Driver Error"));

            // Act
            var result = await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync("Access Denied OS Driver Error", UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task InstallService_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "SecureService" };
            var dto = new ServiceDto { Name = "SecureService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());

            // Act
            var result = await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task InstallService_GeneralException_DisplaysUnexpectedErrorAndReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "CrashingService" };
            var dto = new ServiceDto { Name = "CrashingService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Fatal Kernel Loop"));

            // Act
            var result = await sut.InstallService(config, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region OpenManager Method and Exceptions Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("C:\\NonExistent\\Manager.exe")]
        public async Task OpenManager_PathInvalidOrMissing(string? path)
        {
            // Arrange
            var sut = CreateSut();
            _appConfigMock.Setup(c => c.ManagerAppPublishPath).Returns(path);

            // Act
            await sut.OpenManager(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ManagerAppNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task OpenManager_ProcessStartThrowsException_DisplaysLaunchFailedError()
        {
            // Arrange
            var sut = CreateSut();

            // 1. Use a temporary file path that actually EXISTS on disk to pass the File.Exists check
            var tempExeFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.exe");
            File.WriteAllText(tempExeFile, "Pretend Executable Payload");

            _appConfigMock.Setup(c => c.ManagerAppPublishPath).Returns(tempExeFile);
            _appConfigMock.Setup(c => c.ForceSoftwareRendering).Returns(true);

            // 2. Clear out strings or simulate conditions where Process.Start throws an exception.
            // Since Process.Start with UseShellExecute = true throws a Win32Exception if the file 
            // is a fake text-based .exe file, the catch block will trigger natively!
            try
            {
                // Act
                await sut.OpenManager(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ManagerAppLaunchFailed, UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                // Clean up the temporary file context safely
                if (File.Exists(tempExeFile))
                {
                    File.Delete(tempExeFile);
                }
            }
        }

        #endregion

        #region ExecuteServiceCommandAsync Unified Pipeline Tests

        [Fact]
        public async Task ExecuteServiceCommand_ServiceNotInstalled_ReturnsFalseAndDisplaysError()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "MissingControlService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(false);

            // Act
            var result = await sut.StartService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExecuteServiceCommand_CheckDisabledActive_ServiceIsDisabled_ReturnsFalseAndDisplaysError()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "DisabledService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Disabled);

            // Act - StartService sets checkDisabled to true
            var result = await sut.StartService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ServiceDisabledError, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExecuteServiceCommand_ManagerOperationFails_DisplaysReturnedErrorMessage()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "FailingStateService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.StopServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Service is deadlocked. Control failed."));

            // Act - StopService leaves checkDisabled as false
            var result = await sut.StopService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync("Service is deadlocked. Control failed.", UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExecuteServiceCommand_UnauthorizedAccess_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "ProtectedControlService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.StartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());

            // Act
            var result = await sut.StartService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExecuteServiceCommand_GeneralException_DisplaysUnexpectedError()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "ExceptionControlService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.StartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("RPC Server Unavailable"));

            // Act
            var result = await sut.StartService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region IsServiceNameValid Conditional Branch Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Invalid/Name\\WithSpecialChars")]
        public async Task IsServiceNameValid_InvalidScenarios_ReturnsFalseAndDisplaysWarning(string? serviceName)
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var result = await sut.UninstallService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            // Verify core localization payload warning mechanics fired
            _messageBoxService.Verify(m => m.ShowWarningAsync(It.IsAny<string>(), UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region ExportConfigAsync Format Conditional Branch and Catch Tests

        [Fact]
        public async Task ExportConfig_UserCancelsFileDialog_ExitsEarlyWithoutProcessing()
        {
            // Arrange
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(string.Empty);

            // Act
            await sut.ExportXmlConfig("password", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _modelToServiceDtoMock.Verify(m => m(), Times.Never);
        }

        [Fact]
        public async Task ExportConfig_ValidationError_AbortsExportFileWriting()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            var dto = new ServiceDto { Name = "BadExport" };
            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, null, "password", It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            await sut.ExportXmlConfig("password", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(File.Exists(path));
            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExportConfig_SerializationExceptionThrown_CatchCatchesAndShowsUnexpectedError()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            var dto = new ServiceDto { Name = "CrashExport" };
            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, null, "password", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Emulating an internal static/serializer fault path execution
            _modelToServiceDtoMock.Setup(m => m()).Throws(new IOException("Disk Full / Access Denied"));

            // Act
            await sut.ExportXmlConfig("password", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region ImportConfigAsync Security Gates and Catch Block Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task ImportConfig_UserCancelsFileDialog_ExitsEarly(string? returnedPath)
        {
            // Arrange
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(returnedPath!);

            // Act
            await sut.ImportXmlConfig(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _xmlServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string?>.IsAny), Times.Never);
        }

        [Fact]
        public async Task ImportConfig_SecurityGuardFails_DisplaysGuardErrorMessage()
        {
            // Arrange
            var sut = CreateSut();
            // Trigger UNC path block criteria explicitly
            var uncPath = @"\\MaliciousServer\Share\attack.xml";
            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(uncPath);

            // Act
            await sut.ImportXmlConfig(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxService.Verify(m => m.ShowErrorAsync(Core.Resources.Strings.Msg_SecurityUncPathProhibited, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ImportConfig_ContentValidationFails_DisplaysSyntaxErrorReason()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{ invalid json structure }");
            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);

            string? errorOut = "Missing closing brace delimiter.";
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out errorOut)).Returns(false);

            try
            {
                // Act
                await sut.ImportJsonConfig(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                _messageBoxService.Verify(m => m.ShowErrorAsync("Missing closing brace delimiter.", UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportConfig_DeserializationReturnsNull_DisplaysLoadErrorMessage()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
            File.WriteAllText(path, "<service />");
            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);

            string? errorOut = null;
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out errorOut)).Returns(true);
            _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns((ServiceDto?)null);

            try
            {
                // Act
                await sut.ImportXmlConfig(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_FailedToLoadXml, UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportConfig_DomainValidationFails_LogsInfoAndAbortsBinding()
        {
            // Arrange
            var sampleDto = new ServiceDto { Name = "InvalidDomainImport" };
            bool bindCalled = false;
            var sut = CreateSut(spy => { bindCalled = true; });

            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{}");
            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);

            string? errorOut = null;
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out errorOut)).Returns(true);
            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(sampleDto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(sampleDto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            try
            {
                // Act
                await sut.ImportJsonConfig(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.False(bindCalled);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportConfig_GeneralException_DisplaysUnexpectedError()
        {
            // Arrange
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.OpenXml()).Throws(new IOException("Hardware File Lock Denied"));

            // Act
            await sut.ImportXmlConfig(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region UninstallService Branch and Catch Block Tests

        [Fact]
        public async Task UninstallService_ServiceNotInstalled_ReturnsFalseAndDisplaysError()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "MissingUninstallService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(false);

            // Act
            var result = await sut.UninstallService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
            _serviceManagerMock.Verify(m => m.UninstallServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UninstallService_ManagerReturnsFailure_ReturnsFalseAndDisplaysErrorMessage()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "StuckService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Service marked for deletion."));

            // Act
            var result = await sut.UninstallService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync("Service marked for deletion.", UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task UninstallService_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "SecureSystemService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());

            // Act
            var result = await sut.UninstallService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task UninstallService_GeneralException_DisplaysUnexpectedErrorAndReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "CrashingUninstallService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("WMI Registry Failure"));

            // Act
            var result = await sut.UninstallService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task UninstallService_SuccessfulRemoval_DisplaysSuccessMessageBoxAndReturnsTrue()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "ValidInstalledService";

            // 1. Pass the IsServiceNameValid gate implicitly by using a standard name string
            // 2. Pass the IsServiceInstalled check gate
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>()))
                .Returns(true);

            // 3. Force UninstallServiceAsync to return a successful operational track result
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await sut.UninstallService(serviceName, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Verify that the success info dialog box line was executed with the expected arguments
            _messageBoxService.Verify(m =>
                m.ShowInfoAsync(Resources.Strings.Msg_ServiceRemoved, UiAppConfig.Caption),
                Times.Once);
        }

        #endregion

        #region RestartService Branch and Catch Block Tests

        [Fact]
        public async Task RestartService_SuccessfulExecution_DisplaysInfoAndReturnsTrue()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "HealthyService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);
            _serviceManagerMock.Setup(m => m.RestartServiceAsync(serviceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await sut.RestartService(serviceName, CancellationToken.None);

            // Assert
            Assert.True(result);
            _messageBoxService.Verify(m => m.ShowInfoAsync(Resources.Strings.Msg_ServiceRestarted, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task RestartService_ServiceDisabled_AbortsWithDisabledError()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "DisabledRestartService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Disabled);

            // Act
            var result = await sut.RestartService(serviceName, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ServiceDisabledError, UiAppConfig.Caption), Times.Once);
            _serviceManagerMock.Verify(m => m.RestartServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region ExportJsonConfig Branch and Catch Block Tests

        [Fact]
        public async Task ExportJsonConfig_UserCancelsFileDialog_ExitsEarlyWithoutProcessing()
        {
            // Arrange
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(string.Empty);

            // Act
            await sut.ExportJsonConfig("secretPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _modelToServiceDtoMock.Verify(m => m(), Times.Never);
        }

        [Fact]
        public async Task ExportJsonConfig_ValidationError_AbortsExportFileWriting()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            var dto = new ServiceDto { Name = "BadJsonExport" };
            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, null, "secretPassword", It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            await sut.ExportJsonConfig("secretPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(File.Exists(path));
            _messageBoxService.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExportJsonConfig_SerializationExceptionThrown_CatchCatchesAndShowsUnexpectedError()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            var dto = new ServiceDto { Name = "CrashJsonExport" };
            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(dto, null, "secretPassword", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Force evaluation down the catch lane by breaking dependencies on data extraction execution
            _modelToServiceDtoMock.Setup(m => m()).Throws(new UnauthorizedAccessException("I/O Lock Encountered"));

            // Act
            await sut.ExportJsonConfig("secretPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxService.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region ExportConfigAsync Delegate Invocation Tests

        [Fact]
        public async Task ExportConfigAsync_ValidModel_ExecutesExportActionDelegate()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            var dto = new ServiceDto { Name = "DelegateTestService" };
            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);

            _serviceConfigurationValidator.Setup(d => d.ValidateAsync(
                It.IsAny<ServiceDto>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            bool delegateWasInvoked = false;
            ServiceDto? capturedDto = null;
            string? capturedPath = null;

            // Define an explicit action spy to pass into the system pipeline
            Action<ServiceDto?, string> spyExportAction = (passedDto, passedPath) =>
            {
                delegateWasInvoked = true;
                capturedDto = passedDto;
                capturedPath = passedPath;
            };

            // Act
            // Invoke the private ExportConfigAsync directly via reflection to exercise the exportAction delegate
            var privateMethod = typeof(ServiceCommands).GetMethod("ExportConfigAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = (Task)privateMethod!.Invoke(sut, new object?[]
            {
                "password",
                new Func<string?>(() => path),
                spyExportAction,
                "JSON",
                "Success",
                CancellationToken.None,
            })!;

            await task;

            // Assert
            Assert.True(delegateWasInvoked, "The exportAction delegate parameter was never executed.");
            Assert.Equal(dto, capturedDto);
            Assert.Equal(path, capturedPath);
        }

        #endregion

        #region ImportConfigAsync Target Binding Tests

        [Fact]
        public async Task ImportXmlConfig_AllGatesPassed_InvokesBindServiceDtoToModel()
        {
            // Arrange
            var expectedDto = new ServiceDto { Name = "XmlBindService", ExecutablePath = @"C:\Windows\System32\cmd.exe" };

            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
            File.WriteAllText(path, "<service></service>"); // Setup local physical file context to satisfy Guard

            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);

            string? validationError = null;
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out validationError)).Returns(true);
            _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(expectedDto);

            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(expectedDto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            bool bindActionWasExecuted = false;
            ServiceDto? boundDtoResult = null;

            // Instantiating the SUT with our tracking verification callback action stub
            var sut = CreateSut(dto =>
            {
                bindActionWasExecuted = true;
                boundDtoResult = dto;
            });

            try
            {
                // Act
                await sut.ImportXmlConfig(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.True(bindActionWasExecuted, "The _bindServiceDtoToModel(dto) logic line was not executed.");
                Assert.NotNull(boundDtoResult);
                Assert.Equal("XmlBindService", boundDtoResult.Name);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportJsonConfig_AllGatesPassed_InvokesBindServiceDtoToModel()
        {
            // Arrange
            var expectedDto = new ServiceDto { Name = "JsonBindService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };

            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{}"); // Setup local physical file context to satisfy Guard

            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);

            string? validationError = null;
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out validationError)).Returns(true);
            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(expectedDto);

            _serviceConfigurationValidator.Setup(v => v.ValidateAsync(expectedDto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            bool bindActionWasExecuted = false;
            ServiceDto? boundDtoResult = null;

            var sut = CreateSut(dto =>
            {
                bindActionWasExecuted = true;
                boundDtoResult = dto;
            });

            try
            {
                // Act
                await sut.ImportJsonConfig(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.True(bindActionWasExecuted, "The _bindServiceDtoToModel(dto) logic line was not executed.");
                Assert.NotNull(boundDtoResult);
                Assert.Equal("JsonBindService", boundDtoResult.Name);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion

        #region Dispose implementation

        public void Dispose()
        {
            if (_createdWrapperFile && File.Exists(_wrapperPath))
            {
                try { File.Delete(_wrapperPath); }
                catch { /* Best effort cleanup */ }
            }
        }

        #endregion
    }
}