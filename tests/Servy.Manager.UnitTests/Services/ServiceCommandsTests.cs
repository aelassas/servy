using Moq;
using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Services;
using Servy.Manager.Validators;
using Servy.UI.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.Services
{
    public class ServiceCommandsTests
    {
        private readonly Mock<IServiceRepository> _serviceRepositoryMock;
        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IFileDialogService> _fileDialogServiceMock;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidatorMock;
        private readonly Mock<IXmlServiceValidator> _xmlServiceValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonServiceValidatorMock;

        private bool _refreshCalled;
        private TaskCompletionSource<bool> _refreshTcs;
        private string _removedServiceName;

        private ServiceCommands CreateServiceCommands()
        {
            _refreshCalled = false;
            _removedServiceName = null;

            var mockControllerFactory = new Mock<Func<string, IServiceControllerWrapper>>();
            var mockServiceControllerProvider = new Mock<IServiceControllerProvider>();
            var mockWindowsApi = new Mock<IWindowsServiceApi>();
            var mockErrorProvider = new Mock<IWin32ErrorProvider>();
            var mockRepository = new Mock<IServiceRepository>();

            var realServiceManager = new ServiceManager(
                mockControllerFactory.Object,
                mockServiceControllerProvider.Object,
                mockWindowsApi.Object,
                mockErrorProvider.Object,
                mockRepository.Object
            );

            _refreshTcs = new TaskCompletionSource<bool>();

            return new ServiceCommands(
                realServiceManager,
                _serviceRepositoryMock.Object,
                _messageBoxServiceMock.Object,
                _fileDialogServiceMock.Object,
                name => _removedServiceName = name,
                () =>
                {
                    _refreshCalled = true;
                    _refreshTcs.SetResult(true);
                    return Task.CompletedTask;
                },
                _serviceConfigurationValidatorMock.Object,
                _xmlServiceValidatorMock.Object,
                _jsonServiceValidatorMock.Object
            );
        }

        public ServiceCommandsTests()
        {
            _serviceRepositoryMock = new Mock<IServiceRepository>();
            _messageBoxServiceMock = new Mock<IMessageBoxService>();
            _fileDialogServiceMock = new Mock<IFileDialogService>();
            _serviceConfigurationValidatorMock = new Mock<IServiceConfigurationValidator>();
            _refreshTcs = new TaskCompletionSource<bool>();
            _removedServiceName = null;
            _xmlServiceValidatorMock = new Mock<IXmlServiceValidator>();
            _jsonServiceValidatorMock = new Mock<IJsonServiceValidator>();
        }

        [Fact]
        public async Task ImportJsonConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidJson()
        {
            // 1. Arrange
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "MyService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };
            var json = JsonConvert.SerializeObject(dto);

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, json);

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);
            _serviceConfigurationValidatorMock.Setup(v => v.Validate(It.IsAny<ServiceDto>())).ReturnsAsync(true);

            // Ensure the mock triggers the completion signal
            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .Callback(() => _refreshTcs.TrySetResult(true));

            _jsonServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(true);

            // 2. Act
            await sut.ImportJsonConfigAsync();

            // 3. Assert (Wait with Timeout)
            var delay = Task.Delay(2000);
            var completedTask = await Task.WhenAny(_refreshTcs.Task, delay);

            // If we hit the delay, the refresh was never triggered
            Assert.True(completedTask == _refreshTcs.Task, "Refresh task timed out! The refresh logic was not executed.");

            // 4. Verification
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(_refreshCalled);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportJsonConfigAsync_ShouldShowError_WhenJsonInvalid()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "{ invalid-json }");

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);

            // Act
            await sut.ImportJsonConfigAsync();

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), AppConfig.Caption));
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Never);

            File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportXmlConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidXml()
        {
            // 1. Arrange
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "XmlService", ExecutablePath = @"C:\Windows\System32\notepad.exe" };

            // Serialize to a real XML string for the file
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto));
            var tempFile = Path.GetTempFileName();
            using (var writer = new StreamWriter(tempFile))
            {
                serializer.Serialize(writer, dto);
            }

            var xmlContent = File.ReadAllText(tempFile);

            _fileDialogServiceMock.Setup(d => d.OpenXml()).Returns(tempFile);

            // Mock Validator Interface
            string outErr = null;
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr))
                .Returns(true);

            _serviceConfigurationValidatorMock.Setup(v => v.Validate(It.IsAny<ServiceDto>())).ReturnsAsync(true);

            // Setup repository to trigger the refresh signal
            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .Callback(() => _refreshTcs.TrySetResult(true));

            // 2. Act
            await sut.ImportXmlConfigAsync();

            // 3. Assert (Wait with Timeout for the background refresh)
            var delay = Task.Delay(2000);
            var completedTask = await Task.WhenAny(_refreshTcs.Task, delay);

            Assert.True(completedTask == _refreshTcs.Task, "Refresh task timed out! XML import did not trigger refresh.");

            // 4. Verification
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Once);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), AppConfig.Caption), Times.Once);
            Assert.True(_refreshCalled);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task ImportXmlConfigAsync_ShouldShowError_WhenXmlInvalid()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "<invalid><xml>");

            _fileDialogServiceMock.Setup(d => d.OpenXml()).Returns(tempFile);

            // Mock validator to fail
            string outErr = "Malformed XML";
            _xmlServiceValidatorMock.Setup(v => v.TryValidate(It.IsAny<string>(), out outErr))
                .Returns(false);

            // Act
            await sut.ImportXmlConfigAsync();

            // Assert
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), AppConfig.Caption), Times.Once);
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.False(_refreshCalled);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

    }
}
