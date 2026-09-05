using Moq;
using Servy.Config;
using Servy.Core.Common;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Services;
using Servy.Models;
using Servy.Services;
using Servy.Testing;
using Servy.UI.Services;
using Servy.Validation;
using System.Diagnostics;

namespace Servy.UnitTests.Services
{
    public class ServiceCommandsTests : IDisposable
    {
        private readonly string _wrapperPath = Core.Config.AppConfig.GetServyUIServicePath();
        private bool _createdWrapperFile = false;

        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidatorMock;
        private readonly Mock<IXmlServiceValidator> _xmlServiceValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonServiceValidatorMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<Func<ServiceDto?>> _modelToServiceDtoMock;
        private readonly Mock<IXmlServiceSerializer> _xmlServiceSerializerMock;
        private readonly Mock<IJsonServiceSerializer> _jsonServiceSerializerMock;
        private readonly Mock<IProcessHelper> _processHelperMock;

        public ServiceCommandsTests()
        {
            _dialogServiceMock = new Mock<IFileDialogService>();
            _serviceManagerMock = new Mock<IServiceManager>();
            _messageBoxServiceMock = new Mock<IMessageBoxService>();
            _serviceConfigurationValidatorMock = new Mock<IServiceConfigurationValidator>();
            _xmlServiceValidatorMock = new Mock<IXmlServiceValidator>();
            _jsonServiceValidatorMock = new Mock<IJsonServiceValidator>();
            _appConfigMock = new Mock<IAppConfiguration>();
            _cursorServiceMock = new Mock<ICursorService>();
            _xmlServiceSerializerMock = new Mock<IXmlServiceSerializer>();
            _jsonServiceSerializerMock = new Mock<IJsonServiceSerializer>();
            _modelToServiceDtoMock = new Mock<Func<ServiceDto?>>();
            _processHelperMock = new Mock<IProcessHelper>();

            // Default all service-manager operations to success
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
                messageBoxService: _messageBoxServiceMock.Object,
                dialogService: _dialogServiceMock.Object,
                serviceConfigurationValidator: _serviceConfigurationValidatorMock.Object,
                xmlServiceValidator: _xmlServiceValidatorMock.Object,
                jsonServiceValidator: _jsonServiceValidatorMock.Object,
                appConfig: _appConfigMock.Object,
                cursorService: _cursorServiceMock.Object,
                xmlServiceSerializer: _xmlServiceSerializerMock.Object,
                jsonServiceSerializer: _jsonServiceSerializerMock.Object,
                processHelper: _processHelperMock.Object
            );
        }

        /// <summary>
        /// Instantiates an OperationResult with IsSuccess = false and a blank ErrorMessage
        /// via TestReflection on its private constructor.
        /// </summary>
        private static OperationResult CreateBlankFailureOperationResult(string? errorMessage)
        {
            return TestReflection.CreateInstance<OperationResult>(false, errorMessage);
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
                // Fail loudly: setup failure must be visible, not swallowed
                throw new InvalidOperationException($"Critical: Failed to setup dummy wrapper at {_wrapperPath}", ex);
            }
        }

        #region InstallService Branch and Catch Block Tests

        [Fact]
        public async Task InstallService_MissingWrapperExe_ReturnsFalseAndDisplaysError()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "BrokenWrapperService" };

            // Delete wrapper intentionally to force branch path execution
            var wrapperPath = Core.Config.AppConfig.GetServyUIServicePath();
            var backup = wrapperPath + ".bak";

            try
            {
                if (File.Exists(wrapperPath)) File.Move(wrapperPath, backup);

                // Act
                var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

                // Assert
                Assert.False(result);
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_InvalidWrapperExePath, UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                if (File.Exists(backup))
                {
                    if (File.Exists(wrapperPath)) File.Delete(wrapperPath);
                    File.Move(backup, wrapperPath);
                }
                else
                {
                    SetupDummyWrapperExe();
                }
            }
        }

        [Fact]
        public async Task InstallService_DtoNullFallback_ReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "NullDtoService" };
            _modelToServiceDtoMock.Setup(m => m()).Returns((ServiceDto?)null);

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ValidationError, UiAppConfig.Caption), Times.Once);
            _serviceConfigurationValidatorMock.Verify(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_RunAsLocalSystem_MasksUserAccountAndCredentials()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "LocalSysService", RunAsLocalSystem = true, ConfirmPassword = "abc" };
            var dto = new ServiceDto { Name = "LocalSysService", UserAccount = "OldUser", Password = "OldPassword" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), "abc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

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
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _serviceManagerMock.Verify(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_ValidConfiguration_ReturnsTrueAndDisplaysSuccess()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "ValidInstallService" };
            var dto = new ServiceDto { Name = "ValidInstallService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.IsServiceInstalled("ValidInstallService", It.IsAny<CancellationToken>())).Returns(false);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Resources.Strings.Msg_ServiceInstalled, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task InstallService_ServiceExists_UserAbortsOverwrite_ReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "ExistingService" };
            var dto = new ServiceDto { Name = "ExistingService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.IsServiceInstalled("ExistingService", It.IsAny<CancellationToken>())).Returns(true);
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Resources.Strings.Msg_ServiceAlreadyExists, UiAppConfig.Caption)).ReturnsAsync(false);

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _serviceManagerMock.Verify(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_ServiceExists_UserConfirmsOverwrite_ProceedsWithInstall()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "ExistingServiceToOverwrite" };
            var dto = new ServiceDto { Name = "ExistingServiceToOverwrite" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.IsServiceInstalled("ExistingServiceToOverwrite", It.IsAny<CancellationToken>())).Returns(true);
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Resources.Strings.Msg_ServiceAlreadyExists, UiAppConfig.Caption)).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _serviceManagerMock.Verify(m => m.InstallServiceAsync(It.Is<InstallServiceOptions>(o => o.ServiceName == "ExistingServiceToOverwrite"), It.IsAny<CancellationToken>()), Times.Once);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Resources.Strings.Msg_ServiceInstalled, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task InstallService_ManagerReturnsFailure_DisplaysErrorMessageBox()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "FailingInstallation" };
            var dto = new ServiceDto { Name = "FailingInstallation" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Access Denied OS Driver Error"));

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync("Access Denied OS Driver Error", UiAppConfig.Caption), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public async Task InstallService_ManagerFailsWithoutMessage_DisplaysUnexpectedErrorFallback(string? blankMessage)
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "SilentInstallFailureService" };
            var dto = new ServiceDto { Name = "SilentInstallFailureService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateBlankFailureOperationResult(blankMessage));

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task InstallService_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "SecureService" };
            var dto = new ServiceDto { Name = "SecureService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task InstallService_GeneralException_DisplaysUnexpectedErrorAndReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "CrashingService" };
            var dto = new ServiceDto { Name = "CrashingService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Fatal Kernel Loop"));

            // Act
            var result = await sut.InstallServiceAsync(config, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
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
            await sut.OpenManagerAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ManagerAppNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task OpenManager_ProcessStartThrowsUnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            string baseDir = AppFoldersHelper.GetAppDirectory();
            string tempTrackingFile = Path.Combine(baseDir, $"{Guid.NewGuid():N}.exe");
            File.WriteAllText(tempTrackingFile, string.Empty);

            ProcessStartInfo? captured = null;
            _appConfigMock.Setup(c => c.ManagerAppPublishPath).Returns(tempTrackingFile);
            _appConfigMock.Setup(c => c.ForceSoftwareRendering).Returns(false);
            _processHelperMock
                .Setup(h => h.Start(It.IsAny<ProcessStartInfo>()))
                .Callback<ProcessStartInfo>(psi => captured = psi)
                .Throws(new UnauthorizedAccessException("Access denied when starting process"));

            var sut = CreateSut();

            try
            {
                // Act
                await sut.OpenManagerAsync(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.NotNull(captured);
                Assert.Equal(tempTrackingFile, captured.FileName);
                Assert.True(captured.UseShellExecute);
                Assert.Equal($"\"{Core.Config.AppConfig.SkipSplashArgument}\"", captured.Arguments);

                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(
                    Resources.Strings.Msg_AdminRightsRequired,
                    UiAppConfig.Caption),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempTrackingFile))
                {
                    try { File.Delete(tempTrackingFile); } catch { /* fail-silent */ }
                }
            }
        }

        [Fact]
        public async Task OpenManager_ProcessStartThrowsException_DisplaysLaunchFailedError()
        {
            // Arrange
            // Create a real temp .exe so the File.Exists gate passes
            string tempTrackingFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid():N}.exe");
            File.WriteAllText(tempTrackingFile, string.Empty);

            ProcessStartInfo? captured = null;
            _appConfigMock.Setup(c => c.ManagerAppPublishPath).Returns(tempTrackingFile);
            _appConfigMock.Setup(c => c.ForceSoftwareRendering).Returns(true);

            // MOCK INDEPENDENT EXCEPTION SEAM: Force the process helper to throw an explicit error natively,
            // bypassing any real operating system ShellExecute tracking side effects.
            _processHelperMock
                .Setup(h => h.Start(It.IsAny<ProcessStartInfo>()))
                .Callback<ProcessStartInfo>(psi => captured = psi)
                .Throws(new System.ComponentModel.Win32Exception(5, "Access denied simulation")); // 5 = ERROR_ACCESS_DENIED

            var sut = CreateSut();

            try
            {
                // Act
                await sut.OpenManagerAsync(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.NotNull(captured);
                Assert.Equal(tempTrackingFile, captured.FileName);
                Assert.True(captured.UseShellExecute);
                Assert.Equal($"\"{Core.Config.AppConfig.SkipSplashArgument}\" {Core.Config.AppConfig.ForceSoftwareRenderingArg}", captured.Arguments);

                // Verify that the UI correctly intercepts the failure and displays the targeted error message text
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(
                    Resources.Strings.Msg_ManagerAppLaunchFailed,
                    UiAppConfig.Caption),
                    Times.Once);
            }
            finally
            {
                // Clean up the tracking file context safely
                if (File.Exists(tempTrackingFile))
                {
                    File.Delete(tempTrackingFile);
                }
            }
        }

        #endregion

        #region OpenSecurityHardeningGuide Method Tests

        [Fact]
        public async Task OpenSecurityHardeningGuide_ProcessStartsSuccessfully_StartsProcessWithCorrectUri()
        {
            // Arrange
            using (var currentProcess = Process.GetCurrentProcess())
            {
                _processHelperMock
                    .Setup(h => h.Start(It.Is<ProcessStartInfo>(psi =>
                        psi.FileName == Core.Config.AppConfig.SecurityHardeningGuideLink &&
                        psi.UseShellExecute)))
                    .Returns(currentProcess);

                var sut = CreateSut();

                // Act
                await sut.OpenSecurityHardeningGuideAsync(CancellationToken.None);

                // Assert
                _processHelperMock.Verify(h => h.Start(It.Is<ProcessStartInfo>(psi =>
                    psi.FileName == Core.Config.AppConfig.SecurityHardeningGuideLink &&
                    psi.UseShellExecute)), Times.Once);
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
        }

        [Fact]
        public async Task OpenSecurityHardeningGuide_ProcessReturnsNull_SucceedsWithoutError()
        {
            // Arrange
            _processHelperMock
                .Setup(h => h.Start(It.IsAny<ProcessStartInfo>()))
                .Returns((Process?)null);

            var sut = CreateSut();

            // Act
            await sut.OpenSecurityHardeningGuideAsync(TestContext.Current.CancellationToken);

            // Assert: Handing off to an existing browser instance (returning null) should NOT trigger an error popup
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OpenSecurityHardeningGuide_ProcessStartThrowsException_DisplaysError()
        {
            // Arrange
            _processHelperMock
                .Setup(h => h.Start(It.IsAny<ProcessStartInfo>()))
                .Throws(new System.ComponentModel.Win32Exception("Failed to open browser"));

            var sut = CreateSut();

            // Act
            await sut.OpenSecurityHardeningGuideAsync(TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
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
            var result = await sut.StartServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
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
            var result = await sut.StartServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ServiceDisabledError, UiAppConfig.Caption), Times.Once);
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
            var result = await sut.StopServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync("Service is deadlocked. Control failed.", UiAppConfig.Caption), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public async Task ExecuteServiceCommand_ManagerFailsWithoutMessage_DisplaysUnexpectedErrorFallback(string? blankMessage)
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "SilentControlFailureService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.StopServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateBlankFailureOperationResult(blankMessage));

            // Act
            var result = await sut.StopServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
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
            var result = await sut.StartServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
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
            var result = await sut.StartServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        #endregion

        #region Start & Stop Success Path Tests

        [Fact]
        public async Task StartService_SuccessfulExecution_DisplaysInfoAndReturnsTrue()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "StartableService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);
            _serviceManagerMock.Setup(m => m.StartServiceAsync(serviceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await sut.StartServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Resources.Strings.Msg_ServiceStarted, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task StopService_SuccessfulExecution_DisplaysInfoAndReturnsTrue()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "StoppableService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.StopServiceAsync(serviceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await sut.StopServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Resources.Strings.Msg_ServiceStopped, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        #endregion

        #region IsServiceNameValid Conditional Branch Tests

        [Theory]
        [InlineData(null, nameof(Strings.Msg_ValidationError))]
        [InlineData("", nameof(Strings.Msg_ValidationError))]
        [InlineData("    ", nameof(Strings.Msg_ValidationError))]
        [InlineData("Invalid/Name\\WithSpecialChars", nameof(Strings.Msg_InvalidServiceName))]
        public async Task IsServiceNameValid_InvalidScenarios_ReturnsFalseAndDisplaysWarning(string? serviceName, string expectedResourceKey)
        {
            // Arrange
            var sut = CreateSut();
            var expected = Strings.ResourceManager.GetString(expectedResourceKey)!;

            // Act
            var result = await sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(expected, UiAppConfig.Caption), Times.Once);
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
            await sut.ExportXmlConfigAsync("password", cancellationToken: TestContext.Current.CancellationToken);

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
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, null, "password", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            await sut.ExportXmlConfigAsync("password", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(File.Exists(path));
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExportXmlConfig_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            _modelToServiceDtoMock.Setup(m => m()).Throws(new UnauthorizedAccessException());

            // Act
            await sut.ExportXmlConfigAsync("password", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task ExportConfig_ModelExtractionThrows_ShowsUnexpectedError()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            // Emulating an internal static/serializer fault path execution
            _modelToServiceDtoMock.Setup(m => m()).Throws(new IOException("Disk Full / Access Denied"));

            // Act
            await sut.ExportXmlConfigAsync("password", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
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
            _dialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string?>())).Returns(returnedPath!);

            // Act
            await sut.ImportXmlConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

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
            _dialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string?>())).Returns(uncPath);

            // Act
            await sut.ImportXmlConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_SecurityUncPathProhibited, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ImportConfig_ContentValidationFails_DisplaysSyntaxErrorReason()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{ invalid json structure }");
            _dialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string?>())).Returns(path);

            string? errorOut = "Missing closing brace delimiter.";
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out errorOut)).Returns(false);

            try
            {
                // Act
                await sut.ImportJsonConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync("Missing closing brace delimiter.", UiAppConfig.Caption), Times.Once);
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
            _dialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string?>())).Returns(path);

            string? errorOut = null;
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out errorOut)).Returns(true);
            _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns((ServiceDto?)null);

            try
            {
                // Act
                await sut.ImportXmlConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_FailedToLoadXml, UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportConfig_DomainValidationFails_AbortsBinding()
        {
            // Arrange
            var sampleDto = new ServiceDto { Name = "InvalidDomainImport" };
            bool bindCalled = false;
            var sut = CreateSut(spy => { bindCalled = true; });

            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{}");
            _dialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string?>())).Returns(path);

            string? errorOut = null;
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out errorOut)).Returns(true);
            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(sampleDto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(sampleDto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            try
            {
                // Act
                await sut.ImportJsonConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.False(bindCalled);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportXmlConfig_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string?>())).Throws(new UnauthorizedAccessException("Access denied"));

            // Act
            await sut.ImportXmlConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task ImportConfig_GeneralException_DisplaysUnexpectedError()
        {
            // Arrange
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string?>())).Throws(new IOException("Hardware File Lock Denied"));

            // Act
            await sut.ImportXmlConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        #endregion

        #region UninstallService Branch and Catch Block Tests

        [Fact]
        public async Task UninstallService_ServiceNotInstalled_ReturnsFalseAndDisplaysError()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "MissingUninstallService";
            string errorMsg = $"Service '{serviceName}' does not exist.";
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure(errorMsg));
            // Act
            var result = await sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(errorMsg, UiAppConfig.Caption), Times.Once);
            _serviceManagerMock.Verify(m => m.UninstallServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UninstallService_ManagerReturnsFailure_ReturnsFalseAndDisplaysErrorMessage()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "StuckService";
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Service marked for deletion."));

            // Act
            var result = await sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync("Service marked for deletion.", UiAppConfig.Caption), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public async Task UninstallService_ManagerFailsWithoutMessage_DisplaysUnexpectedErrorFallback(string? blankMessage)
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "SilentFailureService";
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateBlankFailureOperationResult(blankMessage));

            // Act
            var result = await sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task UninstallService_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "SecureSystemService";
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());

            // Act
            var result = await sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task UninstallService_GeneralException_DisplaysUnexpectedErrorAndReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "CrashingUninstallService";
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("WMI Registry Failure"));

            // Act
            var result = await sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task UninstallService_SuccessfulRemoval_DisplaysSuccessMessageBoxAndReturnsTrue()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "ValidInstalledService";

            // 1. Pass the IsServiceNameValid gate implicitly by using a standard name string
            // 2. Force UninstallServiceAsync to return a successful operational track result
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);

            // Verify that the success info dialog box line was executed with the expected arguments
            _messageBoxServiceMock.Verify(m =>
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
            var result = await sut.RestartServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Resources.Strings.Msg_ServiceRestarted, UiAppConfig.Caption), Times.Once);
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
            var result = await sut.RestartServiceAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ServiceDisabledError, UiAppConfig.Caption), Times.Once);
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
            await sut.ExportJsonConfigAsync("secretPassword", cancellationToken: TestContext.Current.CancellationToken);

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
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, null, "secretPassword", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            await sut.ExportJsonConfigAsync("secretPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(File.Exists(path));
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExportJsonConfig_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            _modelToServiceDtoMock.Setup(m => m()).Throws(new UnauthorizedAccessException());

            // Act
            await sut.ExportJsonConfigAsync("secretPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task ExportJsonConfig_ModelExtractionThrows_ShowsUnexpectedError()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            // Force evaluation down the catch lane by breaking dependencies on data extraction execution
            _modelToServiceDtoMock.Setup(m => m()).Throws(new InvalidOperationException("Boom!"));

            // Act
            await sut.ExportJsonConfigAsync("secretPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        #endregion

        #region ExportConfigAsync Delegate Invocation Tests

        [Fact]
        public async Task ExportConfigAsync_ValidModel_ExecutesExportActionDelegate()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

            var dto = new ServiceDto { Name = "DelegateTestService" };
            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);

            _serviceConfigurationValidatorMock.Setup(d => d.ValidateAsync(
                It.IsAny<ServiceDto>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
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
            var task = (Task)TestReflection.InvokeNonPublic(sut, "ExportConfigAsync", new object?[]
            {
                "password",
                new Func<string?>(() => path),
                spyExportAction,
                "JSON",
                "Success",
                TestContext.Current.CancellationToken,
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

            _dialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string?>())).Returns(path);

            string? validationError = null;
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out validationError)).Returns(true);
            _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(expectedDto);

            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(expectedDto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
                await sut.ImportXmlConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

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

            _dialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string?>())).Returns(path);

            string? validationError = null;
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out validationError)).Returns(true);
            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(expectedDto);

            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(expectedDto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
                await sut.ImportJsonConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

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

        [Fact]
        public async Task ImportJsonConfig_UnauthorizedAccessException_DisplaysAdminRightsRequired()
        {
            // Arrange
            var sut = CreateSut();
            _dialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string?>())).Throws(new UnauthorizedAccessException("Access denied"));

            // Act
            await sut.ImportJsonConfigAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_AdminRightsRequired, UiAppConfig.Caption), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        #endregion

        #region OperationCanceledException Propagation Tests

        [Fact]
        public async Task InstallService_OperationCanceled_PropagatesInsteadOfMasking()
        {
            // Arrange
            var sut = CreateSut();
            var config = new ServiceConfiguration { Name = "CancelledInstallService" };
            var dto = new ServiceDto { Name = "CancelledInstallService" };

            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceManagerMock.Setup(m => m.IsServiceInstalled("CancelledInstallService", It.IsAny<CancellationToken>())).Returns(false);
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.InstallServiceAsync(config, TestContext.Current.CancellationToken));

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Never);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task UninstallService_OperationCanceled_PropagatesInsteadOfMasking()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "CancelledUninstallService";
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(serviceName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken));

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Never);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task ExecuteServiceCommand_StartService_OperationCanceled_PropagatesInsteadOfMasking()
        {
            // Arrange
            var sut = CreateSut();
            var serviceName = "CancelledStartService";
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(serviceName, It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(serviceName, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);
            _serviceManagerMock.Setup(m => m.StartServiceAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.StartServiceAsync(serviceName, TestContext.Current.CancellationToken));

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Never);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task OpenManager_OperationCanceled_PropagatesInsteadOfMasking()
        {
            // Arrange
            string baseDir = AppFoldersHelper.GetAppDirectory();
            string tempTrackingFile = Path.Combine(baseDir, $"{Guid.NewGuid():N}.exe");
            File.WriteAllText(tempTrackingFile, string.Empty);

            _appConfigMock.Setup(c => c.ManagerAppPublishPath).Returns(tempTrackingFile);
            _processHelperMock
                .Setup(h => h.Start(It.IsAny<ProcessStartInfo>()))
                .Throws(new OperationCanceledException());

            var sut = CreateSut();

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => sut.OpenManagerAsync(cancellationToken: TestContext.Current.CancellationToken));

                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_ManagerAppLaunchFailed, UiAppConfig.Caption), Times.Never);
            }
            finally
            {
                if (File.Exists(tempTrackingFile))
                {
                    try { File.Delete(tempTrackingFile); } catch { /* fail-silent */ }
                }
            }
        }

        [Fact]
        public async Task ExportConfig_OperationCanceled_PropagatesInsteadOfMasking()
        {
            // Arrange
            var sut = CreateSut();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            var dto = new ServiceDto { Name = "CancelledExport" };
            _modelToServiceDtoMock.Setup(m => m()).Returns(dto);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(dto, null, "password", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.ExportXmlConfigAsync("password", cancellationToken: TestContext.Current.CancellationToken));

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Never);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task ImportConfig_OperationCanceled_PropagatesInsteadOfMasking()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var sut = CreateSut();

                // Act & Assert
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => sut.ImportXmlConfigAsync(cancellationToken: cts.Token));

                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Resources.Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Never);
                _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
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
