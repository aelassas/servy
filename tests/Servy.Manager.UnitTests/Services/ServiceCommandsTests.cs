using Moq;
using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Helpers;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
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
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidatorMock;

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
                _loggerMock.Object,
                _fileDialogServiceMock.Object,
                name => _removedServiceName = name,
                () =>
                {
                    _refreshCalled = true;
                    _refreshTcs.SetResult(true);
                    return Task.CompletedTask;
                },
                _serviceConfigurationValidatorMock.Object
            );
        }

        public ServiceCommandsTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
            _serviceRepositoryMock = new Mock<IServiceRepository>();
            _messageBoxServiceMock = new Mock<IMessageBoxService>();
            _fileDialogServiceMock = new Mock<IFileDialogService>();
            _loggerMock = new Mock<ILogger>();
            _serviceConfigurationValidatorMock = new Mock<IServiceConfigurationValidator>();
            _refreshTcs = new TaskCompletionSource<bool>();
            _removedServiceName = null;
        }

        [Fact]
        public async Task ImportJsonConfigAsync_ShouldCallRepositoryAndRefresh_WhenValidJson()
        {
            // Arrange
            var sut = CreateServiceCommands();
            var dto = new ServiceDto { Name = "MyService", ExecutablePath = @"C:\myApp.exe" };
            var json = JsonConvert.SerializeObject(dto);

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, json);

            _fileDialogServiceMock.Setup(d => d.OpenJson()).Returns(tempFile);
            _serviceConfigurationValidatorMock.Setup(v => v.Validate(It.IsAny<ServiceDto>())).ReturnsAsync(true);
            _serviceRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await sut.ImportJsonConfigAsync();
            await _refreshTcs.Task;

            // Assert
            _serviceRepositoryMock.Verify(r => r.UpsertAsync(It.Is<ServiceDto>(d => d.Name == "MyService"), It.IsAny<CancellationToken>()), Times.Once);
            _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.ImportJson_Success, AppConfig.Caption));
            Assert.True(_refreshCalled);

            File.Delete(tempFile);
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

       
    }
}
