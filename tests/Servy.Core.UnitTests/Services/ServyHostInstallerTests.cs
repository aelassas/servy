using Moq;
using Servy.Core.Config;
using Servy.Core.Native;
using Servy.Core.Services;
using Servy.Testing;
using System.ServiceProcess;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Unit tests for <see cref="ServyHostInstaller"/>: installing the <c>Servy</c> host service, keeping it Automatic and
    /// Local System, starting and stopping it, and adding it to the dependencies of services an earlier version installed.
    /// </summary>
    [Collection(Logging.LoggerCollection.Name)]
    public class ServyHostInstallerTests : TempDirectoryTestBase
    {
        private const uint HostAccess = SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_START | (uint)SERVICE_QUERY_STATUS;

        private readonly FakeServiceHandles _handles = new FakeServiceHandles();
        private readonly Mock<IWindowsServiceApi> _api = new Mock<IWindowsServiceApi>();
        private readonly Mock<IWin32ErrorProvider> _errors = new Mock<IWin32ErrorProvider>();
        private readonly Mock<IServiceControllerProvider> _controllers = new Mock<IServiceControllerProvider>();
        private readonly Mock<IServiceControllerWrapper> _host = new Mock<IServiceControllerWrapper>();
        private readonly string _hostExe;

        public ServyHostInstallerTests()
        {
            _hostExe = Path.Combine(TempDirectory, "Servy.Host.exe");
            File.WriteAllText(_hostExe, "host");
            _controllers.Setup(c => c.GetService(AppConfig.ServyHostServiceName)).Returns(_host.Object);
            _api.Setup(a => a.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny)).Returns(true);
            _api.Setup(a => a.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);
        }

        public override void Dispose()
        {
            _handles.Dispose();
            base.Dispose();
        }

        private ServyHostInstaller Create() => new ServyHostInstaller(_api.Object, _errors.Object, _controllers.Object, TimeSpan.FromSeconds(2));

        /// <summary>Makes the host controller report <paramref name="statuses"/> in turn, then the last one forever.</summary>
        private void HostStatuses(params ServiceControllerStatus[] statuses)
        {
            var queue = new Queue<ServiceControllerStatus>(statuses);
            var current = statuses[0];
            _host.Setup(h => h.Refresh()).Callback(() => { if (queue.Count > 0) current = queue.Dequeue(); });
            _host.SetupGet(h => h.Status).Returns(() => current);
        }

        #region Constructor

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ServyHostInstaller(null!, _errors.Object, _controllers.Object));
            Assert.Throws<ArgumentNullException>(() => new ServyHostInstaller(_api.Object, null!, _controllers.Object));
            Assert.Throws<ArgumentNullException>(() => new ServyHostInstaller(_api.Object, _errors.Object, null!));
        }

        #endregion

        #region EnsureInstalledAndRunningAsync

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_NotInstalled_CreatesAnAutomaticLocalSystemServiceAndStartsIt()
        {
            // Arrange
            var scm = _handles.Scm(1);
            var created = _handles.Service(2);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE)).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Servy", HostAccess)).Returns(_handles.Service(0));
            _errors.Setup(e => e.GetLastWin32Error()).Returns(Errors.ERROR_SERVICE_DOES_NOT_EXIST);
            _api.Setup(a => a.CreateService(scm, "Servy", "Servy", HostAccess, (uint)SERVICE_WIN32_OWN_PROCESS, (uint)SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL, $"\"{_hostExe}\"", null, IntPtr.Zero, null, null, null)).Returns(created);
            HostStatuses(ServiceControllerStatus.Stopped, ServiceControllerStatus.Stopped, ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _api.Verify(a => a.CreateService(scm, "Servy", "Servy", HostAccess, (uint)SERVICE_WIN32_OWN_PROCESS, (uint)SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL, $"\"{_hostExe}\"", null, IntPtr.Zero, null, null, null), Times.Once);
            _api.Verify(a => a.ChangeServiceConfig2(created, (uint)SERVICE_CONFIG_DESCRIPTION, ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.Once);
            _host.Verify(h => h.Start(), Times.Once);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_Installed_SetsAutomaticAndTheExtractedPathAndStartsItWhenStopped()
        {
            // Arrange
            var scm = _handles.Scm(1);
            var existing = _handles.Service(3);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE)).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Servy", HostAccess)).Returns(existing);
            _api.Setup(a => a.ChangeServiceConfig(existing, SERVICE_NO_CHANGE, (uint)SERVICE_AUTO_START, SERVICE_NO_CHANGE, $"\"{_hostExe}\"",
                null, IntPtr.Zero, null, ServiceAccounts.LocalSystem, null, null)).Returns(true);
            HostStatuses(ServiceControllerStatus.Stopped, ServiceControllerStatus.Stopped, ServiceControllerStatus.StartPending, ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _api.Verify(a => a.ChangeServiceConfig(existing, SERVICE_NO_CHANGE, (uint)SERVICE_AUTO_START, SERVICE_NO_CHANGE, $"\"{_hostExe}\"",
                null, IntPtr.Zero, null, ServiceAccounts.LocalSystem, null, null), Times.Once);
            _api.Verify(a => a.ChangeServiceConfig2(existing, (uint)SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny), Times.Once);
            _api.Verify(a => a.CreateService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _host.Verify(h => h.Start(), Times.Once);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_AlreadyRunning_DoesNotStartItAgain()
        {
            // Arrange
            var scm = _handles.Scm(1);
            var existing = _handles.Service(3);
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Servy", HostAccess)).Returns(existing);
            _api.Setup(a => a.ChangeServiceConfig(existing, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            HostStatuses(ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _host.Verify(h => h.Start(), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_ExecutableMissing_FailsWithoutTouchingTheScm()
        {
            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(Path.Combine(TempDirectory, "absent.exe"), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("does not exist", result.ErrorMessage);
            _api.Verify(a => a.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>()), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_OpenServiceFailsForAnotherReason_FailsWithoutCreating()
        {
            // Arrange: access denied is not "not installed"
            var scm = _handles.Scm(1);
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Servy", HostAccess)).Returns(_handles.Service(0));
            _errors.Setup(e => e.GetLastWin32Error()).Returns(5);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _api.Verify(a => a.CreateService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_ScmCannotBeOpened_Fails()
        {
            // Arrange
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(_handles.Scm(0));
            _errors.Setup(e => e.GetLastWin32Error()).Returns(5);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to install or start the 'Servy' service", result.ErrorMessage);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_ServiceNeverStarts_FailsAfterTheTimeout()
        {
            // Arrange
            var scm = _handles.Scm(1);
            var existing = _handles.Service(3);
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Servy", HostAccess)).Returns(existing);
            _api.Setup(a => a.ChangeServiceConfig(existing, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            HostStatuses(ServiceControllerStatus.Stopped);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("did not reach the Running state", result.ErrorMessage);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_BlankPath_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Create().EnsureInstalledAndRunningAsync("  ", TestContext.Current.CancellationToken));
        }

        #endregion

        #region IsRunning / StopAsync / StartAsync

        [Theory]
        [InlineData(ServiceControllerStatus.Running, true)]
        [InlineData(ServiceControllerStatus.StartPending, true)]
        [InlineData(ServiceControllerStatus.Stopped, false)]
        [InlineData(ServiceControllerStatus.StopPending, false)]
        public void IsRunning_ReflectsTheStatus(ServiceControllerStatus status, bool expected)
        {
            HostStatuses(status);

            Assert.Equal(expected, Create().IsRunning());
        }

        [Fact]
        public void IsRunning_NotInstalled_ReturnsFalse()
        {
            _host.SetupGet(h => h.Status).Throws(new InvalidOperationException("not installed"));

            Assert.False(Create().IsRunning());
        }

        [Fact]
        public async Task StopAsync_Running_StopsAndWaitsForStopped()
        {
            // Arrange
            HostStatuses(ServiceControllerStatus.Running, ServiceControllerStatus.Running, ServiceControllerStatus.StopPending, ServiceControllerStatus.Stopped);

            // Act
            await Create().StopAsync(TestContext.Current.CancellationToken);

            // Assert
            _host.Verify(h => h.Stop(), Times.Once);
            Assert.Equal(ServiceControllerStatus.Stopped, _host.Object.Status);
        }

        [Fact]
        public async Task StopAsync_AlreadyStopped_DoesNothing()
        {
            HostStatuses(ServiceControllerStatus.Stopped);

            await Create().StopAsync(TestContext.Current.CancellationToken);

            _host.Verify(h => h.Stop(), Times.Never);
        }

        [Fact]
        public async Task StopAsync_NeverStops_Throws()
        {
            HostStatuses(ServiceControllerStatus.Running);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Create().StopAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task StartAsync_StopPending_WaitsForStoppedThenStarts()
        {
            // Arrange
            HostStatuses(ServiceControllerStatus.StopPending, ServiceControllerStatus.StopPending, ServiceControllerStatus.Stopped,
                ServiceControllerStatus.Stopped, ServiceControllerStatus.Running);

            // Act
            await Create().StartAsync(TestContext.Current.CancellationToken);

            // Assert
            _host.Verify(h => h.Start(), Times.Once);
        }

        [Fact]
        public async Task StartAndStop_Cancelled_Throw()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().StartAsync(cts.Token));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().StopAsync(cts.Token));
            }
        }

        #endregion

        #region EnsureServicesDependOnHostAsync

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_ServiceWithoutTheDependency_KeepsItsDependenciesAndAddsTheHost()
        {
            // Arrange
            var scm = _handles.Scm(1);
            var handle = _handles.Service(5);
            var legacy = new Mock<IServiceControllerWrapper>();
            legacy.Setup(s => s.GetDependencyNames()).Returns(new[] { "Tcpip" });
            _controllers.Setup(c => c.GetService("Legacy")).Returns(legacy.Object);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Legacy", SERVICE_CHANGE_CONFIG)).Returns(handle);
            _api.Setup(a => a.ChangeServiceConfig(handle, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, null, null, IntPtr.Zero,
                "Tcpip\0Servy\0\0", null, null, null)).Returns(true);

            // Act
            var updated = await Create().EnsureServicesDependOnHostAsync(new[] { "Legacy" }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, updated);
            _api.Verify(a => a.ChangeServiceConfig(handle, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, null, null, IntPtr.Zero,
                "Tcpip\0Servy\0\0", null, null, null), Times.Once);
        }

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_DependencyPresentOrNotInstalled_ChangesNothing()
        {
            // Arrange
            var scm = _handles.Scm(1);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            var current = new Mock<IServiceControllerWrapper>();
            current.Setup(s => s.GetDependencyNames()).Returns(new[] { "servy" });
            _controllers.Setup(c => c.GetService("Current")).Returns(current.Object);
            var orphan = new Mock<IServiceControllerWrapper>();
            orphan.Setup(s => s.GetDependencyNames()).Throws(new InvalidOperationException("not installed"));
            _controllers.Setup(c => c.GetService("DbOnly")).Returns(orphan.Object);

            // Act: the host itself and blank names are ignored as well
            var updated = await Create().EnsureServicesDependOnHostAsync(new[] { "Current", "DbOnly", "Servy", " ", "current" }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, updated);
            _api.Verify(a => a.ChangeServiceConfig(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _controllers.Verify(c => c.GetService("Servy"), Times.Never);
        }

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_ChangeRefused_IsLoggedAndCountedAsNotUpdated()
        {
            // Arrange
            var scm = _handles.Scm(1);
            var handle = _handles.Service(5);
            var legacy = new Mock<IServiceControllerWrapper>();
            legacy.Setup(s => s.GetDependencyNames()).Returns(Array.Empty<string>());
            _controllers.Setup(c => c.GetService("Legacy")).Returns(legacy.Object);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Legacy", SERVICE_CHANGE_CONFIG)).Returns(handle);
            _api.Setup(a => a.ChangeServiceConfig(handle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);
            _errors.Setup(e => e.GetLastWin32Error()).Returns(5);

            // Act
            var (updated, log) = await LogCapture.RunAsync(() => Create().EnsureServicesDependOnHostAsync(new[] { "Legacy" }, TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal(0, updated);
            Assert.Contains("Could not add the 'Servy' dependency to service 'Legacy'", log);
        }

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_NullNames_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => Create().EnsureServicesDependOnHostAsync(null!, TestContext.Current.CancellationToken));
        }

        #endregion
    }
}
