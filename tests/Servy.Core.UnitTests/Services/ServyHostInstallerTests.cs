using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Native;
using Servy.Core.Services;
using Servy.Testing;
using System.Runtime.InteropServices;
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
        private readonly Mock<IServiceHelper> _serviceHelper = new Mock<IServiceHelper>();
        private readonly List<IntPtr> _strings = new List<IntPtr>();
        private readonly string _hostExe;

        private delegate void QueryConfigCallback(SafeServiceHandle handle, IntPtr buffer, int size, out int required);

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
            foreach (var ptr in _strings)
                Marshal.FreeHGlobal(ptr);
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

        /// <summary>
        /// Makes the Service Control Manager report the host service as registered with <paramref name="binaryPath"/>,
        /// through the two-pass QueryServiceConfig the installer uses, and accept any reconfiguration.
        /// </summary>
        /// <returns>The handle the installer reconfigures the service through.</returns>
        private SafeServiceHandle RegisteredAs(string binaryPath)
        {
            var queryHandle = _handles.Service(7);
            var existing = _handles.Service(3);
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(() => _handles.Scm(1));
            _api.Setup(a => a.OpenService(It.IsAny<SafeScmHandle>(), "Servy", SERVICE_QUERY_CONFIG)).Returns(queryHandle);
            _api.Setup(a => a.OpenService(It.IsAny<SafeScmHandle>(), "Servy", HostAccess)).Returns(existing);
            _api.Setup(a => a.ChangeServiceConfig(existing, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

            int size = Marshal.SizeOf<QUERY_SERVICE_CONFIG>();
            var path = Marshal.StringToHGlobalUni(binaryPath);
            _strings.Add(path);
            _api.Setup(a => a.QueryServiceConfig(queryHandle, IntPtr.Zero, 0, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigCallback((SafeServiceHandle h, IntPtr p, int s, out int required) => required = size))
                .Returns(false);
            _api.Setup(a => a.QueryServiceConfig(queryHandle, It.Is<IntPtr>(p => p != IntPtr.Zero), size, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigCallback((SafeServiceHandle h, IntPtr p, int s, out int required) =>
                {
                    required = size;
                    Marshal.StructureToPtr(new QUERY_SERVICE_CONFIG { lpBinaryPathName = path }, p, false);
                }))
                .Returns(true);
            return existing;
        }

        /// <summary>Makes the Service Control Manager report that no service is named Servy.</summary>
        private void NotRegistered()
        {
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(() => _handles.Scm(1));
            _api.Setup(a => a.OpenService(It.IsAny<SafeScmHandle>(), "Servy", It.IsAny<uint>())).Returns(() => _handles.Service(0));
            _errors.Setup(e => e.GetLastWin32Error()).Returns(Errors.ERROR_SERVICE_DOES_NOT_EXIST);
        }

        /// <summary>Asserts that the Servy service was neither reconfigured, stopped, started nor created.</summary>
        private void AssertNothingTouched()
        {
            _api.Verify(a => a.ChangeServiceConfig(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _api.Verify(a => a.CreateService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _host.Verify(h => h.Stop(), Times.Never);
            _host.Verify(h => h.Start(), Times.Never);
            _serviceHelper.Verify(s => s.StopServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_NotInstalled_CreatesAnAutomaticLocalSystemServiceAndStartsIt()
        {
            // Arrange
            NotRegistered();
            var created = _handles.Service(2);
            _api.Setup(a => a.CreateService(It.IsAny<SafeScmHandle>(), "Servy", "Servy", HostAccess, (uint)SERVICE_WIN32_OWN_PROCESS, (uint)SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL, $"\"{_hostExe}\"", null, IntPtr.Zero, null, null, null)).Returns(created);
            HostStatuses(ServiceControllerStatus.Stopped, ServiceControllerStatus.Stopped, ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _api.Verify(a => a.CreateService(It.IsAny<SafeScmHandle>(), "Servy", "Servy", HostAccess, (uint)SERVICE_WIN32_OWN_PROCESS, (uint)SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL, $"\"{_hostExe}\"", null, IntPtr.Zero, null, null, null), Times.Once);
            _api.Verify(a => a.ChangeServiceConfig2(created, (uint)SERVICE_CONFIG_DESCRIPTION, ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.Once);
            _host.Verify(h => h.Start(), Times.Once);
            _serviceHelper.Verify(s => s.GetRunningServyServices(), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_InstalledWithTheExpectedPath_SetsAutomaticAndStartsItWhenStopped()
        {
            // Arrange
            var existing = RegisteredAs($"\"{_hostExe}\"");
            HostStatuses(ServiceControllerStatus.Stopped, ServiceControllerStatus.Stopped, ServiceControllerStatus.StartPending, ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _api.Verify(a => a.ChangeServiceConfig(existing, SERVICE_NO_CHANGE, (uint)SERVICE_AUTO_START, SERVICE_NO_CHANGE, $"\"{_hostExe}\"",
                null, IntPtr.Zero, null, ServiceAccounts.LocalSystem, null, null), Times.Once);
            _api.Verify(a => a.ChangeServiceConfig2(existing, (uint)SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny), Times.Once);
            _api.Verify(a => a.CreateService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(),
                It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _host.Verify(h => h.Start(), Times.Once);
            _host.Verify(h => h.Stop(), Times.Never);
            _serviceHelper.Verify(s => s.StopServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_AlreadyRunningFromTheExpectedPath_TouchesNoService()
        {
            // Arrange
            RegisteredAs($"\"{_hostExe}\"");
            HostStatuses(ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            _host.Verify(h => h.Start(), Times.Never);
            _host.Verify(h => h.Stop(), Times.Never);
            _serviceHelper.Verify(s => s.StopServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
            _serviceHelper.Verify(s => s.StartServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_RegisteredWithAnotherPath_StopsTheServicesAndTheHostRepointsItAndStartsThemAgain()
        {
            // Arrange: the other build (net48) registered its own host; one of its services is not run by this build's
            // wrappers but depends on the host, so it is found through its dependency
            var calls = new List<string>();
            var existing = RegisteredAs("\"C:\\ProgramData\\Servy\\Servy.Host.Net48.exe\"");
            _serviceHelper.Setup(s => s.GetRunningServyServices()).Returns(new List<string> { "UiService" });
            _controllers.Setup(c => c.GetServices()).Returns(new[]
            {
                Controller("Net48Service", ServiceControllerStatus.Running, "Servy"),
                Controller("Spooler", ServiceControllerStatus.Running, "RPCSS"),
                Controller("UiService", ServiceControllerStatus.Running, "Servy"),
                Controller("StoppedServyService", ServiceControllerStatus.Stopped, "Servy"),
            });

            IEnumerable<string>? stopped = null;
            IEnumerable<string>? started = null;
            _serviceHelper.Setup(s => s.StopServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, CancellationToken>((names, ct) => { stopped = names.ToList(); calls.Add("stop services"); })
                .Returns(Task.CompletedTask);
            _serviceHelper.Setup(s => s.StartServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, CancellationToken>((names, ct) => { started = names.ToList(); calls.Add("start services"); })
                .Returns(Task.CompletedTask);
            _api.Setup(a => a.ChangeServiceConfig(existing, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => calls.Add("repoint"))
                .Returns(true);
            _host.Setup(h => h.Stop()).Callback(() => calls.Add("stop host"));
            _host.Setup(h => h.Start()).Callback(() => calls.Add("start host"));
            HostStatuses(ServiceControllerStatus.Running, ServiceControllerStatus.StopPending, ServiceControllerStatus.Stopped,
                ServiceControllerStatus.Stopped, ServiceControllerStatus.StartPending, ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(new[] { "stop services", "stop host", "repoint", "start host", "start services" }, calls);
            Assert.Equal(new[] { "UiService", "Net48Service" }, stopped);
            Assert.Equal(stopped, started);
            _api.Verify(a => a.ChangeServiceConfig(existing, SERVICE_NO_CHANGE, (uint)SERVICE_AUTO_START, SERVICE_NO_CHANGE, $"\"{_hostExe}\"",
                null, IntPtr.Zero, null, ServiceAccounts.LocalSystem, null, null), Times.Once);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_HostFailsToStartFromTheNewPath_StillStartsTheStoppedServices()
        {
            // Arrange
            RegisteredAs("\"C:\\Dev\\Servy\\bin\\Debug\\Servy.Host.exe\"");
            _serviceHelper.Setup(s => s.GetRunningServyServices()).Returns(new List<string> { "UiService" });
            _controllers.Setup(c => c.GetServices()).Returns(Array.Empty<IServiceControllerWrapper>());
            HostStatuses(ServiceControllerStatus.Stopped);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert: the failure is reported, and the services are not left stopped
            Assert.False(result.IsSuccess);
            Assert.Contains("did not reach the Running state", result.ErrorMessage);
            _serviceHelper.Verify(s => s.StopServicesAsync(It.Is<IEnumerable<string>>(n => n.Single() == "UiService"), It.IsAny<CancellationToken>()), Times.Once);
            _serviceHelper.Verify(s => s.StartServicesAsync(It.Is<IEnumerable<string>>(n => n.Single() == "UiService"), CancellationToken.None), Times.Once);
        }

        [Theory]
        [InlineData("\"C:\\Apps\\MyApp\\MyApp.exe\" --serve")]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Service.exe\" \"Servy\"")]
        [InlineData("C:\\Tools\\NotServy.Host.exe")]
        public async Task EnsureInstalledAndRunningAsync_NameTakenByAnotherProgram_IsReportedAndLeftUntouched(string commandLine)
        {
            // Arrange: a service an earlier version let a user install under the name, before it was reserved (#7294)
            RegisteredAs(commandLine);
            HostStatuses(ServiceControllerStatus.Running);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("already exists and runs", result.ErrorMessage);
            Assert.Contains(commandLine, result.ErrorMessage);
            AssertNothingTouched();
            _serviceHelper.Verify(s => s.GetRunningServyServices(), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_ExecutableCannotBeRead_FailsAndTouchesNothing()
        {
            // Arrange: the service opens, but its configuration cannot be read, so it cannot be confirmed to be the host
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(() => _handles.Scm(1));
            _api.Setup(a => a.OpenService(It.IsAny<SafeScmHandle>(), "Servy", SERVICE_QUERY_CONFIG)).Returns(_handles.Service(8));
            _api.Setup(a => a.OpenService(It.IsAny<SafeScmHandle>(), "Servy", HostAccess)).Returns(_handles.Service(3));

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("could not be read", result.ErrorMessage);
            AssertNothingTouched();
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_OpenServiceFailsForAnotherReason_FailsAndTouchesNothing()
        {
            // Arrange: access denied is not "not installed"
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(() => _handles.Scm(1));
            _api.Setup(a => a.OpenService(It.IsAny<SafeScmHandle>(), "Servy", It.IsAny<uint>())).Returns(() => _handles.Service(0));
            _errors.Setup(e => e.GetLastWin32Error()).Returns(5);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            AssertNothingTouched();
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_ScmCannotBeOpened_FailsAndTouchesNothing()
        {
            // Arrange
            _api.Setup(a => a.OpenSCManager(null, null, It.IsAny<uint>())).Returns(() => _handles.Scm(0));
            _errors.Setup(e => e.GetLastWin32Error()).Returns(5);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            AssertNothingTouched();
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_ServiceNeverStarts_FailsAfterTheTimeout()
        {
            // Arrange
            RegisteredAs($"\"{_hostExe}\"");
            HostStatuses(ServiceControllerStatus.Stopped);

            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(_hostExe, _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("did not reach the Running state", result.ErrorMessage);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_ExecutableMissing_FailsWithoutTouchingTheScm()
        {
            // Act
            var result = await Create().EnsureInstalledAndRunningAsync(Path.Combine(TempDirectory, "absent.exe"), _serviceHelper.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("does not exist", result.ErrorMessage);
            _api.Verify(a => a.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>()), Times.Never);
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_BlankPath_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Create().EnsureInstalledAndRunningAsync("  ", _serviceHelper.Object, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task EnsureInstalledAndRunningAsync_NullServiceHelper_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => Create().EnsureInstalledAndRunningAsync(_hostExe, null!, TestContext.Current.CancellationToken));
        }

        #endregion

        #region Registration

        private static IServiceControllerWrapper Controller(string name, ServiceControllerStatus status, params string[] dependencies)
        {
            var controller = new Mock<IServiceControllerWrapper>();
            controller.SetupGet(s => s.ServiceName).Returns(name);
            controller.SetupGet(s => s.Status).Returns(status);
            controller.Setup(s => s.GetDependencyNames()).Returns(dependencies);
            return controller.Object;
        }

        [Theory]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.exe\"", ServyHostServiceState.ServyHost)]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.Net48.exe\"", ServyHostServiceState.ServyHost)]
        [InlineData("C:\\Dev\\bin\\Debug\\SERVY.HOST.EXE", ServyHostServiceState.ServyHost)]
        [InlineData("\"C:\\Apps\\MyApp.exe\"", ServyHostServiceState.Foreign)]
        public void GetState_RegisteredService_IsTheHostOnlyWhenItRunsAServyHostExecutable(string commandLine, ServyHostServiceState expected)
        {
            RegisteredAs(commandLine);

            Assert.Equal(expected, Create().GetState());
        }

        [Fact]
        public void GetState_NotInstalled_ReturnsNotInstalled()
        {
            NotRegistered();

            Assert.Equal(ServyHostServiceState.NotInstalled, Create().GetState());
        }

        [Fact]
        public void ReadRegistration_Installed_ReturnsTheCommandLine()
        {
            RegisteredAs("\"C:\\ProgramData\\Servy\\Servy.Host.exe\"");

            Assert.Equal((ServyHostServiceState.ServyHost, "\"C:\\ProgramData\\Servy\\Servy.Host.exe\""), Create().ReadRegistration());
        }

        [Theory]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.exe\"", true)]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.Net48.exe\" --debug", true)]
        [InlineData("C:\\ProgramData\\Servy\\servy.host.exe", true)]
        [InlineData("C:\\ProgramData\\Servy\\Servy.Host.exe.bat", false)]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.exe", false)]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Service.exe\" \"Servy\"", false)]
        [InlineData("C:\\Tools\\NotServy.Host.exe", false)]
        public void IsServyHostExecutable_RecognizesBothBuildsOfTheHost(string commandLine, bool expected)
        {
            Assert.Equal(expected, ServyHostInstaller.IsServyHostExecutable(commandLine));
        }

        [Theory]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.exe\"", "C:\\ProgramData\\Servy\\Servy.Host.exe", true)]
        [InlineData("C:\\ProgramData\\Servy\\Servy.Host.exe", "C:\\ProgramData\\Servy\\Servy.Host.exe", true)]
        [InlineData("  \"c:\\programdata\\SERVY\\servy.host.EXE\" ", "C:\\ProgramData\\Servy\\Servy.Host.exe", true)]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.exe\" --flag", "C:\\ProgramData\\Servy\\Servy.Host.exe", true)]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.Net48.exe\"", "C:\\ProgramData\\Servy\\Servy.Host.exe", false)]
        [InlineData("\"C:\\Dev\\bin\\Debug\\Servy.Host.exe\"", "C:\\ProgramData\\Servy\\Servy.Host.exe", false)]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Host.exe", "C:\\ProgramData\\Servy\\Servy.Host.exe", false)]
        public void IsSameExecutable_ComparesTheCommandLinesExecutable(string commandLine, string exePath, bool expected)
        {
            Assert.Equal(expected, ServyHostInstaller.IsSameExecutable(commandLine, exePath));
        }

        [Fact]
        public void GetRunningServyServices_JoinsTheWrapperServicesAndTheRunningDependentsWithoutDuplicates()
        {
            // Arrange
            _serviceHelper.Setup(s => s.GetRunningServyServices()).Returns(new List<string> { "UiService", "CliService" });
            _controllers.Setup(c => c.GetServices()).Returns(new[]
            {
                Controller("uiservice", ServiceControllerStatus.Running, "Servy"),
                Controller("Net48Service", ServiceControllerStatus.StartPending, "Tcpip", "servy"),
                Controller("StoppedDependent", ServiceControllerStatus.Stopped, "Servy"),
                Controller("Spooler", ServiceControllerStatus.Running, "RPCSS"),
            });

            // Act
            var names = Create().GetRunningServyServices(_serviceHelper.Object);

            // Assert
            Assert.Equal(new[] { "UiService", "CliService", "Net48Service" }, names);
        }

        [Fact]
        public void GetRunningServyServices_ServicesCannotBeListed_KeepsTheWrapperServices()
        {
            // Arrange
            _serviceHelper.Setup(s => s.GetRunningServyServices()).Returns(new List<string> { "UiService" });
            _controllers.Setup(c => c.GetServices()).Throws(new InvalidOperationException("SCM unavailable"));

            // Act & Assert
            Assert.Equal(new[] { "UiService" }, Create().GetRunningServyServices(_serviceHelper.Object));
        }

        #endregion

        #region StopAsync / StartAsync

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

        /// <summary>
        /// Makes the Service Control Manager report <paramref name="dependencies"/> as the raw
        /// <c>lpDependencies</c> of <paramref name="name"/>, through the two-pass <c>QueryServiceConfig</c> the
        /// installer reads them with, and hand out a handle opened for query and reconfiguration.
        /// </summary>
        /// <returns>The handle the installer reconfigures the service through.</returns>
        private SafeServiceHandle DependsOn(SafeScmHandle scm, string name, int handleId, params string[] dependencies)
        {
            var handle = _handles.Service(handleId);
            _api.Setup(a => a.OpenService(scm, name, SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG)).Returns(handle);

            // A MULTI_SZ: each entry null-terminated, the list closed by an empty one. No dependencies at all is a
            // null pointer, which is what the SCM reports for a service that has none.
            var multiSz = IntPtr.Zero;
            if (dependencies.Length > 0)
            {
                multiSz = Marshal.StringToHGlobalUni(string.Join("\0", dependencies) + "\0");
                _strings.Add(multiSz);
            }

            int size = Marshal.SizeOf<QUERY_SERVICE_CONFIG>();
            _api.Setup(a => a.QueryServiceConfig(handle, IntPtr.Zero, 0, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigCallback((SafeServiceHandle h, IntPtr p, int s, out int required) => required = size))
                .Returns(false);
            _api.Setup(a => a.QueryServiceConfig(handle, It.Is<IntPtr>(p => p != IntPtr.Zero), size, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigCallback((SafeServiceHandle h, IntPtr p, int s, out int required) =>
                {
                    required = size;
                    Marshal.StructureToPtr(new QUERY_SERVICE_CONFIG { lpDependencies = multiSz }, p, false);
                }))
                .Returns(true);
            return handle;
        }

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_ServiceWithoutTheDependency_KeepsItsDependenciesAndAddsTheHost()
        {
            // Arrange
            var scm = _handles.Scm(1);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            var handle = DependsOn(scm, "Legacy", 5, "Tcpip");
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
        public async Task EnsureServicesDependOnHostAsync_LoadOrderGroupDependency_KeepsTheGroupInsteadOfItsMembers()
        {
            // Arrange: the SCM holds '+TDI;Tcpip', while ServiceController.ServicesDependedOn would report the
            // services currently in the TDI group instead of the group itself
            var scm = _handles.Scm(1);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            var handle = DependsOn(scm, "Legacy", 5, "+TDI", "Tcpip");
            var expanded = new Mock<IServiceControllerWrapper>();
            expanded.Setup(s => s.GetDependencyNames()).Returns(new[] { "Nsi", "Tdx", "Tcpip" });
            _controllers.Setup(c => c.GetService("Legacy")).Returns(expanded.Object);
            _api.Setup(a => a.ChangeServiceConfig(handle, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, null, null, IntPtr.Zero,
                "+TDI\0Tcpip\0Servy\0\0", null, null, null)).Returns(true);

            // Act
            var updated = await Create().EnsureServicesDependOnHostAsync(new[] { "Legacy" }, TestContext.Current.CancellationToken);

            // Assert: the group reference survives, so "after any member of TDI" does not become "after all of them"
            Assert.Equal(1, updated);
            _api.Verify(a => a.ChangeServiceConfig(handle, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, null, null, IntPtr.Zero,
                "+TDI\0Tcpip\0Servy\0\0", null, null, null), Times.Once);
            _controllers.Verify(c => c.GetService("Legacy"), Times.Never);
        }

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_DependencyPresentOrNotInstalled_ChangesNothing()
        {
            // Arrange
            var scm = _handles.Scm(1);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            DependsOn(scm, "Current", 5, "servy");
            // A database-only record: the SCM does not know the service
            _api.Setup(a => a.OpenService(scm, "DbOnly", SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG)).Returns(() => _handles.Service(0));
            _errors.Setup(e => e.GetLastWin32Error()).Returns(Errors.ERROR_SERVICE_DOES_NOT_EXIST);

            // Act: the host itself and blank names are ignored as well
            var updated = await Create().EnsureServicesDependOnHostAsync(new[] { "Current", "DbOnly", "Servy", " ", "current" }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, updated);
            _api.Verify(a => a.ChangeServiceConfig(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _api.Verify(a => a.OpenService(scm, "Servy", It.IsAny<uint>()), Times.Never);
        }

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_ConfigurationUnreadable_IsLoggedAndChangesNothing()
        {
            // Arrange: the first pass reports no size, so the current dependency list cannot be read
            var scm = _handles.Scm(1);
            var handle = _handles.Service(5);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            _api.Setup(a => a.OpenService(scm, "Legacy", SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG)).Returns(handle);
            _api.Setup(a => a.QueryServiceConfig(handle, IntPtr.Zero, 0, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigCallback((SafeServiceHandle h, IntPtr p, int s, out int required) => required = 0))
                .Returns(false);
            _errors.Setup(e => e.GetLastWin32Error()).Returns(5);

            // Act
            var (updated, log) = await LogCapture.RunAsync(() => Create().EnsureServicesDependOnHostAsync(new[] { "Legacy" }, TestContext.Current.CancellationToken));

            // Assert: nothing is written back, because writing it would clear the dependencies that could not be read
            Assert.Equal(0, updated);
            Assert.Contains("Could not read the configuration of service 'Legacy'", log);
            _api.Verify(a => a.ChangeServiceConfig(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EnsureServicesDependOnHostAsync_ChangeRefused_IsLoggedAndCountedAsNotUpdated()
        {
            // Arrange
            var scm = _handles.Scm(1);
            _api.Setup(a => a.OpenSCManager(null, null, SC_MANAGER_CONNECT)).Returns(scm);
            var handle = DependsOn(scm, "Legacy", 5);
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
