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

            try
            {
                // Clean up original .tmp file and write the payload to the authorized .json path
                if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
                File.WriteAllText(tempFile, json);

                _fileDialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string>())).Returns(tempFile);
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
            }
            finally
            {
                // Teardown
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportJsonConfigAsync_ShouldShowError_WhenJsonInvalid()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var rawTempFile = Path.GetTempFileName();

            // Mutate the extension to .json to safely pass the foundational ImportGuard path security check
            var tempJsonFile = Path.ChangeExtension(rawTempFile, ".json");

            try
            {
                File.WriteAllText(tempJsonFile, "{ invalid-json }");

                _fileDialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string>())).Returns(tempJsonFile);

                string outErr = "Invalid JSON";
                _jsonServiceValidatorMock
                    .Setup(v => v.TryValidate(It.IsAny<string>(), out outErr))
                    .Returns(false);

                // Act
                await sut.ImportJsonConfigAsync(CancellationToken.None);

                // Assert
                // Explicit verify constraint ensures that execution actually bypassed the path guard and hit the format validator
                _jsonServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Once);
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                // Clean up all local operating context artifacts cleanly
                if (File.Exists(rawTempFile)) File.Delete(rawTempFile);
                if (File.Exists(tempJsonFile)) File.Delete(tempJsonFile);
            }
        }

        [Fact]
        public async Task ImportXmlConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidXml()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "XmlService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };

            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto));

            // Change extension from .tmp to .xml to pass ValidatePathSecurity
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".xml");

            try
            {
                // Clean up original .tmp file and write the payload to the authorized .xml path
                if (File.Exists(baseTempFile)) File.Delete(baseTempFile);

                using (var writer = new StreamWriter(tempFile))
                {
                    serializer.Serialize(writer, dto);
                }

                _fileDialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string>())).Returns(tempFile);

                string outErr = null;
                _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
                _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

                _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);

                _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(dto);

                // Act
                await sut.ImportXmlConfigAsync(CancellationToken.None);

                // Assert
                _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
                _xmlServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Once);
                _xmlServiceSerializerMock.Verify(s => s.Deserialize(It.IsAny<string>()), Times.Once);
                Assert.True(_refreshCalled);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportXmlConfigAsync_ShouldShowError_WhenXmlInvalid()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var rawTempFile = Path.GetTempFileName();

            // Mutate the extension to .xml to safely pass the foundational ImportGuard path security check
            var tempXmlFile = Path.ChangeExtension(rawTempFile, ".xml");

            try
            {
                File.WriteAllText(tempXmlFile, "<invalid><xml>");

                _fileDialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string>())).Returns(tempXmlFile);

                string outErr = "Malformed XML";
                _xmlServiceValidatorMock
                    .Setup(v => v.TryValidate(It.IsAny<string>(), out outErr))
                    .Returns(false);

                // Act
                await sut.ImportXmlConfigAsync(CancellationToken.None);

                // Assert
                // Explicit verify constraint ensures that execution actually bypassed the path guard and hit the format validator
                _xmlServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Once);
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
                Assert.False(_refreshCalled);
            }
            finally
            {
                // Clean up all local operating context artifacts cleanly
                if (File.Exists(rawTempFile)) File.Delete(rawTempFile);
                if (File.Exists(tempXmlFile)) File.Delete(tempXmlFile);
            }
        }

        [Fact]
        public async Task ImportConfigAsync_FileDialogCancelled_ReturnsEarlySilently()
        {
            // Arrange
            var sut = CreateServiceCommands();
            _fileDialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string>())).Returns(string.Empty);

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

            try
            {
                if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
                File.WriteAllText(tempFile, "{}");

                _fileDialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string>())).Returns(tempFile);
                string outErr = null;
                _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
                _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null);

                // Act
                await sut.ImportJsonConfigAsync(CancellationToken.None);

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_FailedToLoadJson, UiAppConfig.Caption), Times.Once);
                _serviceConfigurationValidatorMock.Verify(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportConfigAsync_DomainValidationFails_AbortsImportCycle()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".json");

            try
            {
                if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
                File.WriteAllText(tempFile, "{}");

                _fileDialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string>())).Returns(tempFile);
                string outErr = null;
                _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
                _jsonServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(new ServiceDto { Name = "InvalidDomain" });
                _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

                // Act
                await sut.ImportJsonConfigAsync(CancellationToken.None);

                // Assert
                _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportConfigAsync_ServiceAlreadyExists_UserConfirms_UpsertsAndRefreshes()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "XmlService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };

            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto));

            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".xml");

            try
            {
                if (File.Exists(baseTempFile)) File.Delete(baseTempFile);

                using (var writer = new StreamWriter(tempFile))
                {
                    serializer.Serialize(writer, dto);
                }

                _fileDialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string>())).Returns(tempFile);

                string outErr = null;
                _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
                _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

                _serviceRepositoryMock.Setup(r => r.GetByNameAsync(dto.Name, false, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
                _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_ImportServiceConfirmation, UiAppConfig.Caption)).ReturnsAsync(true);
                _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

                _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);

                // Act
                await sut.ImportXmlConfigAsync(CancellationToken.None);

                // Assert
                _serviceRepositoryMock.Verify(r => r.GetByNameAsync(dto.Name, false, It.IsAny<CancellationToken>()), Times.Once);
                _xmlServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Once);
                _xmlServiceSerializerMock.Verify(s => s.Deserialize(It.IsAny<string>()), Times.Once);
                _messageBoxServiceMock.Verify(m => m.ShowConfirmAsync(Strings.Msg_ImportServiceConfirmation, UiAppConfig.Caption), Times.Once);
                _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
                Assert.True(_refreshCalled);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportConfigAsync_ServiceAlreadyExists_UserDeclines_DoesNotUpsert()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "XmlService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };

            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto));

            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".xml");

            try
            {
                if (File.Exists(baseTempFile)) File.Delete(baseTempFile);

                using (var writer = new StreamWriter(tempFile))
                {
                    serializer.Serialize(writer, dto);
                }

                _fileDialogServiceMock.Setup(d => d.OpenXml(It.IsAny<string>())).Returns(tempFile);

                string outErr = null;
                _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
                _serviceConfigurationValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

                _serviceRepositoryMock.Setup(r => r.GetByNameAsync(dto.Name, false, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
                _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(Strings.Msg_ImportServiceConfirmation, UiAppConfig.Caption)).ReturnsAsync(false);

                _xmlServiceSerializerMock.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);

                // Act
                await sut.ImportXmlConfigAsync(CancellationToken.None);

                // Assert
                _serviceRepositoryMock.Verify(r => r.GetByNameAsync(dto.Name, false, It.IsAny<CancellationToken>()), Times.Once);
                _xmlServiceValidatorMock.Verify(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Once);
                _xmlServiceSerializerMock.Verify(s => s.Deserialize(It.IsAny<string>()), Times.Once);
                _messageBoxServiceMock.Verify(m => m.ShowConfirmAsync(Strings.Msg_ImportServiceConfirmation, UiAppConfig.Caption), Times.Once);
                _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
                Assert.False(_refreshCalled);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportConfigAsync_UpsertReturnsZero_DisplaysPersistenceErrorMessage()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var baseTempFile = Path.GetTempFileName();
            var tempFile = Path.ChangeExtension(baseTempFile, ".json");

            try
            {
                if (File.Exists(baseTempFile)) File.Delete(baseTempFile);
                File.WriteAllText(tempFile, "{}");

                _fileDialogServiceMock.Setup(d => d.OpenJson(It.IsAny<string>())).Returns(tempFile);
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
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ConfigureServiceAsync_ShouldUseConfiguredPath()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // 1. Generate a unique base path template from the OS
            var baseTmpFile = Path.GetTempFileName();
            var tempExe = Path.ChangeExtension(baseTmpFile, ".exe");

            try
            {
                // 2. Delete initial empty temp file before writing mock executable payload
                if (File.Exists(baseTmpFile)) File.Delete(baseTmpFile);

                // 3. Write our mock executable artifact safely
                File.WriteAllText(tempExe, "dummy");

                _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(tempExe);

                // Mock Repository to return a valid domain entity
                _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ServiceDto { Name = service.Name });

                // Act
                await sut.ConfigureServiceAsync(service, CancellationToken.None);

                // Assert
                _appConfigMock.Verify(c => c.DesktopAppPublishPath, Times.AtLeastOnce);
            }
            finally
            {
                // 4. Clean up our active dummy testing file artifact safely
                if (File.Exists(tempExe))
                {
                    try { File.Delete(tempExe); } catch { /* fail-silent */ }
                }
            }
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

            try
            {
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
                Assert.True(capturedPsi.UseShellExecute);

                // 3. ARGUMENT VALIDATION: Ensure only the skip-splash argument is supplied, and no service name payload exists
                Assert.Contains($"\"{AppConfig.SkipSplashArgument}\"", capturedPsi.Arguments);
                Assert.DoesNotContain(AppConfig.ForceSoftwareRenderingArg, capturedPsi.Arguments); // Software rendering turned off in setup

                // Confirm the arguments consist solely of the splash skip payload (and optional whitespace) without any service parameters
                string expectedArgs = $"\"{AppConfig.SkipSplashArgument}\"";
                Assert.Equal(expectedArgs, capturedPsi.Arguments.Trim());
            }
            finally
            {
                // Delete temporary executable path on test completion
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

            try
            {
                File.WriteAllText(tempExe, "dummy");
                _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(tempExe);

                // Act
                await sut.ConfigureServiceAsync(new Service { Name = " " }, CancellationToken.None);

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_InvalidServiceName, UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe)) File.Delete(tempExe);
            }
        }

        [Fact]
        public async Task ConfigureServiceAsync_ServiceNotFoundInDb_ShowsNotFoundError()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var tempExe = Path.GetTempFileName() + ".exe";

            try
            {
                File.WriteAllText(tempExe, "dummy");
                _appConfigMock.Setup(c => c.DesktopAppPublishPath).Returns(tempExe);
                _serviceRepositoryMock.Setup(r => r.GetByNameAsync("Missing", true, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

                // Act
                await sut.ConfigureServiceAsync(new Service { Name = "Missing" }, CancellationToken.None);

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(Strings.Msg_ServiceNotFound, UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe)) File.Delete(tempExe);
            }
        }

        #endregion

        #region Lifecycle Methods Tests

        [Fact]
        public async Task StartServiceAsync_ShouldCallServiceManager()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // 1. Must mock repository so GetServiceDomain succeeds
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            // 2. Mock state to allow start
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Manual);

            // Act
            var result = await sut.StartServiceAsync(service, showMessageBox: false, cancellationToken: CancellationToken.None);

            // Assert
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
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            // Act
            var result = await sut.StopServiceAsync(service, showMessageBox: false, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result);
            _serviceManagerMock.Verify(m => m.StopServiceAsync(service.Name, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RestartServiceAsync_ShouldCallServiceManager()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);

            // Act
            var result = await sut.RestartServiceAsync(service, showMessageBox: false, cancellationToken: CancellationToken.None);

            // Assert
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

            string debugDir = null;
            bool directoryCreatedByTest = false;

#if DEBUG
            // Ensure the debug directory exists to satisfy the #if DEBUG validation check in InstallServiceAsync
            debugDir = Path.GetFullPath(AppConfig.ServyServiceManagerDebugFolder);

            try
            {
                if (!Directory.Exists(debugDir))
                {
                    Directory.CreateDirectory(debugDir);
                    directoryCreatedByTest = true;
                }
            }
            catch { /* Ignore creation errors if running in restricted environments */ }
#endif

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
            Assert.False(await sut.InstallServiceAsync(new Service { Name = "  " }, cancellationToken: CancellationToken.None));
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
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // 1. Auto-Confirm prompt
            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // 2. Provide Domain Object
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            // Act
            var result = await sut.UninstallServiceAsync(service, CancellationToken.None);

            // Assert
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
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            _serviceRepositoryMock.Setup(r => r.DeleteAsync(service.Name, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await sut.RemoveServiceAsync(service, CancellationToken.None);

            // Assert
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
            Assert.False(await sut.RemoveServiceAsync(new Service { Name = "\t" }, cancellationToken: CancellationToken.None));
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
            await sut.CopyPidAsync(service, cancellationToken: CancellationToken.None);

            // Assert
            _uiDispatcherMock.Verify(d => d.InvokeAsync(It.IsAny<Func<bool>>()), Times.Never);
        }

        [Fact]
        public async Task CopyPidAsync_SuccessfulClipboardAccess_ShowsMessage()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "HealthyService", Pid = 4321 };

            // Simulate immediate STA UI Thread Clipboard execution success
            _uiDispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Func<bool>>()))
                .ReturnsAsync(true);

            // Act
            await sut.CopyPidAsync(service, cancellationToken: CancellationToken.None);

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
            // Verifies that the internal retry loop honored Core.Config.AppConfig.ClipboardComMaxRetries
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
            await sut.CopyPidAsync(service, cancellationToken: CancellationToken.None);

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

            // Generate a guaranteed unique filename instead of a fixed name to avoid test collisions
            var uniqueBaseName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            var targetPath = Path.Combine(Path.GetTempPath(), $"{uniqueBaseName}_export_test.xml");
            var sampleDto = new ServiceDto { Name = service.Name, ExecutablePath = "test.exe" };

            _fileDialogServiceMock.Setup(f => f.SaveXml(Strings.SaveFileDialog_XmlTitle))
                .Returns(targetPath);

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sampleDto);

            try
            {
                // Act
                await sut.ExportServiceToXmlAsync(service, CancellationToken.None);

                // Assert
                _serviceRepositoryMock.Verify(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()), Times.Once);
                _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.ExportXml_Success, UiAppConfig.Caption), Times.Once);
            }
            finally
            {
                // Clean up the generated XML file if the exporter successfully wrote it to disk
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
            }
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

            // The service is absent from the database.
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

        [Fact]
        public async Task SearchServicesAsync_ContainsMalformedOrOrphanedRecords_FiltersOutNullMappedModels()
        {
            // Arrange
            var sut = CreateServiceCommands();

            // Populate DTO array: one valid service record and one malformed/orphaned record with an empty name
            var mockDtos = new List<ServiceDto>
            {
                new ServiceDto
                {
                    Name = "ValidServyService",
                    ExecutablePath = "C:\\Servy\\servy.exe"
                },
                new ServiceDto
                {
                    Name = string.Empty, // Triggers ToModelAsync to evaluate service.Name as empty and return null cleanly
                    ExecutablePath = "C:\\Servy\\malformed.exe"
                }
            };

            _serviceRepositoryMock.Setup(r => r.SearchAsync("Servy", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDtos);

            _appConfigMock.Setup(c => c.IsDesktopAppAvailable).Returns(true);

            // Act
            var result = await sut.SearchServicesAsync("Servy", calculatePerf: false, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Verifies the null model produced by the malformed/orphaned DTO was filtered out safely (Regression protection for #797)
            Assert.Single(result);
            Assert.Equal("ValidServyService", result[0].Name);

            _serviceRepositoryMock.Verify(r => r.SearchAsync("Servy", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SearchServicesAsync_CalculatePerfTrue_ForwardsParameterToMappingPipeline()
        {
            // Arrange
            var sut = CreateServiceCommands();

            // Provide a running service record containing an active system Process PID identifier
            var mockDtos = new List<ServiceDto>
            {
                new ServiceDto
                {
                    Name = "PerfMonitoredService",
                    ExecutablePath = "C:\\Servy\\servy.exe",
                    Pid = 4321
                }
            };

            _serviceRepositoryMock.Setup(r => r.SearchAsync("Perf", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDtos);

            _appConfigMock.Setup(c => c.IsDesktopAppAvailable).Returns(true);

            // Stub process helper tree metrics call for process ID 4321
            var expectedMetrics = new ProcessMetrics(12.5, 2048576);
            _processHelperMock.Setup(p => p.GetProcessTreeMetrics(4321)).Returns(expectedMetrics);

            // Act
            var result = await sut.SearchServicesAsync("Perf", calculatePerf: true, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(12.5, result[0].CpuUsage);

            // Verifies that the internal process helper was actively invoked during performance evaluations (calculatePerf parameter pinned)
            _processHelperMock.Verify(p => p.GetProcessTreeMetrics(4321), Times.Once);
            _serviceRepositoryMock.Verify(r => r.SearchAsync("Perf", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var service = new Service { Name = "LockingService" };

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });
            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Manual);

            // Run one command so ExecuteLockedAsync allocates a SemaphoreSlim in _serviceLocks.
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
