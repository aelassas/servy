using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Native;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Covers the call <see cref="ServiceManager.InstallServiceAsync"/> makes to
    /// <see cref="IServyExePermissionsHardener"/> once a service is installed: made for an account other than
    /// Local System, on both the "created" and the "already existed, reconfigured" paths, skipped for Local
    /// System and for a failed install, and never allowed to turn a successful install into a failed one.
    /// </summary>
    public class ServiceManagerHardeningTests : IDisposable
    {
        private const string ServiceName = "HardenedService";

        private readonly FakeServiceHandles _handles = new FakeServiceHandles();
        private readonly Mock<IWindowsServiceApi> _windowsServiceApi = new Mock<IWindowsServiceApi>();
        private readonly Mock<IWin32ErrorProvider> _win32ErrorProvider = new Mock<IWin32ErrorProvider>();
        private readonly Mock<IServiceRepository> _serviceRepository = new Mock<IServiceRepository>();
        private readonly Mock<IServyExePermissionsHardener> _hardener = new Mock<IServyExePermissionsHardener>();
        private readonly ServiceManager _serviceManager;

        public ServiceManagerHardeningTests()
        {
            _hardener.Setup(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _serviceRepository.Setup(x => x.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto)null);
            _serviceRepository.Setup(x => x.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _windowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(_handles.Scm(123));
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny)).Returns(true);
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>())).Returns(true);

            _serviceManager = new ServiceManager(
                _ => new Mock<IServiceControllerWrapper>().Object,
                new Mock<IServiceControllerProvider>().Object,
                _windowsServiceApi.Object,
                _win32ErrorProvider.Object,
                _serviceRepository.Object,
                _hardener.Object);
        }

        [Theory]
        [InlineData(@".\svc-account", @".\svc-account")]
        [InlineData(@"  DOMAIN\gMSA$  ", @"DOMAIN\gMSA$")]
        [InlineData(ServiceAccounts.LocalService, ServiceAccounts.LocalService)]
        [InlineData(ServiceAccounts.NetworkService, ServiceAccounts.NetworkService)]
        public async Task InstallService_NewServiceUnderCustomAccount_HardensForThatAccount(string username, string expectedAccount)
        {
            // Arrange
            var serviceHandle = ArrangeServiceCreated();
            var options = CreateOptions(username);

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.HardenAsync(expectedAccount, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _windowsServiceApi.Verify(x => x.DeleteService(serviceHandle), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(ServiceAccounts.LocalSystem)]
        [InlineData(@"NT AUTHORITY\SYSTEM")]
        [InlineData(@".\LocalSystem")]
        public async Task InstallService_NewServiceUnderLocalSystem_DoesNotHarden(string username)
        {
            // Arrange
            ArrangeServiceCreated();
            var options = CreateOptions(username);

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_ExistingServiceReconfiguredUnderCustomAccount_HardensForThatAccount()
        {
            // Arrange
            ArrangeServiceAlreadyExists();
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.ChangeServiceConfig(
                It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _hardener.Verify(h => h.HardenAsync(@".\svc-account", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_ExistingServiceReconfiguredUnderLocalSystem_DoesNotHarden()
        {
            // Arrange
            ArrangeServiceAlreadyExists();
            var options = CreateOptions(null);

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_CreateServiceFails_DoesNotHarden()
        {
            // Arrange
            _windowsServiceApi.Setup(x => x.CreateService(
                    It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                    It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_handles.Service(0));
            _win32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(5); // ERROR_ACCESS_DENIED
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            _hardener.Verify(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_HardeningReportsFailure_InstallStillSucceeds()
        {
            // Arrange
            var serviceHandle = ArrangeServiceCreated();
            _hardener.Setup(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.DeleteService(serviceHandle), Times.Never);
        }

        [Fact]
        public async Task InstallService_HardeningThrows_InstallStillSucceedsAndIsNotRolledBack()
        {
            // Arrange
            var serviceHandle = ArrangeServiceCreated();
            _hardener.Setup(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("script host missing"));
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.DeleteService(serviceHandle), Times.Never);
        }

        [Fact]
        public async Task InstallService_WithoutHardener_InstallsUnderCustomAccount()
        {
            // Arrange
            ArrangeServiceCreated();
            var manager = new ServiceManager(
                _ => new Mock<IServiceControllerWrapper>().Object,
                new Mock<IServiceControllerProvider>().Object,
                _windowsServiceApi.Object,
                _win32ErrorProvider.Object,
                _serviceRepository.Object);
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await manager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Arranges CreateService to create the service, so the "new service" success path runs.
        /// </summary>
        /// <returns>The handle of the created service.</returns>
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

        /// <summary>
        /// Arranges CreateService to fail while the service is listed as installed, so the
        /// "already exists, configuration updated" success path runs.
        /// </summary>
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

        private static InstallServiceOptions CreateOptions(string username)
        {
            return new InstallServiceOptions
            {
                ServiceName = ServiceName,
                Description = "Hardening test service",
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                Username = username,
                Password = string.IsNullOrWhiteSpace(username) || ServiceAccounts.IsBuiltInServiceAccount(username.Trim()) || username.Trim().EndsWith("$")
                    ? null
                    : "password"
            };
        }

        public void Dispose()
        {
            _handles.Dispose();
        }
    }
}
