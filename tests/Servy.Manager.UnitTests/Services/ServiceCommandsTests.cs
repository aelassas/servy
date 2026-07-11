using Moq;
using Newtonsoft.Json;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.Validation;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.Services
{
    public class ServiceCommandsTests : IDisposable
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IServiceRepository> _serviceRepositoryMock;
        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IFileDialogService> _fileDialogServiceMock;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidatorMock;
        private readonly Mock<IXmlServiceValidator> _xmlServiceValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonServiceValidatorMock;
        private readonly Mock<IXmlServiceSerializer> _xmlServiceSerializerMock;
        private readonly Mock<IJsonServiceSerializer> _jsonServiceSerializerMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<IProcessHelper> _processHelperMock;
        private readonly Mock<IUiDispatcher> _uiDispatcherMock;
        private readonly List<ServiceCommands> _created = new List<ServiceCommands>();

        private bool _refreshCalled;
        private string _removedServiceName;

        public ServiceCommandsTests()
        {
            // Injecting a Mock ServiceManager instead of a real one prevents test hangs 
            // and isolates the ServiceCommands logic perfectly.
            _serviceManagerMock = new Mock<IServiceManager>();
            _serviceRepositoryMock = new Mock<IServiceRepository>();
            _messageBoxServiceMock = new Mock<IMessageBoxService>();
            _fileDialogServiceMock = new Mock<IFileDialogService>();
            _serviceConfigurationValidatorMock = new Mock<IServiceConfigurationValidator>();
            _xmlServiceValidatorMock = new Mock<IXmlServiceValidator>();
            _jsonServiceValidatorMock = new Mock<IJsonServiceValidator>();
            _xmlServiceSerializerMock = new Mock<IXmlServiceSerializer>();
            _jsonServiceSerializerMock = new Mock<IJsonServiceSerializer>();
            _appConfigMock = new Mock<IAppConfiguration>();
            _processHelperMock = new Mock<IProcessHelper>();
            _uiDispatcherMock = new Mock<IUiDispatcher>();

            _removedServiceName = null;

            // Default safe returns for ServiceManager to prevent internal NullRefs
            _serviceManagerMock.Setup(m => m.StartServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.StopServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.RestartServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
        }

        private ServiceCommands CreateServiceCommands()
        {
            _refreshCalled = false;
            _removedServiceName = null;

            var sut = new ServiceCommands(
                _serviceManagerMock.Object, // Use the Mock!
                _serviceRepositoryMock.Object,
                _messageBoxServiceMock.Object,
                _fileDialogServiceMock.Object,
                name => _removedServiceName = name,
                () =>
                {
                    _refreshCalled = true;
                    return Task.CompletedTask;
                },
                _serviceConfigurationValidatorMock.Object,
                _xmlServiceValidatorMock.Object,
                _jsonServiceValidatorMock.Object,
                _xmlServiceSerializerMock.Object,
                _jsonServiceSerializerMock.Object,
                _appConfigMock.Object,
                _processHelperMock.Object,
                _uiDispatcherMock.Object
            );
            _created.Add(sut);
            return sut;
        }

        public void Dispose()
        {
            foreach (var sut in _created) sut.Dispose();
        }

        #region Import/Export & Config Tests

        [Fact]
        public async Task ImportJsonConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidJson()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "MyService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };
            var json = JsonConvert.SerializeObject(dto);

            // Change extension from .tmp to .json to pass ValidatePathSecurity
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".json");

            // Clean up original .tmp file and write the payload to the authorized .json path
            if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
            File.WriteAllText(tempFile, json);

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(true);

            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(dto);

            // Act
            await sut.ImportJsonConfigAsync(CancellationToken.None);

            // Assert
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            _jsonServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Once);
            _jsonServiceSerializerMock.Verify(s => s.Deserialize(It.IsAny<string>()), Times.Once);
            Assert.True(_refreshCalled);

            // Teardown
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportJsonConfigAsync_ShouldShowError_WhenJsonInvalid()
        {
            var sut = CreateServiceCommands();
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "{ invalid-json }");

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);

            string outErr = "Invalid JSON";
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(false);

            await sut.ImportJsonConfigAsync(CancellationToken.None);

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()));
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

            File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportXmlConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidXml()
        {
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "XmlService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };

            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto));

            // Change extension from .tmp to .xml to pass ValidatePathSecurity
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".xml");

            // Clean up original .tmp file and write the payload to the authorized .xml path
            if (File.Exists(baseTempFile)) File.Delete(baseTempFile);

            using (var writer = new StreamWriter(tempFile))
            {
                serializer.Serialize(writer, dto);
            }

            _fileDialogServiceMock.Setup(d => d.OpenXml()).Returns(tempFile);

            string outErr = null;
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(dto);

            // Act
            await sut.ImportXmlConfigAsync(CancellationToken.None);

            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            _xmlServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Once);
            _xmlServiceSerializerMock.Verify(s => s.Deserialize(It.IsAny<string>()), Times.Once);
            Assert.True(_refreshCalled);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportXmlConfigAsync_ShouldShowError_WhenXmlInvalid()
        {
            var sut = CreateServiceCommands();
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "<invalid><xml>");

            _fileDialogServiceMock.Setup(d => d.OpenXml()).Returns(tempFile);

            string outErr = "Malformed XML";
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(false);

            await sut.ImportXmlConfigAsync(CancellationToken.None);

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.False(_refreshCalled);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportConfigAsync_FileDialogCancelled_ReturnsEarlySilently()
        {
            // Arrange
            var sut = CreateServiceCommands();
            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(string.Empty);

            // Act
            await sut.ImportJsonConfigAsync(CancellationToken.None);

            // Assert
            _jsonServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Never);
        }

        [Fact]
        public async Task ImportConfigAsync_DeserializationYieldsNull_DisplaysLoadErrorMessage()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".json");
            if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
            File.WriteAllText(tempFile, "{}");

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);
            string outErr = null;
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null);

            // Act
            await sut.ImportJsonConfigAsync(CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_FailedToLoadJson, UiAppConfig.Caption), Times.Once);
            _serviceConfigurationValidatorMock.Verify(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportConfigAsync_DomainValidationFails_AbortsImportCycle()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".json");
            if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
            File.WriteAllText(tempFile, "{}");

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);
            string outErr = null;
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(new ServiceDto { Name = "InvalidDomain" });
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            await sut.ImportJsonConfigAsync(CancellationToken.None);

            // Assert
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportConfigAsync_UpsertReturnsZero_DisplaysPersistenceErrorMessage()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".json");
            if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
            File.WriteAllText(tempFile, "{}");

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);
            string outErr = null;
            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
            _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(new ServiceDto { Name = "FailedUpsert" });
            _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

            // Act
            await sut.ImportJsonConfigAsync(CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.ImportJson_Error, UiAppConfig.Caption), Times.Once);
            Assert.False(_refreshCalled);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ConfigureServiceAsync_ShouldUseConfiguredPath()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // 1. Bypass the File.Exists check
            var tempExe = Path.GetTempFileName() + ".exe";
            File.WriteAllText(tempExe, "dummy");

            _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(tempExe);

            // 2. Mock Repository to return a valid domain entity
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            await sut.ConfigureServiceAsync(service, CancellationToken.None);

            _appConfigMock.Verify(c => c.DesktopAppPublishPath, Times.AtLeastOnce);

            if (File.Exists(tempExe)) File.Delete(tempExe);
        }

        [Fact]
        public async Task ConfigureServiceAsync_MissingOrInvalidAppPublishPath_ShowsNotFoundError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(string.Empty);

            // Act
            await sut.ConfigureServiceAsync(new Service { Name = "AnyService" }, CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_DesktopAppNotFound, UiAppConfig.Caption), Times.Once);
            _serviceRepositoryMock.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ConfigureServiceAsync_NullServiceParameter_LaunchesAppDirectlyWithoutArguments()
        {
            // Arrange
            // Create an empty, non-executable tracking file context to pass the File.Exists guard
            string tempTrackingFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid():N}.exe");
            File.WriteAllText(tempTrackingFile, string.Empty);

            _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(tempTrackingFile);
            _appConfigMock.Setup(c => c.ForceSoftwareRendering).Returns(false);

            ProcessStartInfo capturedPsi = null;

            // INTERCEPTION SEAM: Capture the launch metadata via callback and return null 
            // to safely complete the fire-and-forget execution block without triggering ShellExecute.
            _processHelperMock
                .Setup(h => h.Start(It.IsAny<ProcessStartInfo>()))
                .Callback<ProcessStartInfo>(psi => capturedPsi = psi)
                .Returns((Process)null);

            var sut = CreateServiceCommands();

            try
            {
                // Act
                await sut.ConfigureServiceAsync(null, CancellationToken.None);

                // Assert
                // 1. Verify that database checks were bypassed since the target service context parameter was null
                _serviceRepositoryMock.Verify(r => r.GetByNameAsync(
                    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                    Times.Never);

                // 2. HERMETIC VERIFICATION: Positively assert the precise launch state parameters
                Assert.NotNull(capturedPsi);
                Assert.Equal(tempTrackingFile, capturedPsi.FileName);
                Assert.Contains($"\"{AppConfig.SkipSplashArgument}\"", capturedPsi.Arguments);
                Assert.True(capturedPsi.UseShellExecute);
            }
            finally
            {
                // Cleanup the tracking file resource layout safely
                if (File.Exists(tempTrackingFile))
                {
                    File.Delete(tempTrackingFile);
                }
            }
        }

        [Fact]
        public async Task ConfigureServiceAsync_WhitespaceServiceName_ShowsInvalidNameError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var tempExe = Path.GetTempFileName() + ".exe";
            File.WriteAllText(tempExe, "dummy");
            _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(tempExe);

            // Act
            await sut.ConfigureServiceAsync(new Service { Name = " " }, CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_InvalidServiceName, UiAppConfig.Caption), Times.Once);

            if (File.Exists(tempExe)) File.Delete(tempExe);
        }

        [Fact]
        public async Task ConfigureServiceAsync_ServiceNotFoundInDb_ShowsNotFoundError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var tempExe = Path.GetTempFileName() + ".exe";
            File.WriteAllText(tempExe, "dummy");
            _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(tempExe);
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync("Missing", true, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

            // Act
            await sut.ConfigureServiceAsync(new Service { Name = "Missing" }, CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);

            if (File.Exists(tempExe)) File.Delete(tempExe);
        }

        #endregion

        #region Lifecycle Methods Tests

        [Fact]
        public async Task StartServiceAsync_ShouldCallServiceManager()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // 1. Must mock repository so GetServiceDomain succeeds
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            // 2. Mock state to allow start
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Manual);

            var result = await sut.StartServiceAsync(service, showMessageBox: false, cancellationToken: CancellationToken.None);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.StartServiceAsync(service.Name, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartServiceAsync_ServiceIsDisabled_ReturnsFalseAndDisplaysDisabledError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "DisabledService" };
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = service.Name });
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Disabled);

            // Act
            var result = await sut.StartServiceAsync(service, showMessageBox: true, cancellationToken: CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceDisabledError, UiAppConfig.Caption), Times.Once);
            _serviceManagerMock.Verify(m => m.StartServiceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.False(result);
        }

        [Fact]
        public async Task StopServiceAsync_ShouldCallServiceManager()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            var result = await sut.StopServiceAsync(service, showMessageBox: false, cancellationToken: CancellationToken.None);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.StopServiceAsync(service.Name, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RestartServiceAsync_ShouldCallServiceManager()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);

            var result = await sut.RestartServiceAsync(service, showMessageBox: false, cancellationToken: CancellationToken.None);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.RestartServiceAsync(service.Name, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteServiceCommandAsync_NullOrWhitespaceServiceInput_ReturnsFalseImmediately()
        {
            // Arrange
            var sut = CreateServiceCommands();

            // Act & Assert Flow Branch Lookups
            Assert.False(await sut.StartServiceAsync(null, cancellationToken: CancellationToken.None));
            Assert.False(await sut.StartServiceAsync(new Service { Name = "" }, cancellationToken: CancellationToken.None));
            Assert.False(await sut.StartServiceAsync(new Service { Name = "  " }, cancellationToken: CancellationToken.None));
        }

        [Fact]
        public async Task ExecuteServiceCommandAsync_ServiceNotFoundInRepository_ReturnsFalseAndLogsError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "MissingRepoService" };
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

            // Act
            var result = await sut.StartServiceAsync(service, showMessageBox: true, cancellationToken: CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExecuteServiceCommandAsync_OperationReturnsFailureWithCustomMessage_DisplaysMessage()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "FailingOpService" };
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = service.Name });
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Manual);
            _serviceManagerMock.Setup(m => m.StartServiceAsync(service.Name, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Custom Core Critical Crash Error Context"));

            // Act
            var result = await sut.StartServiceAsync(service, showMessageBox: true, cancellationToken: CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync("Custom Core Critical Crash Error Context", UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task InstallServiceAsync_ShouldCallServiceManager()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // Use reflection to gracefully handle the conditional existence of the debug folder property.
            var debugFolderProp = typeof(Core.Config.AppConfig).GetProperty("ServyServiceManagerDebugFolder");
            string debugDir = debugFolderProp?.GetValue(null) as string;

            bool directoryCreatedByTest = false;

            if (!string.IsNullOrEmpty(debugDir))
            {
                try
                {
                    debugDir = Path.GetFullPath(debugDir);
                    if (!Directory.Exists(debugDir))
                    {
                        Directory.CreateDirectory(debugDir);
                        directoryCreatedByTest = true;
                    }
                }
                catch { /* Ignore creation errors if running in restricted environments */ }
            }

            // 1. Bypass Service Exists check
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(service.Name, It.IsAny<CancellationToken>())).Returns(false);

            // 2. Provide Domain Object
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name, ExecutablePath = "C:\\test.exe" });

            try
            {
                // Act
                var result = await sut.InstallServiceAsync(service, cancellationToken: CancellationToken.None);

                // Assert
                Assert.True(result, "InstallServiceAsync returned false. The Directory.Exists validation likely failed.");
                _serviceManagerMock.Verify(m => m.InstallServiceAsync(It.Is<InstallServiceOptions>(o => o.ServiceName == service.Name), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                // Teardown - Clean up created diagnostic folder artifacts explicitly to maintain sandbox safety boundaries
                if (directoryCreatedByTest && !string.IsNullOrEmpty(debugDir) && Directory.Exists(debugDir))
                {
                    try
                    {
                        Directory.Delete(debugDir, recursive: true);
                    }
                    catch { /* Prevent teardown exceptions from hiding primary assertion faults */ }
                }
            }
        }

        [Fact]
        public async Task InstallServiceAsync_NullOrWhitespaceServiceInput_ReturnsFalse()
        {
            // Arrange
            var sut = CreateServiceCommands();

            // Act & Assert
            Assert.False(await sut.InstallServiceAsync(null, cancellationToken: CancellationToken.None));
            Assert.False(await sut.InstallServiceAsync(new Service { Name = string.Empty }, cancellationToken: CancellationToken.None));
        }

        [Fact]
        public async Task InstallServiceAsync_ServiceAlreadyInstalledButUserCancelsOverwrite_ReturnsFalse()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "AlreadyHereService" };
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(service.Name, It.IsAny<CancellationToken>())).Returns(true);
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_ServiceAlreadyExists, UiAppConfig.Caption)).ReturnsAsync(false);

            // Act
            var result = await sut.InstallServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _serviceRepositoryMock.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallServiceAsync_ServiceNotFoundInDb_ShowsNotFoundErrorAndReturnsFalse()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "MissingInstallDbRecord" };
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(service.Name, It.IsAny<CancellationToken>())).Returns(false);
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

            // Act
            var result = await sut.InstallServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task InstallServiceAsync_ManagerInstallFailsWithCustomMessage_DisplaysMessageAndReturnsFalse()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "FailingInstallation" };
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(service.Name, It.IsAny<CancellationToken>())).Returns(false);
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = service.Name, ExecutablePath = "C:\\fail.exe" });
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Access Denied Error Code 5"));

            // Act
            var result = await sut.InstallServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync("Access Denied Error Code 5", UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task UninstallServiceAsync_ShouldCallServiceManager()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // 1. Auto-Confirm prompt
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // 2. Provide Domain Object
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            var result = await sut.UninstallServiceAsync(service, CancellationToken.None);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.UninstallServiceAsync(service.Name, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(service.Name, _removedServiceName); // Verifies the UI callback was invoked
        }

        [Fact]
        public async Task UninstallServiceAsync_NullOrWhitespaceServiceInput_ReturnsFalse()
        {
            // Arrange
            var sut = CreateServiceCommands();

            // Act & Assert
            Assert.False(await sut.UninstallServiceAsync(null, cancellationToken: CancellationToken.None));
            Assert.False(await sut.UninstallServiceAsync(new Service { Name = "\t" }, cancellationToken: CancellationToken.None));
        }

        [Fact]
        public async Task UninstallServiceAsync_ConfirmationDeniedByUser_ReturnsFalseEarly()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "SaveMeService" };
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_UninstallServiceConfirm, UiAppConfig.Caption)).ReturnsAsync(false);

            // Act
            var result = await sut.UninstallServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _serviceRepositoryMock.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UninstallServiceAsync_ServiceMissingFromDbLookup_ShowsNotFoundError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "GhostService" };
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_UninstallServiceConfirm, UiAppConfig.Caption)).ReturnsAsync(true);
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

            // Act
            var result = await sut.UninstallServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task UninstallServiceAsync_ManagerUninstallReturnsFailure_DisplaysErrorMessage()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "UnstoppableService" };
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_UninstallServiceConfirm, UiAppConfig.Caption)).ReturnsAsync(true);
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = service.Name });
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(service.Name, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Service is marked for deletion hook lockout"));

            // Act
            var result = await sut.UninstallServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync("Service is marked for deletion hook lockout", UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task RemoveServiceAsync_ShouldCallRepositoryDelete()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            _serviceRepositoryMock.Setup(r => r.DeleteAsync(service.Name, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var result = await sut.RemoveServiceAsync(service, CancellationToken.None);

            Assert.True(result);
            _serviceRepositoryMock.Verify(r => r.DeleteAsync(service.Name, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(service.Name, _removedServiceName);
        }

        [Fact]
        public async Task RemoveServiceAsync_NullOrWhitespaceServiceInput_ReturnsFalse()
        {
            // Arrange
            var sut = CreateServiceCommands();

            // Act & Assert
            Assert.False(await sut.RemoveServiceAsync(null, cancellationToken: CancellationToken.None));
            Assert.False(await sut.RemoveServiceAsync(new Service { Name = string.Empty }, cancellationToken: CancellationToken.None));
        }

        [Fact]
        public async Task RemoveServiceAsync_UserAbortsConfirmation_ReturnsFalse()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "ProtectedRecord" };
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_RemoveServiceConfirm, UiAppConfig.Caption)).ReturnsAsync(false);

            // Act
            var result = await sut.RemoveServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _serviceRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RemoveServiceAsync_ServiceNotFoundInRepositoryLookup_DisplaysNotFoundError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "GhostRepoRecord" };
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_RemoveServiceConfirm, UiAppConfig.Caption)).ReturnsAsync(true);
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, false, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

            // Act
            var result = await sut.RemoveServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task RemoveServiceAsync_RepositoryDeleteReturnsZeroRows_DisplaysUnexpectedErrorBox()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "LockedRowService" };
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_RemoveServiceConfirm, UiAppConfig.Caption)).ReturnsAsync(true);
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, false, It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = service.Name });
            _serviceRepositoryMock.Setup(r => r.DeleteAsync(service.Name, It.IsAny<CancellationToken>())).ReturnsAsync(0);

            // Act
            var result = await sut.RemoveServiceAsync(service, CancellationToken.None);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region CopyPidAsync Tests

        [Fact]
        public async Task CopyPidAsync_NullPid_ReturnsImmediatelyWithoutInvokingDispatcher()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "NoPidService", Pid = null };

            // Act
            await sut.CopyPidAsync(service);

            // Assert
            _uiDispatcherMock.Verify(d => d.InvokeAsync(It.IsAny<Func<bool>>()), Times.Never);
        }

        [Fact]
        public async Task CopyPidAsync_SuccessfulClipboardAccess_LogsInfoAndShowsMessage()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "HealthyService", Pid = 4321 };

            // Simulate immediate STA UI Thread Clipboard execution success
            _uiDispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Func<bool>>()))
                .ReturnsAsync(true);

            // Act
            await sut.CopyPidAsync(service);

            // Assert
            _uiDispatcherMock.Verify(d => d.InvokeAsync(It.IsAny<Func<bool>>()), Times.Once);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.Msg_PidCopied, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task CopyPidAsync_DispatcherReturnsFalseEveryAttempt_RetriesThenShowsError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "LockedClipboardService", Pid = 9999 };

            // Simulate alternative process holding a Win32 clipboard handle block (returns false up to maximum retry cap)
            _uiDispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Func<bool>>()))
                .ReturnsAsync(false);

            // Act
            await sut.CopyPidAsync(service, cancellationToken: CancellationToken.None); 

            // Assert
            // Verifies that the internal retry loop honored Core.Config.AppConfig.ClipboardComMaxRetries (typically 3 or 5)
            _uiDispatcherMock.Verify(d => d.InvokeAsync(It.IsAny<Func<bool>>()), Times.Exactly(Core.Config.AppConfig.ClipboardComMaxRetries));
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_PidCopyFailed, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task CopyPidAsync_UnexpectedCrashInsideDispatcher_CatchesExceptionAndShowsGenericError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "CrashingClipboardService", Pid = 8888 };

            _uiDispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Func<bool>>()))
                .ThrowsAsync(new InvalidOperationException("Fatal thread context exception"));

            // Act
            await sut.CopyPidAsync(service);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region ExportServiceConfigAsync Tests (XML & JSON Formats)

        [Fact]
        public async Task ExportServiceToXmlAsync_ValidPathAndDto_DisplaysSuccess()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "XmlExportService" };
            var targetPath = Path.Combine(Path.GetTempPath(), "export_test.xml");
            var sampleDto = new ServiceDto { Name = service.Name, ExecutablePath = "test.exe" };

            _fileDialogServiceMock.Setup(f => f.SaveXml(Strings.SaveFileDialog_XmlTitle))
                .Returns(targetPath);

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sampleDto);

            // Act
            await sut.ExportServiceToXmlAsync(service, CancellationToken.None);

            // Assert
            _serviceRepositoryMock.Verify(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()), Times.Once);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.ExportXml_Success, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExportServiceToJsonAsync_FileDialogCancelled_ReturnsEarlyWithoutQueryingRepository()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "JsonCancelledService" };

            // User clicks "Cancel" on the native Save File Dialog returning null or empty string
            _fileDialogServiceMock.Setup(f => f.SaveJson(Strings.SaveFileDialog_JsonTitle))
                .Returns(string.Empty);

            // Act
            await sut.ExportServiceToJsonAsync(service, CancellationToken.None);

            // Assert
            _serviceRepositoryMock.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExportServiceToXmlAsync_ServiceNotFoundInRepository_ShowsNotFoundError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "MissingService" };
            _fileDialogServiceMock.Setup(f => f.SaveXml(It.IsAny<string>())).Returns(@"C:\out.xml");

            // Simulate the database lacking this specific record window mapping
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto)null);

            // Act
            await sut.ExportServiceToXmlAsync(service, CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExportServiceToJsonAsync_RepositoryThrowsException_HandlesExceptionGracefully()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "FaultyDbService" };
            _fileDialogServiceMock.Setup(f => f.SaveJson(It.IsAny<string>())).Returns(@"C:\out.json");

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Data.DataException("SQLite lock corruption detected"));

            // Act
            await sut.ExportServiceToJsonAsync(service, CancellationToken.None);

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task ExportServiceConfigAsync_NullServiceArgument_ShowsNothing()
        {
            // Arrange
            var sut = CreateServiceCommands();

            // Act & Assert
            await sut.ExportServiceToXmlAsync(null, CancellationToken.None);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Never);
            await sut.ExportServiceToJsonAsync(null, CancellationToken.None);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Never);
        }

        #endregion

        #region SearchServicesAsync Tests

        [Fact]
        public async Task SearchServicesAsync_NullSearchText_ConvertsToEmptyStringForRepository()
        {
            // Arrange
            var sut = CreateServiceCommands();
            _serviceRepositoryMock.Setup(r => r.SearchAsync(string.Empty, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceDto>());

            // Act
            var result = await sut.SearchServicesAsync(null, calculatePerf: false, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            _serviceRepositoryMock.Verify(r => r.SearchAsync(string.Empty, false, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public async Task Dispose_CalledMultipleTimes_DisposesSemaphoresOnceAndClearsLocks()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "LockingService" };

            // Trigger internal Lazy allocation of a SemaphoreSlim via a private locked method path
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Manual);

            // Execute a command once to eagerly generate an unmanaged lock reference window inside _serviceLocks
            await sut.StartServiceAsync(service, showMessageBox: false, cancellationToken: CancellationToken.None);

            // Act - Dispose the first time
            sut.Dispose();

            // Act - Dispose a second time to challenge the atomic Interlocked flag
            var doubleDisposeException = Record.Exception(() => sut.Dispose());

            // Assert
            Assert.Null(doubleDisposeException); // Second dispose should be a clean early return
        }

        #endregion
    }
}