using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Native;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System.ServiceProcess;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Covers the calls <see cref="ServiceManager"/> makes to <see cref="IServyExePermissionsHardener"/>.
    /// <see cref="ServiceManager.InstallServiceAsync"/> hardens for an account other than Local System, on both the
    /// "created" and the "already existed, reconfigured" paths, skipped for Local System and for a failed install.
    /// It also revokes the previous account's access when a reconfigured service moves to another account (#7161),
    /// and the account of the stale-casing row a reinstall drops, on the success paths only (#7191).
    /// When the service moves to another account, it also takes back the former account's service control rights
    /// before granting the new account's (#7221).
    /// <see cref="ServiceManager.UninstallServiceAsync"/> revokes the removed service's account after the delete,
    /// including for an orphan database record, and skips it for Local System and for a failed delete. Neither
    /// call is ever allowed to turn a successful install or uninstall into a failed one. Because every one of
    /// those failures is swallowed, the log line it writes is the only signal an operator gets that a security
    /// step did not happen, so the five failure arms assert it. Those tests drive the static logger - hence the
    /// sequential logger collection.
    /// <see cref="ServiceManager.RevokeVaultAccessIfUnusedAsync"/>, the public wrapper the Manager calls after a
    /// remove, is covered here as well: it passes no current account, so any non-Local-System former account is
    /// revoked, and a refused or throwing revocation still completes its task (#7190).
    /// </summary>
    [Collection(LoggerCollection.Name)]
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
                .ReturnsAsync((ServiceDto?)null);
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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
        public async Task InstallService_NewServiceUnderLocalSystem_DoesNotHarden(string? username)
        {
            // Arrange
            ArrangeServiceCreated();
            var options = CreateOptions(username);

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _hardener.Verify(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_HardeningReportsFailure_InstallStillSucceedsAndLogsAWarning()
        {
            // Arrange
            var serviceHandle = ArrangeServiceCreated();
            _hardener.Setup(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var options = CreateOptions(@".\svc-account");

            // Act
            var capture = await LogCapture.RunAsync(() => _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.True(capture.Result.IsSuccess);
            _windowsServiceApi.Verify(x => x.DeleteService(serviceHandle), Times.Never);

            // The install succeeded either way, so this line is the only thing that tells an operator the
            // hardening did not fully apply; deleting it makes the two outcomes identical in the log.
            Assert.Contains($@"[WARN] | Servy's file permissions were not fully hardened for '.\svc-account' (service '{ServiceName}')", capture.Log);
        }

        [Fact]
        public async Task InstallService_HardeningThrows_InstallStillSucceedsIsNotRolledBackAndLogsAnError()
        {
            // Arrange
            var serviceHandle = ArrangeServiceCreated();
            _hardener.Setup(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("script host missing"));
            var options = CreateOptions(@".\svc-account");

            // Act
            var capture = await LogCapture.RunAsync(() => _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.True(capture.Result.IsSuccess);
            _windowsServiceApi.Verify(x => x.DeleteService(serviceHandle), Times.Never);

            // The exception is swallowed, so the error line is the only trace of it.
            Assert.Contains($@"[ERROR] | Hardening Servy's file permissions for '.\svc-account' (service '{ServiceName}') failed.", capture.Log);
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
            var result = await manager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
                .ReturnsAsync((ServiceDto?)null);
            _serviceRepository.Setup(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            ArrangeServiceCreated();
            var options = CreateOptions(@".\new-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

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
                .ReturnsAsync((ServiceDto?)null);
            _serviceRepository.Setup(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            ArrangeServiceCreated();
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceRepository.Verify(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_StaleCasingRowDroppedThenInstallFails_RestoresTheRowAndRevokesNothing()
        {
            // Arrange: the casing-variance block drops the stale row (the SCM no longer has it), then CreateService
            // fails and the service is not listed either, so the install fails and ExecuteDatabaseRecoveryAsync
            // restores the row. The restored row still runs under its own account, so nothing may be revoked:
            // that is why ServiceManager.cs waits for the success paths instead of revoking straight after the drop.
            const string legacyName = "hardenedservice";
            _windowsServiceApi.Setup(x => x.GetServices()).Returns(new List<WindowsServiceInfo>());
            _serviceRepository.Setup(x => x.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = legacyName, RunAsLocalSystem = false, UserAccount = @".\old-account" });
            _serviceRepository.Setup(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _windowsServiceApi.Setup(x => x.CreateService(
                    It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                    It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_handles.Service(0));
            _win32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(5); // ERROR_ACCESS_DENIED
            var options = CreateOptions(@".\new-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _serviceRepository.Verify(r => r.DeleteAsync(legacyName, It.IsAny<CancellationToken>()), Times.Once);
            _serviceRepository.Verify(r => r.UpsertAsync(
                It.Is<ServiceDto>(d => d.Name == legacyName), false, false, CancellationToken.None), Times.Once);
            _hardener.Verify(h => h.HardenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\old-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { "upsert", "revoke" }, order);
        }

        [Fact]
        public async Task InstallService_ExistingServiceMovedToAnotherAccount_RevokesThePreviousAccountsServiceControlRights()
        {
            // Arrange
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\old-account");
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.RevokeServiceControlRights(It.IsAny<SafeServiceHandle>(), @".\old-account"), Times.Once);
            _windowsServiceApi.Verify(x => x.GrantServiceControlRights(It.IsAny<SafeServiceHandle>(), @".\svc-account"), Times.Once);
        }

        [Fact]
        public async Task InstallService_ExistingServiceMovedToAnotherAccount_RevokesTheServiceControlRightsBeforeTheGrant()
        {
            // Arrange: the revocation must come first. RevokeServiceControlRightsIfAccountChanged skips only on a
            // case-insensitive string match, so a former account that is the same SID under another spelling
            // (.\alice -> alice) is still revoked by SID - and granting first would have that revocation take back
            // the grant just written, leaving the service's own account with no control over its service. Both calls
            // log and swallow, so only an explicit order assertion can pin the sequence.
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\old-account");
            var order = new List<string>();
            _windowsServiceApi.Setup(x => x.RevokeServiceControlRights(It.IsAny<SafeServiceHandle>(), It.IsAny<string>()))
                .Callback(() => order.Add("revoke"));
            _windowsServiceApi.Setup(x => x.GrantServiceControlRights(It.IsAny<SafeServiceHandle>(), It.IsAny<string>()))
                .Callback(() => order.Add("grant"));
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { "revoke", "grant" }, order);
        }

        [Fact]
        public async Task InstallService_ExistingServiceMovedToLocalSystem_RevokesThePreviousAccountsServiceControlRights()
        {
            // Arrange: Local System gets no grant of its own, so the former account's is the only one left to remove
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\old-account");
            var options = CreateOptions(null);

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.RevokeServiceControlRights(It.IsAny<SafeServiceHandle>(), @".\old-account"), Times.Once);
        }

        [Fact]
        public async Task InstallService_ExistingServiceKeepsItsAccount_RevokesNoServiceControlRights()
        {
            // Arrange: the same account, written with another case
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\SVC-ACCOUNT");
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.RevokeServiceControlRights(It.IsAny<SafeServiceHandle>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_ExistingServiceWithoutPreviousRecord_RevokesNoServiceControlRights()
        {
            // Arrange: no former record, so there is no former account to take anything back from
            ArrangeServiceAlreadyExists();
            var options = CreateOptions(@".\svc-account");

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _windowsServiceApi.Verify(x => x.RevokeServiceControlRights(It.IsAny<SafeServiceHandle>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task InstallService_RevokingTheServiceControlRightsThrows_InstallStillSucceedsAndLogsAnError()
        {
            // Arrange
            ArrangeServiceAlreadyExists();
            ArrangeRecord(@".\old-account");
            _windowsServiceApi.Setup(x => x.RevokeServiceControlRights(It.IsAny<SafeServiceHandle>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("revocation failed"));
            var options = CreateOptions(@".\svc-account");

            // Act
            var capture = await LogCapture.RunAsync(() => _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.True(capture.Result.IsSuccess);
            _windowsServiceApi.Verify(x => x.GrantServiceControlRights(It.IsAny<SafeServiceHandle>(), @".\svc-account"), Times.Once);

            // The former account keeps its control over the service object and nothing fails, so without this
            // line the leftover grant is invisible.
            Assert.Contains($@"[ERROR] | Revoking the service control rights of '.\old-account' (service '{ServiceName}') failed.", capture.Log);
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
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, TestContext.Current.CancellationToken);

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
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, TestContext.Current.CancellationToken);

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
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, TestContext.Current.CancellationToken);

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
            var result = await CreateUninstallManager().UninstallServiceAsync(ServiceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\svc-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UninstallService_RevocationReportsFailure_UninstallStillSucceedsAndLogsAWarning()
        {
            // Arrange
            ArrangeUninstall(deleteSucceeds: true);
            ArrangeRecord(@".\svc-account");
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var capture = await LogCapture.RunAsync(() => CreateUninstallManager().UninstallServiceAsync(ServiceName, TestContext.Current.CancellationToken));

            // Assert
            Assert.True(capture.Result.IsSuccess);

            // The uninstalled service's account keeps its vault grant; the warning is the only report of it.
            Assert.Contains($@"[WARN] | The vault access of '.\svc-account' was not fully revoked after service '{ServiceName}' stopped using it.", capture.Log);
        }

        [Fact]
        public async Task UninstallService_RevocationThrows_UninstallStillSucceedsAndLogsAnError()
        {
            // Arrange
            ArrangeUninstall(deleteSucceeds: true);
            ArrangeRecord(@".\svc-account");
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ACL write failed"));

            // Act
            var capture = await LogCapture.RunAsync(() => CreateUninstallManager().UninstallServiceAsync(ServiceName, TestContext.Current.CancellationToken));

            // Assert
            Assert.True(capture.Result.IsSuccess);

            // The exception is swallowed, so the error line is the only trace of it.
            Assert.Contains($@"[ERROR] | Revoking the vault access of '.\svc-account' (service '{ServiceName}') failed.", capture.Log);
        }

        [Theory]
        [InlineData(@".\svc-account", @".\svc-account")]
        [InlineData(@"  DOMAIN\gMSA$  ", @"DOMAIN\gMSA$")]
        public async Task RevokeVaultAccessIfUnused_RecordUnderCustomAccount_RevokesThatAccount(string account, string expected)
        {
            // Arrange: the wrapper is the only caller that passes no current account, which is what makes
            // every non-Local-System former account eligible for revocation
            var record = new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = account };

            // Act
            await _serviceManager.RevokeVaultAccessIfUnusedAsync(record, TestContext.Current.CancellationToken);

            // Assert
            _hardener.Verify(h => h.RevokeIfUnusedAsync(expected, _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RevokeVaultAccessIfUnused_NullRecord_RevokesNothing()
        {
            // Arrange, Act & Assert
            await _serviceManager.RevokeVaultAccessIfUnusedAsync(null, TestContext.Current.CancellationToken);

            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RevokeVaultAccessIfUnused_RecordUnderLocalSystem_RevokesNothing()
        {
            // Arrange
            var record = new ServiceDto { Name = ServiceName, RunAsLocalSystem = true, UserAccount = null };

            // Act
            await _serviceManager.RevokeVaultAccessIfUnusedAsync(record, TestContext.Current.CancellationToken);

            // Assert
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RevokeVaultAccessIfUnused_RevocationReportsFailure_DoesNotThrow()
        {
            // Arrange: ServiceCommands.RemoveServiceAsync awaits this task and keeps Remove successful,
            // so a refused revocation must not surface as an exception
            var record = new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @".\svc-account" };
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var ex = await Record.ExceptionAsync(() => _serviceManager.RevokeVaultAccessIfUnusedAsync(record, TestContext.Current.CancellationToken));

            // Assert
            Assert.Null(ex);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\svc-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RevokeVaultAccessIfUnused_RevocationThrows_DoesNotThrow()
        {
            // Arrange
            var record = new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @".\svc-account" };
            _hardener.Setup(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ACL write failed"));

            // Act
            var ex = await Record.ExceptionAsync(() => _serviceManager.RevokeVaultAccessIfUnusedAsync(record, TestContext.Current.CancellationToken));

            // Assert
            Assert.Null(ex);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(@".\svc-account", _serviceRepository.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RevokeVaultAccessIfUnused_WithoutHardener_RevokesNothing()
        {
            // Arrange: the five-argument constructor leaves the hardener null
            var manager = new ServiceManager(
                _ => new Mock<IServiceControllerWrapper>().Object,
                new Mock<IServiceControllerProvider>().Object,
                _windowsServiceApi.Object,
                _win32ErrorProvider.Object,
                _serviceRepository.Object);
            var record = new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @".\svc-account" };

            // Act
            var ex = await Record.ExceptionAsync(() => manager.RevokeVaultAccessIfUnusedAsync(record, TestContext.Current.CancellationToken));

            // Assert
            Assert.Null(ex);
            _hardener.Verify(h => h.RevokeIfUnusedAsync(It.IsAny<string>(), It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Arranges the service's database record, as it was before the install or uninstall.
        /// </summary>
        /// <param name="account">The account it ran under; <see langword="null"/> for Local System.</param>
        private void ArrangeRecord(string? account)
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

        private static InstallServiceOptions CreateOptions(string? username)
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
