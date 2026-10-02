using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.NamedPipes;
using Servy.Core.Native;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Covers what <see cref="ServiceManager"/> does for the Servy host service: every installed service depends on it,
    /// its name is reserved, and the account of an installed service is granted access to the host's named pipe (the
    /// host rebuilds the pipe's DACL from the services in <c>Servy.db</c>), which is taken back on uninstall and remove.
    /// </summary>
    [Collection(LoggerCollection.Name)]
    public class ServiceManagerHostTests : IDisposable
    {
        private const string ServiceName = "PipedService";

        private readonly FakeServiceHandles _handles = new FakeServiceHandles();
        private readonly Mock<IWindowsServiceApi> _windowsServiceApi = new Mock<IWindowsServiceApi>();
        private readonly Mock<IWin32ErrorProvider> _win32ErrorProvider = new Mock<IWin32ErrorProvider>();
        private readonly Mock<IServiceRepository> _serviceRepository = new Mock<IServiceRepository>();
        private readonly Mock<IServyExePermissionsHardener> _hardener = new Mock<IServyExePermissionsHardener>();
        private readonly Mock<INamedPipesService> _pipes = new Mock<INamedPipesService>();

        public ServiceManagerHostTests()
        {
            _hardener.Setup(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _pipes.Setup(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _serviceRepository.Setup(x => x.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto)null);
            _serviceRepository.Setup(x => x.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _windowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(_handles.Scm(123));
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny)).Returns(true);
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>())).Returns(true);
        }

        public void Dispose() => _handles.Dispose();

        private ServiceManager CreateManager(INamedPipesService pipes, ServiceControllerStatus status = ServiceControllerStatus.Stopped)
        {
            var controller = new Mock<IServiceControllerWrapper>();
            controller.Setup(c => c.Status).Returns(status);
            return new ServiceManager(
                _ => controller.Object,
                new Mock<IServiceControllerProvider>().Object,
                _windowsServiceApi.Object,
                _win32ErrorProvider.Object,
                _serviceRepository.Object,
                _hardener.Object,
                pipes);
        }

        private SafeServiceHandle ArrangeServiceCreated()
        {
            var serviceHandle = _handles.Service(456);
            _windowsServiceApi.Setup(x => x.CreateService(
                    It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                    It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(serviceHandle);
            return serviceHandle;
        }

        private void ArrangeServiceAlreadyExists()
        {
            _windowsServiceApi.Setup(x => x.CreateService(
                    It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                    It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_handles.Service(0));
            _windowsServiceApi.Setup(x => x.GetServices())
                .Returns(new List<WindowsServiceInfo> { new WindowsServiceInfo { ServiceName = ServiceName } });
            _windowsServiceApi.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), ServiceName, It.IsAny<uint>()))
                .Returns(() => _handles.Service(789));
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig(
                    It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);
        }

        private SafeServiceHandle ArrangeUninstall(string account)
        {
            var serviceHandle = _handles.Service(456);
            _windowsServiceApi.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), ServiceName, It.IsAny<uint>())).Returns(serviceHandle);
            _windowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_STATUS>.IsAny)).Returns(false);
            _win32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(Errors.ERROR_SERVICE_NOT_ACTIVE);
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig(
                    serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), null, null, IntPtr.Zero, null, null, null, null))
                .Returns(true);
            _windowsServiceApi.Setup(x => x.DeleteService(serviceHandle)).Returns(true);
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = ServiceName, RunAsLocalSystem = account == null, UserAccount = account });
            return serviceHandle;
        }

        private static InstallServiceOptions CreateOptions(string username, string dependencies = null, string serviceName = ServiceName)
        {
            return new InstallServiceOptions
            {
                ServiceName = serviceName,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                Username = username,
                Password = string.IsNullOrWhiteSpace(username) || ServiceAccounts.IsBuiltInServiceAccount(username.Trim()) ? null : "password",
                ServiceDependencies = dependencies,
            };
        }

        #region Dependency on the host

        [Theory]
        [InlineData(null, "Servy\0\0")]
        [InlineData("Tcpip;Dnscache", "Tcpip\0Dnscache\0Servy\0\0")]
        [InlineData("servy;Tcpip", "servy\0Tcpip\0\0")]
        public async Task InstallService_NewService_DependsOnTheHostService(string dependencies, string expected)
        {
            // Arrange
            ArrangeServiceCreated();
            ServiceDto saved = null;
            _serviceRepository.Setup(x => x.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<ServiceDto, bool, bool, CancellationToken>((dto, d1, d2, d3) => saved = dto).ReturnsAsync(1);

            // Act
            var result = await CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(null, dependencies), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _windowsServiceApi.Verify(x => x.CreateService(
                It.IsAny<SafeScmHandle>(), ServiceName, It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(),
                It.IsAny<string>(), null, IntPtr.Zero, expected, It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            // The implicit dependency is SCM-only: the configured dependencies are stored as the user typed them
            Assert.Equal(dependencies, saved.ServiceDependencies);
        }

        [Fact]
        public async Task InstallService_ExistingService_KeepsDependingOnTheHostService()
        {
            // Arrange
            ArrangeServiceAlreadyExists();

            // Act
            var result = await CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(null, "Tcpip"), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _windowsServiceApi.Verify(x => x.ChangeServiceConfig(
                It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), "Tcpip\0Servy\0\0", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Theory]
        [InlineData("Servy")]
        [InlineData("SERVY")]
        [InlineData(" servy ")]
        public async Task InstallService_TheHostServiceName_IsRefusedWithoutTouchingTheScm(string name)
        {
            // Act
            var (result, log) = await LogCapture.RunAsync(() => CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(null, serviceName: name), CancellationToken.None));

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("is reserved for the Servy host service", result.ErrorMessage);
            Assert.Contains("is reserved for the Servy host service", log);
            _windowsServiceApi.Verify(x => x.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>()), Times.Never);
            _serviceRepository.Verify(x => x.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Pipe access on install

        [Theory]
        [InlineData(@".\svc-account")]
        [InlineData(ServiceAccounts.LocalService)]
        public async Task InstallService_NewServiceUnderCustomAccount_GrantsPipeAccessAfterTheRowIsSaved(string account)
        {
            // Arrange
            ArrangeServiceCreated();
            var order = new List<string>();
            _serviceRepository.Setup(x => x.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("save")).ReturnsAsync(1);
            _pipes.Setup(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>())).Callback(() => order.Add("refresh")).ReturnsAsync(true);

            // Act
            var (result, log) = await LogCapture.RunAsync(() => CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(account), CancellationToken.None));

            // Assert: the host derives the DACL from Servy.db, so the row must exist before it is asked to rebuild it
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(new[] { "save", "refresh" }, order);
            Assert.Contains($"Granted '{account}' access to the Servy host named pipe (service '{ServiceName}').", log);
        }

        [Fact]
        public async Task InstallService_ExistingServiceUnderCustomAccount_GrantsPipeAccess()
        {
            // Arrange
            ArrangeServiceAlreadyExists();

            // Act
            var result = await CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(@".\svc-account"), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _pipes.Verify(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(ServiceAccounts.LocalSystem)]
        public async Task InstallService_NewServiceUnderLocalSystem_DoesNotTouchThePipe(string account)
        {
            // Arrange
            ArrangeServiceCreated();

            // Act
            var result = await CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(account), CancellationToken.None);

            // Assert: Local System always has Full Control on the pipe
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _pipes.Verify(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_HostNotRunning_InstallSucceedsAndSaysWhenTheAccessApplies()
        {
            // Arrange
            ArrangeServiceCreated();
            _pipes.Setup(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var (result, log) = await LogCapture.RunAsync(() => CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(@".\svc-account"), CancellationToken.None));

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Contains("It applies the change the next time the 'Servy' service starts.", log);
        }

        [Fact]
        public async Task InstallService_RefreshThrows_InstallStillSucceedsAndIsNotRolledBack()
        {
            // Arrange
            var serviceHandle = ArrangeServiceCreated();
            _pipes.Setup(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("pipe broke"));

            // Act
            var (result, log) = await LogCapture.RunAsync(() => CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(@".\svc-account"), CancellationToken.None));

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _windowsServiceApi.Verify(x => x.DeleteService(serviceHandle), Times.Never);
            Assert.Contains(@"Refreshing the Servy host named pipe access for '.\svc-account' (service 'PipedService') failed.", log);
        }

        [Fact]
        public async Task InstallService_WithoutPipeService_InstallsWithoutError()
        {
            // Arrange
            ArrangeServiceCreated();

            // Act
            var result = await CreateManager(null).InstallServiceAsync(CreateOptions(@".\svc-account"), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        #endregion

        #region Pipe access on uninstall and remove

        [Fact]
        public async Task UninstallService_CustomAccount_RefreshesThePipeAccessAfterTheDelete()
        {
            // Arrange
            ArrangeUninstall(@".\svc-account");
            var order = new List<string>();
            _serviceRepository.Setup(r => r.DeleteAsync(ServiceName, It.IsAny<CancellationToken>())).Callback(() => order.Add("delete")).ReturnsAsync(1);
            _pipes.Setup(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>())).Callback(() => order.Add("refresh")).ReturnsAsync(true);

            // Act
            var result = await CreateManager(_pipes.Object).UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert: the row is gone, so the rebuilt DACL keeps the account only if another service still uses it
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(new[] { "delete", "refresh" }, order);
        }

        [Fact]
        public async Task UninstallService_LocalSystem_DoesNotTouchThePipe()
        {
            // Arrange
            ArrangeUninstall(null);
            _serviceRepository.Setup(r => r.DeleteAsync(ServiceName, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await CreateManager(_pipes.Object).UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _pipes.Verify(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RevokeVaultAccessIfUnusedAsync_RemovedRecord_RefreshesThePipeAccess()
        {
            // Arrange: the Manager's "remove" deletes the row directly, then calls this
            var record = new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @".\svc-account" };

            // Act
            var log = await LogCapture.RunAsync(() => CreateManager(_pipes.Object).RevokeVaultAccessIfUnusedAsync(record, CancellationToken.None));

            // Assert
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\svc-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
            _pipes.Verify(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("the account keeps it only while another service runs under it", log);
        }

        [Fact]
        public async Task RevokeVaultAccessIfUnusedAsync_WithoutHardener_StillRefreshesThePipeAccess()
        {
            // Arrange
            var manager = new ServiceManager(
                _ => new Mock<IServiceControllerWrapper>().Object,
                new Mock<IServiceControllerProvider>().Object,
                _windowsServiceApi.Object,
                _win32ErrorProvider.Object,
                _serviceRepository.Object,
                exePermissionsHardener: null,
                namedPipesService: _pipes.Object);
            var record = new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @".\svc-account" };

            // Act
            await manager.RevokeVaultAccessIfUnusedAsync(record, CancellationToken.None);

            // Assert
            _pipes.Verify(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_MovedToAnotherAccount_RefreshesForTheNewAndTheFormerAccount()
        {
            // Arrange: the service ran under .\old-account and is reinstalled under .\new-account
            ArrangeServiceAlreadyExists();
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @".\old-account" });

            // Act
            var result = await CreateManager(_pipes.Object).InstallServiceAsync(CreateOptions(@".\new-account"), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\old-account", It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Once);
            _pipes.Verify(p => p.RefreshPipeAccessAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        #endregion
    }
}
