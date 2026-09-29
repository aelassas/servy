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
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Covers the calls <see cref="ServiceManager"/> makes to <see cref="IServyExePermissionsHardener"/>.
    /// <see cref="ServiceManager.InstallServiceAsync"/> hardens for an account other than Local System, on both the
    /// "created" and the "already existed, reconfigured" paths, skipped for Local System and for a failed install.
    /// It also revokes the previous account's access when a reconfigured service moves to another account (#7161).
    /// <see cref="ServiceManager.UninstallServiceAsync"/> revokes the removed service's account after the delete,
    /// including for an orphan database record, and skips it for Local System and for a failed delete. Neither
    /// call is ever allowed to turn a successful install or uninstall into a failed one.
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
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

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

        #region Revocation (#7161)

        [Fact]
        public async Task InstallService_ExistingServiceMovedToAnotherAccount_RevokesThePreviousAccount()
        {
            // Arrange
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\old-account");
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.HardenAsync(@".\svc-account", It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\old-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_ExistingServiceMovedToLocalSystem_RevokesThePreviousAccount()
        {
            // Arrange
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\old-account");
            var options = CreateOptions(null);

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\old-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_ExistingServiceKeepsItsAccount_RevokesNothing()
        {
            // Arrange: the same account, written with another case
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\SVC-ACCOUNT");
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_PreviousRecordRanAsLocalSystem_RevokesNothing()
        {
            // Arrange
            ArrangeServiceAlreadyExists();
            ArrangeRecord(null);
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_NewServiceWithoutPreviousRecord_RevokesNothing()
        {
            // Arrange
            ArrangeServiceCreated();
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_StaleCasingRowDroppedFromDbOnly_RevokesItsAccount()
        {
            // Arrange: a record under another casing layout, whose service the SCM no longer has, so the
            // casing-variance block drops the row with DeleteAsync instead of going through UninstallServiceAsync.
            // The read before the drop decrypts and sees it; the read after it does not, because the row is gone.
            const string legacyName = "hardenedservice";
            _windowsServiceApi.Setup(x => x.GetServices()).Returns(new List<WindowsServiceInfo>());
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = legacyName, RunAsLocalSystem = false, UserAccount = @".\old-account" });
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto)null);
            _serviceRepository.Setup(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            ArrangeServiceCreated();
            var options = CreateOptions(@".\new-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceRepository.Verify(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.HardenAsync(@".\new-account", It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\old-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_StaleCasingRowDroppedFromDbOnly_KeepsAnUnchangedAccount()
        {
            // Arrange: the same drop, but the reinstall keeps the account the dropped row ran under, so the
            // fallback must not hand the revocation the account the install has just granted.
            const string legacyName = "hardenedservice";
            _windowsServiceApi.Setup(x => x.GetServices()).Returns(new List<WindowsServiceInfo>());
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = legacyName, RunAsLocalSystem = false, UserAccount = @".\SVC-ACCOUNT" });
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto)null);
            _serviceRepository.Setup(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            ArrangeServiceCreated();
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceRepository.Verify(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_ExistingServiceMovedToAnotherAccount_RevokesThePreviousAccountAfterTheUpsert()
        {
            // Arrange: the same move as InstallService_ExistingServiceMovedToAnotherAccount_RevokesThePreviousAccount,
            // with the call order recorded. The order is what makes the revocation work: the real
            // RevokeIfUnusedAsync asks the repository whether any remaining record still names the account, so it
            // must run after the UpsertAsync that rewrites this service's own row. Revoking first would read the
            // stale row, see the former account still in use and revoke nothing. The mocked hardener never reads
            // the repository, so only an explicit order assertion can pin it.
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\old-account");
            var order = new List<string>();
            _serviceRepository.Setup(x => x.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("upsert")).ReturnsAsync(1);
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("revoke")).ReturnsAsync(true);
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\old-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { "upsert", "revoke" }, order);
        }

        [Fact]
        public async Task UninstallService_ServiceUnderCustomAccount_AsksToRevokeThatAccountAfterTheDelete()
        {
            // Arrange
            var serviceHandle = ArrangeUninstall(deleteSucceeds: true);
            ArrangeRecord(@" .\svc-account ");
            var order = new List<string>();
            _serviceRepository.Setup(r => r.DeleteAsync(ServiceName, It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("delete")).ReturnsAsync(1);
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("revoke")).ReturnsAsync(true);

            // Act
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.DeleteService(serviceHandle), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\svc-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { "delete", "revoke" }, order);
        }

        [Fact]
        public async Task UninstallService_ServiceUnderLocalSystem_RevokesNothing()
        {
            // Arrange
            ArrangeUninstall(deleteSucceeds: true);
            ArrangeRecord(null);

            // Act
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UninstallService_DeleteServiceFails_RevokesNothing()
        {
            // Arrange
            ArrangeUninstall(deleteSucceeds: false);
            ArrangeRecord(@".\svc-account");

            // Act
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UninstallService_OrphanDatabaseRecord_RevokesItsAccount()
        {
            // Arrange: the SCM entry is already gone, only the database row remains
            _windowsServiceApi.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), ServiceName, It.IsAny<uint>())).Returns(() => _handles.Service(0));
            _win32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(Errors.ERROR_SERVICE_DOES_NOT_EXIST);
            ArrangeRecord(@".\svc-account");

            // Act
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\svc-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UninstallService_RevocationReportsFailure_UninstallStillSucceeds()
        {
            // Arrange
            ArrangeUninstall(deleteSucceeds: true);
            ArrangeRecord(@".\svc-account");
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task UninstallService_RevocationThrows_UninstallStillSucceeds()
        {
            // Arrange
            ArrangeUninstall(deleteSucceeds: true);
            ArrangeRecord(@".\svc-account");
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ACL write failed"));

            // Act
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
        }

        /// <summary>
        /// Arranges the service's database record, as it was before the install or uninstall.
        /// </summary>
        /// <param name="account">The account it ran under; <see langword="null"/> for Local System.</param>
        private void ArrangeRecord(string account)
        {
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = ServiceName, RunAsLocalSystem = account == null, UserAccount = account });
        }

        /// <summary>
        /// Arranges an installed, already stopped service whose deletion succeeds or fails.
        /// </summary>
        /// <returns>The service's handle.</returns>
        private SafeServiceHandle ArrangeUninstall(bool deleteSucceeds)
        {
            var serviceHandle = _handles.Service(456);
            _windowsServiceApi.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), ServiceName, It.IsAny<uint>())).Returns(serviceHandle);
            _windowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_STATUS>.IsAny)).Returns(false);
            _win32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(Errors.ERROR_SERVICE_NOT_ACTIVE);
            _windowsServiceApi.Setup(x => x.ChangeServiceConfig(
                    serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), null, null, IntPtr.Zero, null, null, null, null))
                .Returns(true);
            _windowsServiceApi.Setup(x => x.DeleteService(serviceHandle)).Returns(deleteSucceeds);
            return serviceHandle;
        }

        /// <summary>
        /// A manager whose service controller reports the service as stopped, so an uninstall reaches the delete.
        /// </summary>
        /// <returns>The manager.</returns>
        private ServiceManager CreateUninstallManager()
        {
            var controller = new Mock<IServiceControllerWrapper>();
            controller.Setup(c => c.Status).Returns(ServiceControllerStatus.Stopped);
            return new ServiceManager(
                _ => controller.Object,
                new Mock<IServiceControllerProvider>().Object,
                _windowsServiceApi.Object,
                _win32ErrorProvider.Object,
                _serviceRepository.Object,
                _hardener.Object);
        }

        #endregion

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
