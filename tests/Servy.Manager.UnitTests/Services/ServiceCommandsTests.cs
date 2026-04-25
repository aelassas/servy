using Moq;
using Newtonsoft.Json;
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.Validators;
using Servy.UI.Services;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.Services
{
    public class ServiceCommandsTests
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IServiceRepository> _serviceRepositoryMock;
        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IFileDialogService> _fileDialogServiceMock;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidatorMock;
        private readonly Mock<IXmlServiceValidator> _xmlServiceValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonServiceValidatorMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;

        private bool _refreshCalled;
        private TaskCompletionSource<bool> _refreshTcs;
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
            _appConfigMock = new Mock<IAppConfiguration>();

            _refreshTcs = new TaskCompletionSource<bool>();
            _removedServiceName = null;

            // Default safe returns for ServiceManager to prevent internal NullRefs
            _serviceManagerMock.Setup(m => m.StartServiceAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.StopServiceAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.RestartServiceAsync(It.IsAny<string>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.InstallServiceAsync(It.IsAny<InstallServiceOptions>())).ReturnsAsync(OperationResult.Success());
            _serviceManagerMock.Setup(m => m.UninstallServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());
        }

        private ServiceCommands CreateServiceCommands()
        {
            _refreshCalled = false;
            _removedServiceName = null;
            _refreshTcs = new TaskCompletionSource<bool>();

            return new ServiceCommands(
                _serviceManagerMock.Object, // Use the Mock!
                _serviceRepositoryMock.Object,
                _messageBoxServiceMock.Object,
                _fileDialogServiceMock.Object,
                name => _removedServiceName = name,
                () =>
                {
                    _refreshCalled = true;
                    _refreshTcs.TrySetResult(true);
                    return Task.CompletedTask;
                },
                _serviceConfigurationValidatorMock.Object,
                _xmlServiceValidatorMock.Object,
                _jsonServiceValidatorMock.Object,
                _appConfigMock.Object
            );
        }

        #region Import/Export & Config Tests

        [Fact]
        public async Task ImportJsonConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidJson()
        {
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "MyService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };
            var json = JsonConvert.SerializeObject(dto);

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, json);

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);
            _serviceConfigurationValidatorMock.Setup(v => v.Validate(It.IsAny<ServiceDto>())).ReturnsAsync(true);

            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .Callback(() => _refreshTcs.TrySetResult(true));

            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(true);

            await sut.ImportJsonConfigAsync();

            var delay = Task.Delay(2000);
            var completedTask = await Task.WhenAny(_refreshTcs.Task, delay);

            Assert.True(completedTask == _refreshTcs.Task, "Refresh task timed out! The refresh logic was not executed.");
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(_refreshCalled);

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

            await sut.ImportJsonConfigAsync();

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()));
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Never);

            File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportXmlConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidXml()
        {
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "XmlService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };

            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto));
            var tempFile = Path.GetTempFileName();
            using (var writer = new StreamWriter(tempFile))
            {
                serializer.Serialize(writer, dto);
            }

            _fileDialogServiceMock.Setup(d => d.OpenXml()).Returns(tempFile);

            string outErr = null;
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr)).Returns(true);
            _serviceConfigurationValidatorMock.Setup(v => v.Validate(It.IsAny<ServiceDto>())).ReturnsAsync(true);

            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .Callback(() => _refreshTcs.TrySetResult(true));

            await sut.ImportXmlConfigAsync();

            var delay = Task.Delay(2000);
            var completedTask = await Task.WhenAny(_refreshTcs.Task, delay);

            Assert.True(completedTask == _refreshTcs.Task, "Refresh task timed out! XML import did not trigger refresh.");
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Once);
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

            await sut.ImportXmlConfigAsync();

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Never);
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

            await sut.ConfigureServiceAsync(service);

            _appConfigMock.Verify(c => c.DesktopAppPublishPath, Times.AtLeastOnce);

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

            var result = await sut.StartServiceAsync(service, showMessageBox: false);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.StartServiceAsync(service.Name, It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task StopServiceAsync_ShouldCallServiceManager()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            var result = await sut.StopServiceAsync(service, showMessageBox: false);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.StopServiceAsync(service.Name, It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task RestartServiceAsync_ShouldCallServiceManager()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            _serviceManagerMock.Setup(m => m.GetServiceStartupType(service.Name, It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);

            var result = await sut.RestartServiceAsync(service, showMessageBox: false);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.RestartServiceAsync(service.Name), Times.Once);
        }

        [Fact]
        public async Task InstallServiceAsync_ShouldCallServiceManager()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            // CRITICAL FIX: Bypass the Directory.Exists check for DEBUG builds
            // by ensuring the expected directory actually exists in the test environment.
            var debugDir = Path.GetFullPath(Servy.Core.Config.AppConfig.ServyServiceManagerDebugFolder);
            try
            {
                if (!Directory.Exists(debugDir))
                {
                    Directory.CreateDirectory(debugDir);
                }
            }
            catch { /* Ignore creation errors if running in restricted environments */ }

            // 1. Bypass Service Exists check
            _serviceManagerMock.Setup(m => m.IsServiceInstalled(service.Name)).Returns(false);

            // 2. Provide Domain Object
            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name, ExecutablePath = "C:\\test.exe" });

            var result = await sut.InstallServiceAsync(service);

            Assert.True(result, "InstallServiceAsync returned false. The Directory.Exists validation likely failed.");
            _serviceManagerMock.Verify(m => m.InstallServiceAsync(It.Is<InstallServiceOptions>(o => o.ServiceName == service.Name)), Times.Once);
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

            var result = await sut.UninstallServiceAsync(service);

            Assert.True(result);
            _serviceManagerMock.Verify(m => m.UninstallServiceAsync(service.Name, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(service.Name, _removedServiceName); // Verifies the UI callback was invoked
        }

        [Fact]
        public async Task RemoveServiceAsync_ShouldCallRepositoryDelete()
        {
            var sut = CreateServiceCommands();
            var service = new Service { Name = "TestService" };

            _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            _serviceRepositoryMock.Setup(r => r.GetByNameAsync(service.Name, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = service.Name });

            _serviceRepositoryMock.Setup(r => r.DeleteAsync(service.Name, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var result = await sut.RemoveServiceAsync(service);

            Assert.True(result);
            _serviceRepositoryMock.Verify(r => r.DeleteAsync(service.Name, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(service.Name, _removedServiceName);
        }

        #endregion
    }
}