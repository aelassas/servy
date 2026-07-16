using Moq;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using Servy.Testing;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using static Servy.Core.Native.NativeMethods;

#pragma warning disable CS8625

namespace Servy.Core.UnitTests.Services
{
    public class ServiceManagerTests
    {
        private readonly Mock<IServiceControllerWrapper> _mockController;
        private readonly Mock<IServiceControllerProvider> _mockServiceControllerProvider;
        private readonly Mock<IWindowsServiceApi> _mockWindowsServiceApi;
        private readonly Mock<IWin32ErrorProvider> _mockWin32ErrorProvider;
        private readonly Mock<IServiceRepository> _mockServiceRepository;
        private ServiceManager _serviceManager;

        public ServiceManagerTests()
        {
            _mockController = new Mock<IServiceControllerWrapper>();
            _mockServiceControllerProvider = new Mock<IServiceControllerProvider>();
            _mockWindowsServiceApi = new Mock<IWindowsServiceApi>();
            _mockWin32ErrorProvider = new Mock<IWin32ErrorProvider>();
            _mockServiceRepository = new Mock<IServiceRepository>();

            _serviceManager = new ServiceManager(_ =>
            _mockController.Object,
            _mockServiceControllerProvider.Object,
            _mockWindowsServiceApi.Object,
            _mockWin32ErrorProvider.Object,
            _mockServiceRepository.Object
            );
        }

        [Fact]
        public async Task InstallService_OptionsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _serviceManager.InstallServiceAsync(null!, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("options", exception.ParamName);
        }

        [Theory]
        [InlineData("", "C:\\Apps\\ServyWrapper.exe", "C:\\Apps\\RealApp.exe")] // Missing ServiceName
        [InlineData("TestService", "", "C:\\Apps\\RealApp.exe")]                // Missing WrapperExePath
        [InlineData("TestService", "C:\\Apps\\ServyWrapper.exe", "")]           // Missing RealExePath
        public async Task InstallService_Throws_ArgumentException(string serviceName, string wrapperExePath, string realExePath)
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                WrapperExePath = wrapperExePath,
                RealExePath = realExePath
            };

            // Act & Assert
            // No native SCManager, mock handles, or 13-argument CreateService mock arrangements 
            // are configured here because validation constraints fail and exit before hitting any OS boundaries.
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task InstallService_OptionalFieldsOmitted_Succeeds()
        {
            string serviceName = "TestService";
            string wrapperExePath = @"C:\Apps\Wrapper.exe";
            string realExePath = @"C:\Apps\App.exe";
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockServiceRepository.Setup(x => x.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto
                {
                    Name = serviceName,
                    Description = "desc",
                    ExecutablePath = realExePath,
                    Pid = 123,
                    RunAsLocalSystem = true,
                    UserAccount = null
                });

            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                serviceName,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<uint>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<uint>(),
               It.IsAny<IntPtr>()
               ))
               .Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                WrapperExePath = wrapperExePath,
                RealExePath = realExePath,
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchTimeout = 30
            };

            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            _mockServiceRepository.Verify(x => x.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task InstallService_Returns_Failure_On_Unexpected_Exception()
        {
            // --- Arrange ---
            string serviceName = "TestService";
            string wrapperExePath = @"C:\Apps\Wrapper.exe";
            string realExePath = @"C:\Apps\App.exe";
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            // Mock an exact name match to bypass the Unicode layout hardening block safely
            _mockServiceRepository.Setup(x => x.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto
                {
                    Name = serviceName,
                    Description = "desc",
                    ExecutablePath = realExePath,
                    Pid = 123,
                    RunAsLocalSystem = true,
                    UserAccount = null
                });

            // Standardize signatures with It.IsAny to ensure parameter position shifts don't bypass invocation expectations
            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                It.IsAny<string>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                It.IsAny<string>(),
                It.IsAny<string>(),
                null))
                .Throws(new Exception("Boom!"));

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                WrapperExePath = wrapperExePath,
                RealExePath = realExePath,
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchTimeout = 30
            };

            // --- Act ---
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // --- Assert ---
            Assert.False(result.IsSuccess);
            Assert.Contains("Error installing service", result.ErrorMessage);
            Assert.Contains("Boom!", result.ErrorMessage);
        }

        [Fact]
        public async Task InstallService_Returns_Failure_On_Win32_Errors()
        {
            // --- Arrange Common Setup ---
            var validScmHandle = CreateScmHandle(123);

            // Instantiating default safe handles sets IsInvalid = true automatically
            var invalidScmHandle = new SafeScmHandle();
            var invalidServiceHandle = new SafeServiceHandle();

            var serviceName = "TestService";
            var description = "Test Description";

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                Username = @".\username",
                Password = "password",
                PreLaunchExePath = "pre-launch.exe",
                PreLaunchWorkingDirectory = "preLaunchDir",
                PreLaunchArgs = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "pre-launch-stdout.log",
                PreLaunchStderrPath = "pre-launch-stderr.log",
                PreLaunchTimeout = 30,
                PreLaunchIgnoreFailure = true
            };

            // Ensure repository lookup passes safely by returning null (not update layout mode)
            _mockServiceRepository.Setup(x => x.GetByNameAsync(serviceName, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto?)null);

            // --- Scenario 1: OpenSCManager works, but CreateService fails natively ---
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(validScmHandle);

            _mockWindowsServiceApi.Setup(x => x.CreateService(
                validScmHandle,
                serviceName,
                serviceName,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                It.IsAny<string>(),
                null,
                null))
                .Returns(invalidServiceHandle); // Invalid handle intercepts leg

            _mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(5); // ERROR_ACCESS_DENIED

            // --- Act & Assert 1 ---
            var result1 = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result1.IsSuccess);
            Assert.Contains("Failed to create service", result1.ErrorMessage);

            // --- Scenario 2: OpenSCManager itself fails immediately ---
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(invalidScmHandle);

            _mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1074); // ERROR_SERVICE_DEPENDENCY_DELETED

            // --- Act & Assert 2 ---
            var result2 = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result2.IsSuccess);
            Assert.Contains("Failed to open Service Control Manager", result2.ErrorMessage);
        }

        [Fact]
        public async Task InstallService_CreatesService_AndSetsDescription_WhenServiceDoesNotExist()
        {
            // Arrange
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);
            var serviceName = "TestService";
            var description = "Test Description";

            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(() => scmHandle);

            _mockWindowsServiceApi
                .Setup(x => x.CreateService(
                    It.IsAny<SafeScmHandle>(), // Loose matching to bypass disposed state checks
                    serviceName,
                    serviceName,
                    It.IsAny<uint>(),
                    It.IsAny<uint>(),
                    It.IsAny<uint>(),
                    It.IsAny<uint>(),
                    It.IsAny<string>(),
                    null,
                    IntPtr.Zero,
                    ServiceDependenciesParser.NoDependencies,
                    ServiceAccounts.LocalSystem,
                    null))
                .Returns(() => serviceHandle);

            _mockWindowsServiceApi
                .Setup(x => x.ChangeServiceConfig2(
                    It.IsAny<SafeServiceHandle>(), // Loose matching
                    It.IsAny<uint>(),
                    ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi
                .Setup(x => x.ChangeServiceConfig2(
                   It.IsAny<SafeServiceHandle>(), // Loose matching
                   It.IsAny<uint>(),
                   It.IsAny<IntPtr>()
                   ))
                .Returns(true);

            // Removed the setup for CloseServiceHandle, as SafeHandle bypasses it

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                EnableSizeRotation = true,
                RotationSizeInBytes = 1024 * 1024,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 30,
                MaxFailedChecks = 3,
                MaxRestartAttempts = 1,
                PreLaunchExePath = "pre-launch.exe",
                PreLaunchWorkingDirectory = "preLaunchDir",
                PreLaunchArgs = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "pre-launch-stdout.log",
                PreLaunchStderrPath = "pre-launch-stderr.log",
                PreLaunchTimeout = 30,
                PreLaunchIgnoreFailure = true,
                FailureProgramPath = @"C:\Apps\App\app.exe",
                FailureProgramWorkingDirectory = @"C:\Apps\App",
                FailureProgramArgs = "--arg1 val1",
                PostLaunchExePath = @"C:\Apps\App\app.exe",
                PostLaunchWorkingDirectory = @"C:\Apps\App",
                PostLaunchArgs = "--arg1 val1"
            };

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.OpenSCManager(null, null, It.IsAny<uint>()), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CreateService(It.IsAny<SafeScmHandle>(), serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceAccounts.LocalSystem, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.Once);

            // Verify cleanup via SafeHandle state rather than the API mock
            Assert.True(serviceHandle.IsClosed, "Service handle was not disposed.");
            Assert.True(scmHandle.IsClosed, "SCM handle was not disposed.");
        }

        [Theory]
        [InlineData(ServiceStartType.Automatic)]
        [InlineData(ServiceStartType.AutomaticDelayedStart)]
        public async Task InstallService_CallsUpdateServiceConfig_WhenServiceExistsError(ServiceStartType startType)
        {
            // Arrange
            var scmHandle = CreateScmHandle(123);
            var serviceName = "TestService";
            var description = "Test Description";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                serviceName,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null))
                .Returns(CreateServiceHandle(0));

            _mockWindowsServiceApi.Setup(x => x.GetServices()).Returns(new List<WindowsServiceInfo> { new WindowsServiceInfo { ServiceName = serviceName } });

            var serviceHandle = CreateServiceHandle(456);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig(
                serviceHandle,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null,
                It.IsAny<string>()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<uint>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>())).Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = startType,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchExePath = "pre-launch.exe",
                PreLaunchWorkingDirectory = "preLaunchDir",
                PreLaunchArgs = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "pre-launch-stdout.log",
                PreLaunchStderrPath = "pre-launch-stderr.log",
                PreLaunchTimeout = 30,
                PreLaunchIgnoreFailure = true,
                DisplayName = serviceName
            };

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceAccounts.LocalSystem, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig(serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceAccounts.LocalSystem, null, It.IsAny<string>()), Times.Once);

            // BUNDLED SCENARIO: ChangeServiceConfig2 is invoked exactly Times.Once in both scenarios 
            // because the existing update configuration loop explicitly sets fDelayedAutostart to false 
            // for standard Automatic configurations to clear down stale state drift.
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny), Times.Once);
        }

        [Fact]
        public async Task InstallService_CallsUpdateServiceConfig2_WhenServiceExistsError()
        {
            var scmHandle = CreateScmHandle(123);
            var serviceName = "TestService";
            var description = "Test Description";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                serviceName,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null))
                .Returns(CreateServiceHandle(0));

            _mockWindowsServiceApi.Setup(x => x.GetServices()).Returns(new List<WindowsServiceInfo> { new WindowsServiceInfo { ServiceName = serviceName } });

            var serviceHandle = CreateServiceHandle(456);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig(
                serviceHandle,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null,
                It.IsAny<string>()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<uint>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(false);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>())).Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchExePath = "pre-launch.exe",
                PreLaunchWorkingDirectory = "preLaunchDir",
                PreLaunchArgs = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "pre-launch-stdout.log",
                PreLaunchStderrPath = "pre-launch-stderr.log",
                PreLaunchTimeout = 30,
                PreLaunchIgnoreFailure = true
            };

            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceAccounts.LocalSystem, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig(serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceAccounts.LocalSystem, null, It.IsAny<string>()), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_RequestPreShutdownTimeout()
        {
            var scmHandle = CreateScmHandle(123);
            var serviceName = "TestService";
            var description = "";
            var gMSA = @"TEST\gMSA$";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            var serviceHandle = CreateServiceHandle(456);
            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                It.IsAny<string>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                gMSA,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<uint>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<uint>(),
               It.IsAny<IntPtr>()))
               .Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.AutomaticDelayedStart,
                ProcessPriority = ProcessPriority.Normal,
                Username = gMSA,
                PreLaunchExePath = "pre-launch.exe",
                PreLaunchWorkingDirectory = "preLaunchDir",
                PreLaunchArgs = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "pre-launch-stdout.log",
                PreLaunchStderrPath = "pre-launch-stderr.log",
                PreLaunchTimeout = 30,
                PreLaunchIgnoreFailure = true,
                PreStopExePath = @"C:\Apps\pre-stop.exe"
            };

            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, gMSA, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_RequestPreShutdownTimeout_Error()
        {
            var scmHandle = CreateScmHandle(123);
            var serviceName = "TestService";
            var description = "";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            var serviceHandle = CreateServiceHandle(456);
            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                It.IsAny<string>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(false);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<uint>(),
               It.IsAny<IntPtr>()
               ))
               .Returns(false);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.AutomaticDelayedStart,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchExePath = "pre-launch.exe",
                PreLaunchWorkingDirectory = "preLaunchDir",
                PreLaunchArgs = "preLaunchArgs",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "pre-launch-stdout.log",
                PreLaunchStderrPath = "pre-launch-stderr.log",
                PreLaunchTimeout = 30,
                PreLaunchIgnoreFailure = true
            };

            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceAccounts.LocalSystem, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_DelayedAutoStart()
        {
            var scmHandle = CreateScmHandle(123);
            var serviceName = "TestService";
            var description = "Test Description";
            var gMSA = @"TEST\gMSA$";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            var serviceHandle = CreateServiceHandle(456);
            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                It.IsAny<string>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                gMSA,
                null))
                .Returns(serviceHandle);

            // Setup OpenService for UpdateServiceConfig

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<uint>(),
               ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                It.IsAny<SafeServiceHandle>(),
                It.IsAny<uint>(),
                It.IsAny<IntPtr>()
                ))
                .Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.AutomaticDelayedStart,
                ProcessPriority = ProcessPriority.Normal,
                Username = gMSA
            };

            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, gMSA, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny), Times.Once);
        }

        [Fact]
        public async Task InstallService_DelayedAutoStart_Error()
        {
            // Arrange
            var scmHandle = CreateScmHandle(123);
            var serviceName = "TestService";
            var description = "Test Description";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            var serviceHandle = CreateServiceHandle(456);
            _mockWindowsServiceApi.Setup(x => x.CreateService(
                scmHandle,
                serviceName,
                It.IsAny<string>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null))
                .Returns(serviceHandle);

            // Setup OpenService for UpdateServiceConfig
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            // Simulate failure when setting delayed auto-start
            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny))
                .Returns(false);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<uint>(),
               ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<uint>(),
               It.IsAny<IntPtr>()
               ))
               .Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.AutomaticDelayedStart,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchExePath = "pre-launch.exe",
                PreLaunchWorkingDirectory = "preLaunchDir",
                PreLaunchArgs = "preLaunchArgs",
                EnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "pre-launch-stdout.log",
                PreLaunchStderrPath = "pre-launch-stderr.log",
                PreLaunchTimeout = 30,
                PreLaunchRetryAttempts = 0,
                PreLaunchIgnoreFailure = true
            };

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(
                scmHandle,
                serviceName,
                serviceName,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<string>(),
                null,
                IntPtr.Zero,
                ServiceDependenciesParser.NoDependencies,
                ServiceAccounts.LocalSystem,
                null), Times.Once);

            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(
                It.IsAny<SafeServiceHandle>(),
                It.IsAny<uint>(),
                ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny), Times.Once);
        }

        #region InstallService Async Unicode Case-Variance and Recovery Tests

        [Fact]
        public async Task InstallService_UnicodeCasingVariance_SuccessfullyPurgesLegacyVariant()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "serviceÄ", // Target casing layout
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe"
            };

            var legacyDbRecord = new ServiceDto
            {
                Name = "serviceä", // Distinct Ordinal variant existing in DB
                Description = "Legacy Service Description String Layout"
            };

            // Extended Sequence to handle all 3 database verification reads:
            // Pass 1: The initial pre-install check in InstallServiceAsync (returns legacy record)
            // Pass 2: The internal verification read inside UninstallServiceAsync (returns legacy record to authorize drop)
            // Pass 3: The post-uninstall lookup in InstallServiceAsync to populate target properties (returns null)
            _mockServiceRepository
                .SetupSequence(x => x.GetByNameAsync(options.ServiceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(legacyDbRecord)   // Pass 1
                .ReturnsAsync(legacyDbRecord)   // Pass 2
                .ReturnsAsync((ServiceDto?)null); // Pass 3

            // Simulate that the Windows SCM currently registers the lowercase variant name
            _mockWindowsServiceApi
                .Setup(x => x.GetServices())
                .Returns(new[] { new WindowsServiceInfo { ServiceName = "serviceä" } });

            // Setup handles and dependencies needed to let UninstallServiceAsync pass cleanly
            var scmHandle = CreateScmHandle(123);
            var legacyServiceHandle = CreateServiceHandle(456);
            var targetServiceHandle = CreateServiceHandle(789);

            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi
                .Setup(x => x.OpenService(scmHandle, "serviceä", It.IsAny<uint>()))
                .Returns(legacyServiceHandle);

            _mockWindowsServiceApi
                .Setup(x => x.DeleteService(legacyServiceHandle))
                .Returns(true);

            // Setup mock targets for installing the new capitalized layout record
            _mockWindowsServiceApi
                .Setup(x => x.CreateService(scmHandle, options.ServiceName, options.ServiceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, It.IsAny<string>(), It.IsAny<string>(), null))
                .Returns(targetServiceHandle);

            _mockWindowsServiceApi
                .Setup(x => x.ChangeServiceConfig2(targetServiceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi
                .Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>()))
                .Returns(true);

            // Mock IServiceControllerWrapper for quick stopping transition loops inside UninstallServiceAsync
            var mockController = new Mock<IServiceControllerWrapper>();
            mockController.SetupGet(c => c.Status).Returns(ServiceControllerStatus.Stopped);

            _serviceManager = new ServiceManager(
                name => mockController.Object,
                _mockServiceControllerProvider.Object,
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object
            );

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify that DeleteService was programmatically forced onto the lower-case legacy variant string name
            _mockWindowsServiceApi.Verify(x => x.OpenService(scmHandle, "serviceä", It.IsAny<uint>()), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.DeleteService(legacyServiceHandle), Times.Once);

            // Verify that OpenSCManager / CreateService proceeded forward for the target string casing configuration
            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, "serviceÄ", "serviceÄ", It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, It.IsAny<string>(), It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task InstallService_UnicodeCasingVariance_UninstallFails_ReturnsOperationFailure()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "serviceÄ",
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe"
            };

            var legacyDbRecord = new ServiceDto { Name = "serviceä" };

            _mockServiceRepository
                .Setup(x => x.GetByNameAsync(options.ServiceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(legacyDbRecord);

            _mockWindowsServiceApi
                .Setup(x => x.GetServices())
                .Returns(new[] { new WindowsServiceInfo { ServiceName = "serviceä" } });

            var scmHandle = CreateScmHandle(123);
            var legacyServiceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi
                .Setup(x => x.OpenService(scmHandle, "serviceä", It.IsAny<uint>()))
                .Returns(legacyServiceHandle);

            // Force DeleteService to fail, simulating locked file assets or SCM driver blocks
            _mockWindowsServiceApi
                .Setup(x => x.DeleteService(legacyServiceHandle))
                .Returns(false);

            _mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1072); // ERROR_SERVICE_MARKED_FOR_DELETE

            var mockController = new Mock<IServiceControllerWrapper>();
            mockController.SetupGet(c => c.Status).Returns(ServiceControllerStatus.Stopped);
            _serviceManager = new ServiceManager(
                name => mockController.Object,
                _mockServiceControllerProvider.Object,
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object
            );

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to unregister legacy casing variant", result.ErrorMessage);

            // Verify execution short-circuited and never reached target creation phase
            _mockWindowsServiceApi.Verify(x => x.CreateService(It.IsAny<SafeScmHandle>(), "serviceÄ", It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
        }

        [Fact]
        public async Task InstallService_UnicodeCasingVariance_UnexpectedUninstallException_ReturnsFailure()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "serviceÄ",
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe"
            };

            var legacyDbRecord = new ServiceDto { Name = "serviceä" };

            _mockServiceRepository
                .Setup(x => x.GetByNameAsync(options.ServiceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(legacyDbRecord);

            _mockWindowsServiceApi
                .Setup(x => x.GetServices())
                .Returns(new[] { new WindowsServiceInfo { ServiceName = "serviceä" } });

            // Force OpenService to trigger a severe structural exception during step evaluations
            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Throws(new InvalidOperationException("Fatal SCM Memory Corruption."));

            // Act
            var result = await _serviceManager.InstallServiceAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Unexpected error occurred while trying to drop legacy service casing layout", result.ErrorMessage);
            Assert.Contains("Fatal SCM Memory Corruption.", result.ErrorMessage);
        }

        [Fact]
        public async Task InstallService_UserCancellationDuringCreation_TriggersDatabaseRecovery()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "serviceÄ",
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe"
            };

            var legacyDbRecord = new ServiceDto { Name = "serviceä", Description = "Recoverable Metadata Payload" };

            // Configure the repository to satisfy both the root read and the uninstallation validation checks
            _mockServiceRepository
                .SetupSequence(x => x.GetByNameAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(legacyDbRecord)  // Pass 1: InstallServiceAsync validation gate
                .ReturnsAsync(legacyDbRecord)  // Pass 2: UninstallServiceAsync internal authorization gate
                .ReturnsAsync((ServiceDto?)null); // Pass 3: Post-uninstall target validation step

            _mockWindowsServiceApi
                .Setup(x => x.GetServices())
                .Returns(new[] { new WindowsServiceInfo { ServiceName = "serviceä" } });

            var scmHandle = CreateScmHandle(123);
            var legacyServiceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi
                .Setup(x => x.OpenService(scmHandle, "serviceä", It.IsAny<uint>()))
                .Returns(legacyServiceHandle);

            _mockWindowsServiceApi
                .Setup(x => x.DeleteService(legacyServiceHandle))
                .Returns(true);

            // Mock IServiceControllerWrapper for quick stopping transition loops inside UninstallServiceAsync
            var mockController = new Mock<IServiceControllerWrapper>();
            mockController.SetupGet(c => c.Status).Returns(ServiceControllerStatus.Stopped);

            _serviceManager = new ServiceManager(
                name => mockController.Object,
                _mockServiceControllerProvider.Object,
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object
            );

            // Allow OpenSCManager to pass cleanly so uninstallation completes.
            // Trip the cancellation token inside CreateService to simulate a user cancel mid-installation phase.
            using (var cts = new CancellationTokenSource())
            {
                _mockWindowsServiceApi
                    .Setup(x => x.CreateService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, It.IsAny<string>(), It.IsAny<string>(), null))
                    .Callback(() => cts.Cancel())
                    .Throws(new OperationCanceledException(cts.Token));

                // Act & Assert
                await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    _serviceManager.InstallServiceAsync(options, cts.Token));

                // Verify recovery logic executed: legacyDroppedFromDb was true, meaning UpsertAsync was called to restore backup
                _mockServiceRepository.Verify(x => x.UpsertAsync(
                    It.Is<ServiceDto>(d => d.Name == "serviceä" && d.Description == "Recoverable Metadata Payload"),
                    false,
                    false,
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Fact]
        public async Task InstallService_UnexpectedException_DatabaseRecoveryThrows_ReturnsFailureResultWithoutPanic()
        {
            // Arrange
            var options = new InstallServiceOptions
            {
                ServiceName = "serviceÄ",
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe"
            };

            var legacyDbRecord = new ServiceDto
            {
                Name = "serviceä",
                Description = "Backup Payload Bound for Catastrophic Recovery Failure"
            };

            // Configure the repository to satisfy both the root read and the uninstallation validation checks
            _mockServiceRepository
                .SetupSequence(x => x.GetByNameAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(legacyDbRecord)  // Pass 1: Root pre-install layout validation check
                .ReturnsAsync(legacyDbRecord)  // Pass 2: UninstallServiceAsync internal authorization gate
                .ReturnsAsync((ServiceDto?)null); // Pass 3: Post-uninstall target validation step

            // CRITICAL: Force the recovery restoration pass to fail catastrophically
            _mockServiceRepository
                .Setup(x => x.UpsertAsync(It.IsAny<ServiceDto>(), false, false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database disk image is corrupt or file descriptor leaked."));

            _mockWindowsServiceApi
                .Setup(x => x.GetServices())
                .Returns(new[] { new WindowsServiceInfo { ServiceName = "serviceä" } });

            var scmHandle = CreateScmHandle(123);
            var legacyServiceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi
                .Setup(x => x.OpenService(scmHandle, "serviceä", It.IsAny<uint>()))
                .Returns(legacyServiceHandle);

            _mockWindowsServiceApi
                .Setup(x => x.DeleteService(legacyServiceHandle))
                .Returns(true);

            // Mock IServiceControllerWrapper to complete uninstallation loop evaluations immediately
            var mockController = new Mock<IServiceControllerWrapper>();
            mockController.SetupGet(c => c.Status).Returns(ServiceControllerStatus.Stopped);

            _serviceManager = new ServiceManager(
                name => mockController.Object,
                _mockServiceControllerProvider.Object,
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object
            );

            // Force an unexpected native engine exception inside CreateService to route into the catch-all block
            _mockWindowsServiceApi
                .Setup(x => x.CreateService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, It.IsAny<string>(), It.IsAny<string>(), null))
                .Throws(new Exception("SCM structural allocation failure."));

            // Act
            // Execute the action inside Record.ExceptionAsync EXACTLY ONCE and capture the direct output payload object ref
            OperationResult? result = null;
            var exception = await Record.ExceptionAsync(async () =>
            {
                result = await _serviceManager.InstallServiceAsync(options, TestContext.Current.CancellationToken);
            });

            // Assert
            // 1. Verify that the runtime completely suppressed the database recovery exception (returns null)
            Assert.Null(exception);

            // 2. Verify that the result object is populated and contains the wrapped operational failure information
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("Error installing service", result.ErrorMessage);
            Assert.Contains("SCM structural allocation failure.", result.ErrorMessage);

            // 3. Verify that the database recovery pipeline made exactly one isolated recovery push attempt
            _mockServiceRepository.Verify(x => x.UpsertAsync(
                It.Is<ServiceDto>(d => d.Name == "serviceä" && d.Description == "Backup Payload Bound for Catastrophic Recovery Failure"),
                false,
                false,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        [Fact]
        public void UpdateServiceConfig_Succeeds_WhenServiceIsOpenedAndConfigChanged()
        {
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);
            var serviceName = "TestService";
            var description = "Updated Description";
            var binPath = "binaryPath";

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig(
                serviceHandle,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                binPath,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                It.IsAny<string>()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<uint>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            // Test 1: Verify UpdateServiceConfig executes without exception
            var exception1 = Record.Exception(() => _serviceManager.UpdateServiceConfig(
                scmHandle,
                serviceName,
                description,
                binPath,
                ServiceStartType.Automatic,
                null,
                null,
                null,
                null
            ));
            Assert.Null(exception1);

            // Test 2: Verify UpdateServiceConfig executes without exception with service name
            var exception2 = Record.Exception(() => _serviceManager.UpdateServiceConfig(
                scmHandle,
                serviceName,
                description,
                binPath,
                ServiceStartType.Automatic,
                null,
                null,
                null,
                serviceName
            ));
            Assert.Null(exception2);
        }

        [Fact]
        public void UpdateServiceConfig_Throws_Win32Exception()
        {
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(0);
            var serviceName = "TestService";
            var description = "Updated Description";
            var binPath = "binaryPath";

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig(
                serviceHandle,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                binPath,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                null))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<uint>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            Assert.Throws<Win32Exception>(() =>
                _serviceManager.UpdateServiceConfig(
                    scmHandle,
                    serviceName,
                    description,
                    binPath,
                    ServiceStartType.Automatic,
                    null,
                    null,
                    null,
                    null
                    )
            );

            serviceHandle = CreateServiceHandle(123);

            Assert.Throws<Win32Exception>(() =>
                _serviceManager.UpdateServiceConfig(
                    scmHandle,
                    serviceName,
                    description,
                    binPath,
                    ServiceStartType.Automatic,
                    null,
                    null,
                    null,
                    null
                    )
            );
        }

        [Fact]
        public void SetServiceDescription_WhenDescriptionIsNullOrEmpty()
        {
            var serviceHandle = CreateServiceHandle(456);

            // Should not call ChangeServiceConfig2 if description is null or empty
            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _serviceManager.SetServiceDescription(serviceHandle, null);
            _serviceManager.SetServiceDescription(serviceHandle, "");

            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.AtLeast(1));
        }

        [Fact]
        public void SetServiceDescription_Throws_WhenChangeServiceConfig2Fails()
        {
            var serviceHandle = CreateServiceHandle(456);
            var description = "desc";

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<uint>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(false);

            Assert.Throws<Win32Exception>(() => _serviceManager.SetServiceDescription(serviceHandle, description));
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenOpenSCManagerFails()
        {
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(CreateScmHandle(0));

            var result = await _serviceManager.UninstallServiceAsync("ServiceName", TestContext.Current.CancellationToken);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task UninstallService_Throws_Win32Exception()
        {
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
             .Returns(CreateScmHandle(2));

            _mockWindowsServiceApi.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Throws(new Win32Exception("Boom!"));

            await Assert.ThrowsAsync<Win32Exception>(() => _serviceManager.UninstallServiceAsync("ServiceName", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenOpenServiceFails()
        {
            var scmHandle = CreateScmHandle(123);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "ServiceName", It.IsAny<uint>()))
                .Returns(CreateServiceHandle(0));

            var result = await _serviceManager.UninstallServiceAsync("ServiceName", TestContext.Current.CancellationToken);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenDeleteServiceFails()
        {
            var serviceName = "ServiceName";
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig(
                serviceHandle,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                null,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                null))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_STATUS>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(false);

            // Mock IServiceControllerWrapper to simulate service stopping quickly
            var mockController = new Mock<IServiceControllerWrapper>();

            var statusSequence = new Queue<ServiceControllerStatus>(new[]
            {
                ServiceControllerStatus.Running,
                ServiceControllerStatus.Stopped
            });

            mockController.Setup(c => c.Refresh())
                .Callback(() =>
                {
                    if (statusSequence.Count > 1) // keep Stopped as last state
                        statusSequence.Dequeue();
                });

            mockController.Setup(c => c.Status)
                .Returns(() => statusSequence.Peek());

            // Setup the factory to return this mock controller
            _serviceManager = new ServiceManager(
                svcName => mockController.Object,
                _mockServiceControllerProvider.Object,
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object
                );

            var result = await _serviceManager.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task UninstallService_StopsAndDeletesServiceSuccessfully()
        {
            var serviceName = "ServiceName";
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig(
                serviceHandle,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                null,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                null))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_STATUS>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(true);

            // Mock IServiceControllerWrapper to simulate service stopping quickly
            var mockController = new Mock<IServiceControllerWrapper>();

            var statusSequence = new Queue<ServiceControllerStatus>(new[]
            {
                ServiceControllerStatus.Running,
                ServiceControllerStatus.Stopped
            });

            mockController.Setup(c => c.Refresh())
                .Callback(() =>
                {
                    if (statusSequence.Count > 1) // keep Stopped as last state
                        statusSequence.Dequeue();
                });

            mockController.Setup(c => c.Status)
                .Returns(() => statusSequence.Peek());

            // Setup the factory to return this mock controller
            _serviceManager = new ServiceManager(
                svcName => mockController.Object,
                _mockServiceControllerProvider.Object,
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object
                );

            var result = await _serviceManager.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);

            mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);
            mockController.VerifyGet(c => c.Status, Times.AtLeastOnce);
        }


        [Fact]
        public async Task UninstallService_StopsAndDeletesServiceSuccessfully_WithPolling()
        {
            var serviceName = "ServiceName";
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig(
                serviceHandle,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                null,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                null))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_STATUS>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(true);

            // Mock the IServiceControllerWrapper to simulate service stopping over time
            var mockController = new Mock<IServiceControllerWrapper>();

            // Initial status is Running (or any other non-Stopped)
            var statusSequence = new Queue<ServiceControllerStatus>(new[]
            {
                ServiceControllerStatus.Running,
                ServiceControllerStatus.Paused,
                ServiceControllerStatus.Stopped
            });

            // On Refresh(), dequeue one status if available
            mockController.Setup(c => c.Refresh()).Callback(() =>
            {
                if (statusSequence.Count > 0)
                    statusSequence.Dequeue();
            });

            // Status returns current status or Stopped if none left
            mockController.Setup(c => c.Status)
                .Returns(() => statusSequence.Count > 0 ? statusSequence.Peek() : ServiceControllerStatus.Stopped);

            // Setup the factory to return the mock controller
            _serviceManager = new ServiceManager(
                name => mockController.Object,
                _mockServiceControllerProvider.Object,
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object
            );

            var result = await _serviceManager.UninstallServiceAsync(serviceName, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);

            // Verify the methods were called at least once
            mockController.Verify(sc => sc.Refresh(), Times.AtLeastOnce);
            mockController.Verify(sc => sc.Status, Times.AtLeastOnce);
        }


        [Fact]
        public async Task StartService_ShouldReturnTrue_WhenAlreadyRunning()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "TestService" });

            // Act
            var result = await _serviceManager.StartServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _mockController.Verify(c => c.Start(), Times.Never);
        }

        [Fact]
        public async Task StartService_ShouldStartAndPollUntilRunning_WithPreLaunch()
        {
            // Arrange
            var serviceName = "TestService";

            // 1. Initial Check (Stopped) -> Proceed to Start()
            // 2. First Loop Poll (Stopped) -> Wait and Refresh
            // 3. Second Loop Poll (Running) -> Exit Loop
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Stopped)
                .Returns(ServiceControllerStatus.Stopped)
                .Returns(ServiceControllerStatus.Running);

            _mockServiceRepository.Setup(r => r.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto
                {
                    Name = serviceName,
                    PreLaunchExecutablePath = @"C:\Apps\pre-launch.exe"
                });

            // Act
            var result = await _serviceManager.StartServiceAsync(serviceName, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify the native Start command was sent
            _mockController.Verify(c => c.Start(), Times.Once);

            // Verify that the loop actually checked the SCM for updates
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartService_ShouldStartAndPollUntilRunning()
        {
            // Arrange
            var serviceName = "TestService";

            // We simulate the progression: 
            // 1. Initial check (Stopped)
            // 2. First loop poll (Stopped)
            // 3. Second loop poll (Running) -> Loop exits
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Stopped)
                .Returns(ServiceControllerStatus.Stopped)
                .Returns(ServiceControllerStatus.Running);

            _mockServiceRepository.Setup(r => r.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto
                {
                    Name = serviceName,
                    StartTimeout = 5
                });

            // Act
            var result = await _serviceManager.StartServiceAsync(serviceName, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify the native Start command was issued
            _mockController.Verify(c => c.Start(), Times.Once);

            // Verify that we are now using Refresh() to get updated status from the SCM
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartService_ShouldReturnFalse_WhenServiceNotInDB()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(null as ServiceDto);

            // Act
            var result = await _serviceManager.StartServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _mockController.Verify(c => c.Start(), Times.Never);
        }

        [Fact]
        public async Task StartService_ShouldReturnFalse_WhenExceptionIsThrown()
        {
            _mockController.Setup(c => c.Status).Throws<InvalidOperationException>();
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "TestService", StartTimeout = 10 });

            var result = await _serviceManager.StartServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task StopService_ShouldReturnTrue_WhenAlreadyStopped()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Stopped);
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "TestService" });

            // Act
            var result = await _serviceManager.StopServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _mockController.Verify(c => c.Stop(), Times.Never);
        }

        [Fact]
        public async Task StopService_ShouldReturnFalse_WhenServiceNotFoundInDB()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Stopped);
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(null as ServiceDto);

            // Act
            var result = await _serviceManager.StopServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _mockController.Verify(c => c.Stop(), Times.Never);
        }

        [Fact]
        public async Task StopService_ShouldStopAndPollUntilStopped_WithPreStop()
        {
            // Arrange
            var serviceName = "TestService";

            // We want: 
            // 1. Initial check (Running) -> Proceed to Stop()
            // 2. First loop poll (Running) -> Wait and Refresh
            // 3. Second loop poll (Stopped) -> Exit Loop
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running)
                .Returns(ServiceControllerStatus.Running)
                .Returns(ServiceControllerStatus.Stopped);

            _mockServiceRepository.Setup(r => r.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto
                {
                    Name = serviceName,
                    PreStopExecutablePath = @"C:\Apps\pre-stop.exe"
                });

            // Act
            var result = await _serviceManager.StopServiceAsync(serviceName, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify the Stop command was sent
            _mockController.Verify(c => c.Stop(), Times.Once);

            // Verify the polling logic actually refreshed the status from the SCM
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);

            // REMOVE: WaitForStatus verify, as it is no longer used in the async version
        }

        [Fact]
        public async Task StopService_ShouldStopAndPollUntilStopped()
        {
            // Arrange
            var serviceName = "TestService";
            var stopTimeout = 5; // Keep it short for the test

            // We want the status to be Running the first time it's checked, 
            // then Stopped the next time to satisfy the loop.
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running) // Initial check before sc.Stop()
                .Returns(ServiceControllerStatus.Running) // First poll in the while loop
                .Returns(ServiceControllerStatus.Stopped); // Second poll (loop exits)

            _mockServiceRepository.Setup(r => r.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto
                {
                    Name = serviceName,
                    StopTimeout = stopTimeout
                });

            // Act
            var result = await _serviceManager.StopServiceAsync(serviceName, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify sc.Stop() was called
            _mockController.Verify(c => c.Stop(), Times.Once);

            // Verify we polled for status (Refresh is called inside the loop)
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StopService_ShouldReturnFalse_WhenExceptionIsThrown()
        {
            _mockController.Setup(c => c.Status).Throws<InvalidOperationException>();
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "TestService", StopTimeout = 10 });

            var result = await _serviceManager.StopServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task RestartService_ShouldStopAndStart_AndRefreshStatus()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
                // --- STOP PHASE ---
                .Returns(ServiceControllerStatus.Running)      // Entry check (proceed to stop)
                .Returns(ServiceControllerStatus.StopPending)  // 1st Loop check (enter loop)
                .Returns(ServiceControllerStatus.Stopped)      // 2nd Loop check (exit loop)

                // --- START PHASE ---
                .Returns(ServiceControllerStatus.Stopped)      // Entry check (proceed to start)
                .Returns(ServiceControllerStatus.StartPending) // 1st Loop check (enter loop)
                .Returns(ServiceControllerStatus.Running);     // 2nd Loop check (exit loop)

            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "TestService" });

            // Act
            var result = await _serviceManager.RestartServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            _mockController.Verify(c => c.Stop(), Times.Once);
            _mockController.Verify(c => c.Start(), Times.Once);

            // Each phase (stop, start) polls the SCM once -> exactly 2 refreshes
            _mockController.Verify(c => c.Refresh(), Times.Exactly(2));
        }

        [Fact]
        public async Task RestartService_ShouldReturnFalse_WhenStopServiceFails()
        {
            // Arrange
            // Provide a valid service instance lookup from the repository to prevent early lookup exit
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "TestService" });

            // Service is Running so RestartService proceeds to stop it
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);

            // Simulate Stop() throwing, which should trigger the catch and return false
            _mockController.Setup(c => c.Stop()).Throws(new Exception("Boom!"));

            // Act
            var result = await _serviceManager.RestartServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);

            // Explicitly check that the propagated message originated from the thrown exception,
            // proving that execution successfully targeted the live sc.Stop() intercept wrapper rather than data validation.
            Assert.Contains("Boom!", result.ErrorMessage);
        }

        [Fact]
        public async Task RestartService_ShouldReturnFalse_WhenStartServiceFails()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
               .Returns(ServiceControllerStatus.Running) // 1. Stop Entry
               .Returns(ServiceControllerStatus.Stopped) // 2. Stop Loop Exit
               .Returns(ServiceControllerStatus.Stopped); // 3. Start Entry (Triggers the Start() call)

            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "TestService" });

            // Entry check sees "Stopped", so Start() is reached and throws
            _mockController.Setup(c => c.Start()).Throws(new Exception("Boom!"));

            // Act
            var result = await _serviceManager.RestartServiceAsync("TestService", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Boom!", result.ErrorMessage); // Optional: verify the error message is propagated

            // Verify that we actually tried to start it before it blew up
            _mockController.Verify(c => c.Start(), Times.Once);
        }

        [Fact]
        public void GetServiceStatus_ShouldReturnRunning()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);

            // Act
            var result = _serviceManager.GetServiceStatus("TestService", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ServiceControllerStatus.Running, result);
        }

        [Fact]
        public void GetServiceStatus_ShouldThrowArgumentException()
        {
            // Assert
            Assert.Throws<ArgumentException>(() => _serviceManager.GetServiceStatus("", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsServiceInstalled_ReturnsTrue_WhenServiceExists()
        {
            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "MyService", DisplayName = "My Service" }
                 });

            Assert.True(_serviceManager.IsServiceInstalled("MyService", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsServiceInstalled_ReturnsFalse_WhenServiceMissing()
        {
            _mockWindowsServiceApi.Setup(p => p.GetServices()).Returns(Array.Empty<WindowsServiceInfo>());

            Assert.False(_serviceManager.IsServiceInstalled("MyService", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsServiceInstalled_Throws_ArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _serviceManager.IsServiceInstalled(string.Empty, TestContext.Current.CancellationToken));
        }

        #region GetServiceStartupType

        [Fact]
        public void GetServiceStartupType_ShouldThrowArgumentException_WhenNameIsInvalid()
        {
            Assert.Throws<ArgumentException>(() => _serviceManager.GetServiceStartupType(null!, TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentException>(() => _serviceManager.GetServiceStartupType(" ", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void GetServiceStartupType_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                Assert.Throws<OperationCanceledException>(() =>
                    _serviceManager.GetServiceStartupType("AnyService", cts.Token));
            }
        }

        [Theory]
        [InlineData(ServiceStartMode.Automatic, false, ServiceStartType.Automatic)]
        [InlineData(ServiceStartMode.Automatic, true, ServiceStartType.AutomaticDelayedStart)]
        [InlineData(ServiceStartMode.Manual, false, ServiceStartType.Manual)]
        [InlineData(ServiceStartMode.Disabled, false, ServiceStartType.Disabled)]
        public void GetServiceStartupType_ShouldReturnCorrectType_ForAllModes(
            ServiceStartMode nativeMode,
            bool isDelayed,
            ServiceStartType expected)
        {
            // Arrange
            const string serviceName = "TestService";

            // Create the handles to be injected
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);

            // 1. Setup the Mock Controller StartType
            _mockController.Setup(c => c.StartType).Returns(nativeMode);

            // 2. Setup Native API for the "Automatic" branch
            if (nativeMode == ServiceStartMode.Automatic)
            {
                _mockWindowsServiceApi
                    .Setup(api => api.OpenSCManager(null, null, It.IsAny<uint>()))
                    .Returns(() => scmHandle); // Use factory lambda

                _mockWindowsServiceApi
                    .Setup(api => api.OpenService(It.IsAny<SafeScmHandle>(), serviceName, It.IsAny<uint>()))
                    .Returns(() => svcHandle); // Use factory lambda and loose matching

                _mockWindowsServiceApi
                    .Setup(api => api.QueryServiceConfig2(
                        It.IsAny<SafeServiceHandle>(), // Loose matching
                        SERVICE_CONFIG_DELAYED_AUTO_START_INFO, // SERVICE_CONFIG_DELAYED_AUTO_START_INFO
                        ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny,
                        It.IsAny<int>(),
                        out It.Ref<int>.IsAny))
                        .Returns(new QueryConfig2DelayedStartDelegate((SafeServiceHandle h, uint lvl, ref SERVICE_DELAYED_AUTO_START_INFO info, int sz, ref int req) =>
                        {
                            info.fDelayedAutostart = isDelayed;
                            return true;
                        }));
            }

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expected, result);

            // Verify cleanup only happens if we entered the native block
            if (nativeMode == ServiceStartMode.Automatic)
            {
                // Verify the handle state itself, since SafeHandle.Dispose() bypasses the mock
                Assert.True(svcHandle.IsClosed, "Service handle was not disposed.");
                Assert.True(scmHandle.IsClosed, "SCM handle was not disposed.");
            }
        }

        [Fact]
        public void GetServiceStartupType_ShouldReturnAutomatic_WhenQueryServiceConfig2Fails()
        {
            // Arrange
            const string serviceName = "TestService";
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);

            // Setup the controller to return Automatic
            _mockController.Setup(c => c.StartType).Returns(ServiceStartMode.Automatic);

            // Setup native handles to succeed using factory lambdas
            _mockWindowsServiceApi
                .Setup(api => api.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(() => scmHandle);

            _mockWindowsServiceApi
                .Setup(api => api.OpenService(It.IsAny<SafeScmHandle>(), serviceName, It.IsAny<uint>()))
                .Returns(() => svcHandle);

            // THE KEY: Setup QueryServiceConfig2 to return false (ok = false)
            _mockWindowsServiceApi
                .Setup(api => api.QueryServiceConfig2(
                    It.IsAny<SafeServiceHandle>(),
                    SERVICE_CONFIG_DELAYED_AUTO_START_INFO, // SERVICE_CONFIG_DELAYED_AUTO_START_INFO
                    ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny,
                    It.IsAny<int>(),
                    out It.Ref<int>.IsAny))
                .Returns(false);

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName, TestContext.Current.CancellationToken);

            // Assert
            // It should remain 'Automatic' because the check for 'Delayed' failed
            Assert.Equal(ServiceStartType.Automatic, result);

            // Verify handles are still cleaned up even on P/Invoke failure
            // We check IsClosed because SafeHandle.Dispose() triggers native cleanup 
            // that bypasses the Moq interface.
            Assert.True(svcHandle.IsClosed, "Service handle was not disposed.");
            Assert.True(scmHandle.IsClosed, "SCM handle was not disposed.");
        }

        [Fact]
        public void GetServiceStartupType_ShouldReturnUnknown_WhenExceptionOccurs()
        {
            // Arrange
            _mockController.Setup(c => c.StartType).Returns(ServiceStartMode.Automatic);
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>()))
                    .Throws(new Exception("Native Failure"));

            // Act
            var result = _serviceManager.GetServiceStartupType("AnyService", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ServiceStartType.Unknown, result);
        }

        [Fact]
        public void GetServiceStartupType_ShouldFallbackToAutomatic_WhenOpenSCManagerFails()
        {
            // Arrange
            string serviceName = "EventLog";

            // 1. MUST setup the controller mock to return Automatic 
            // so the code enters the P/Invoke block you want to test.
            _mockController.Setup(x => x.StartType).Returns(ServiceStartMode.Automatic);

            // 2. Simulate the Native API failure (e.g., Access Denied)
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>()))
                    .Returns(CreateScmHandle(0));

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName, TestContext.Current.CancellationToken);

            // Assert
            // It should stay 'Automatic' because the P/Invoke to check for 'Delayed' failed.
            Assert.Equal(ServiceStartType.Automatic, result);

            // Verify we actually tried to open the manager
            _mockWindowsServiceApi.Verify(x => x.OpenSCManager(null!, null!, It.IsAny<uint>()), Times.Once);
        }

        [Fact]
        public void GetServiceStartupType_ShouldLogAndReturnUnknown_WhenControllerThrows()
        {
            // Arrange
            string serviceName = "FaultyService";
            var expectedException = new Exception("Access Denied");

            // Force the mock to throw when StartType is accessed
            _mockController.Setup(c => c.StartType).Throws(expectedException);

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ServiceStartType.Unknown, result);
        }

        #endregion

        #region GetAllServices

        [Fact]
        public void GetAllServices_ShouldThrowWin32Exception_WhenSCManagerFailsToOpen()
        {
            // Branch: scmHandle == IntPtr.Zero
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>())).Returns(CreateScmHandle(0));

            Assert.Throws<Win32Exception>(() => _serviceManager.GetAllServices(TestContext.Current.CancellationToken));
        }

        [Fact]
        public void GetAllServices_ShouldReturnEmpty_WhenNoServicesFound()
        {
            // Branch: Parallel.ForEach with empty list
            _mockServiceControllerProvider.Setup(x => x.GetServices()).Returns(Array.Empty<IServiceControllerWrapper>());
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>())).Returns(CreateScmHandle(1));

            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(ServiceControllerStatus.Running, ServiceStatus.Running)]
        [InlineData(ServiceControllerStatus.Stopped, ServiceStatus.Stopped)]
        [InlineData(ServiceControllerStatus.Paused, ServiceStatus.Paused)]
        [InlineData(ServiceControllerStatus.StartPending, ServiceStatus.StartPending)]
        [InlineData(ServiceControllerStatus.StopPending, ServiceStatus.StopPending)]
        [InlineData(ServiceControllerStatus.PausePending, ServiceStatus.PausePending)]
        [InlineData(ServiceControllerStatus.ContinuePending, ServiceStatus.ContinuePending)]
        [InlineData((ServiceControllerStatus)999, ServiceStatus.None)] // Covers 'default'
        public void GetAllServices_ShouldMapAllStatuses(ServiceControllerStatus native, ServiceStatus expected)
        {
            // Arrange
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");
            mockSvc.Setup(s => s.Status).Returns(native);

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });

            // Use a factory for the return and loose matching for downstream calls
            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>()))
                .Returns(() => CreateScmHandle(1));

            // We MUST mock OpenService if GetAllServices iterates, otherwise it returns null handles
            _mockWindowsServiceApi
                .Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Returns(() => CreateServiceHandle(2));

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expected, result[0].Status);
        }

        [Fact]
        public void GetAllServices_ShouldFallbackToUnknown_WhenStartTypeThrows()
        {
            // Arrange
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");
            // Simulate "Access Denied" by throwing inside the property getter
            mockSvc.Setup(s => s.StartType).Throws(new Exception("Access Denied"));

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });

            // 1. Use factory lambda for SCM handle
            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>()))
                .Returns(() => CreateScmHandle(1));

            // 2. REQUIRED: Mock OpenService to prevent NRE in PopulateNativeDetails
            _mockWindowsServiceApi
                .Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Returns(() => CreateServiceHandle(2));

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            // Verify it hit the catch block and defaulted to Manual (as per the implementation logic)
            Assert.Equal(ServiceStartType.Unknown, result[0].StartupType);
        }

        [Fact]
        public void GetAllServices_ShouldHandleEmptyConfig_AndDelayedFalse()
        {
            // Arrange
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");
            mockSvc.Setup(s => s.StartType).Returns(ServiceStartMode.Automatic);

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>())).Returns(scmHandle);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "TestSvc", It.IsAny<uint>())).Returns(svcHandle);

            // 1. Force 'bytesNeeded > 0' to be FALSE for User/Description
            int zero = 0;
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, IntPtr.Zero, 0, out zero)).Returns(false);
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DESCRIPTION, IntPtr.Zero, 0, out zero)).Returns(false);

            // 2. Force 'info.fDelayedAutostart' to be FALSE (Coverage for THIRD branch)
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny, It.IsAny<int>(), out It.Ref<int>.IsAny))
                .Returns(new QueryConfig2DelayedStartDelegate((SafeServiceHandle h, uint lvl, ref SERVICE_DELAYED_AUTO_START_INFO info, int sz, ref int req) =>
                {
                    // Branch Coverage: This ensures 'info.fDelayedAutostart' is false 
                    // so the 'if (ok && info.fDelayedAutostart)' block is skipped.
                    info.fDelayedAutostart = false;
                    return true; // ok = true
                }));

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ServiceStartType.Automatic, result[0].StartupType);
            Assert.Empty(result[0].LogOnAs); // Stayed empty because bytesNeeded was 0
        }

        [Theory]
        [InlineData(ServiceStartMode.Manual, ServiceStartType.Manual)]
        [InlineData(ServiceStartMode.Disabled, ServiceStartType.Disabled)]
        public void GetAllServices_ShouldMapManualAndDisabledStartTypes(ServiceStartMode native, ServiceStartType expected)
        {
            // Arrange
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");
            mockSvc.Setup(s => s.Status).Returns(ServiceControllerStatus.Running);
            mockSvc.Setup(s => s.StartType).Returns(native); // Hits the switch cases

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });

            // 1. Loose matching and factory return for SCM
            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>()))
                .Returns(() => CreateScmHandle(1));

            // 2. REQUIRED: Mock OpenService so PopulateNativeDetails doesn't hit a null handle
            _mockWindowsServiceApi
                .Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Returns(() => CreateServiceHandle(2));

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expected, result[0].StartupType);
        }

        [Fact]
        public void GetAllServices_ShouldSuccessfullyRetrieveServiceUser()
        {
            // Arrange
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null!, null!, It.IsAny<uint>())).Returns(scmHandle);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "TestSvc", It.IsAny<uint>())).Returns(svcHandle);

            // Setup QueryServiceConfig size thresholds
            int structSize = Marshal.SizeOf(typeof(QUERY_SERVICE_CONFIG));
            string expectedUser = "NT AUTHORITY\\NetworkService";

            // Calculate a safe continuous buffer block size (Structure size + String length + null terminator margin)
            int size = structSize + ((expectedUser.Length + 1) * Marshal.SystemDefaultCharSize);

            // First call: Get the required size payload
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, IntPtr.Zero, 0, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigOut((SafeServiceHandle h, IntPtr p, int s, out int req) => req = size))
                .Returns(false);

            // Second call: Fill the structure and append string data within the caller-provided memory block
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, It.Is<IntPtr>(p => p != IntPtr.Zero), size, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigOut((SafeServiceHandle h, IntPtr p, int s, out int req) =>
                {
                    req = size;

                    // Calculate target offset directly inside the tail block to avoid allocating an external HGlobal leak
                    IntPtr stringTargetPtr = IntPtr.Add(p, structSize);

                    // Write character buffer directly inside the contiguous native structure payload block
                    if (Marshal.SystemDefaultCharSize == 1)
                    {
                        byte[] ansiBytes = System.Text.Encoding.Default.GetBytes(expectedUser + "\0");
                        Marshal.Copy(ansiBytes, 0, stringTargetPtr, ansiBytes.Length);
                    }
                    else
                    {
                        char[] unicodeChars = (expectedUser + "\0").ToCharArray();
                        Marshal.Copy(unicodeChars, 0, stringTargetPtr, unicodeChars.Length);
                    }

                    // Map native structure fields back to the internal continuous buffer address space pointers
                    var config = new QUERY_SERVICE_CONFIG { lpServiceStartName = stringTargetPtr };
                    Marshal.StructureToPtr(config, p, false);
                }))
                .Returns(true);

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(result);
            Assert.Equal(expectedUser, result[0].LogOnAs);
        }

        [Fact]
        public void GetAllServices_ShouldSetDelayedAutoStart_WhenFlagIsTrue()
        {
            // Arrange
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");
            mockSvc.Setup(s => s.StartType).Returns(ServiceStartMode.Automatic); // Required to enter delayed check

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scmHandle);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "TestSvc", It.IsAny<uint>())).Returns(svcHandle);

            // Robust simulation of the dual-pass QueryServiceConfig pattern
            int configStructSize = Marshal.SizeOf<QUERY_SERVICE_CONFIG>();
            _mockWindowsServiceApi
                .Setup(x => x.QueryServiceConfig(It.IsAny<SafeServiceHandle>(), It.IsAny<IntPtr>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
                .Returns(new QueryConfigDelegate((SafeServiceHandle h, IntPtr buf, int size, out int bytesNeeded) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        bytesNeeded = configStructSize;
                        return false; // Win32 size-probe behavior
                    }

                    // Zero out the structure memory block so it doesn't contain garbage unmanaged pointers
                    var zeroedConfig = new QUERY_SERVICE_CONFIG
                    {
                        lpServiceStartName = IntPtr.Zero,
                        lpBinaryPathName = IntPtr.Zero,
                        lpDependencies = IntPtr.Zero,
                        lpDisplayName = IntPtr.Zero,
                        lpLoadOrderGroup = IntPtr.Zero
                    };

                    Marshal.StructureToPtr(zeroedConfig, buf, false);
                    bytesNeeded = size;
                    return true;
                }));

            // Robust simulation of the dual-pass QueryServiceConfig2 pattern for descriptions
            int descStructSize = Marshal.SizeOf<SERVICE_DESCRIPTION>();
            _mockWindowsServiceApi.
                Setup(x => x.QueryServiceConfig2(It.IsAny<SafeServiceHandle>(), SERVICE_CONFIG_DESCRIPTION, It.IsAny<IntPtr>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
                .Returns(new QueryConfig2Delegate((SafeServiceHandle h, uint dwInfoLevel, IntPtr buf, int size, ref int bytesNeeded) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        bytesNeeded = descStructSize;
                        return false; // Win32 size-probe behavior
                    }

                    var zeroedDesc = new SERVICE_DESCRIPTION
                    {
                        lpDescription = IntPtr.Zero // Explicitly safe null string pointer
                    };

                    Marshal.StructureToPtr(zeroedDesc, buf, false);
                    bytesNeeded = size;
                    return true;
                }));

            // Mock QueryServiceConfig2 for Delayed Auto-Start Info
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(
                svcHandle,
                SERVICE_CONFIG_DELAYED_AUTO_START_INFO, // SERVICE_CONFIG_DELAYED_AUTO_START_INFO
                ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny,
                It.IsAny<int>(),
                out It.Ref<int>.IsAny))
                 .Returns(new QueryConfig2DelayedStartDelegate((SafeServiceHandle h, uint lvl, ref SERVICE_DELAYED_AUTO_START_INFO info, int sz, ref int req) =>
                 {
                     req = Marshal.SizeOf<SERVICE_DELAYED_AUTO_START_INFO>();
                     info.fDelayedAutostart = true;
                     return true; // ok = true
                 }));

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(result);
            Assert.Equal(ServiceStartType.AutomaticDelayedStart, result[0].StartupType);
        }

        [Fact]
        public void GetAllServices_ShouldRetrieveServiceDescription_WhenBufferIsAllocated()
        {
            // Arrange
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestServiceWithDescription");
            mockSvc.Setup(s => s.Status).Returns(ServiceControllerStatus.Running);
            mockSvc.Setup(s => s.StartType).Returns(ServiceStartMode.Manual);

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });

            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);

            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(() => scmHandle);

            _mockWindowsServiceApi
                .Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), "TestServiceWithDescription", It.IsAny<uint>()))
                .Returns(() => svcHandle);

            const string expectedDescription = "This is a mocked service description.";
            int structSize = Marshal.SizeOf(typeof(SERVICE_DESCRIPTION));
            // Calculate exact space needed for the layout structure plus the null-terminated unicode string array
            int totalNeeded = structSize + ((expectedDescription.Length + 1) * 2);

            // Prevent QueryServiceConfig pass 1 from throwing an AccessViolationException
            int configStructSize = Marshal.SizeOf<QUERY_SERVICE_CONFIG>();
            _mockWindowsServiceApi
                .Setup(x => x.QueryServiceConfig(It.IsAny<SafeServiceHandle>(), It.IsAny<IntPtr>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
                .Returns(new QueryConfigDelegate((SafeServiceHandle h, IntPtr buf, int size, out int bytesNeeded) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        bytesNeeded = configStructSize;
                        return false; // Real Win32 size-probe behavior
                    }

                    var zeroedConfig = new QUERY_SERVICE_CONFIG
                    {
                        lpServiceStartName = IntPtr.Zero,
                        lpBinaryPathName = IntPtr.Zero,
                        lpDependencies = IntPtr.Zero,
                        lpDisplayName = IntPtr.Zero,
                        lpLoadOrderGroup = IntPtr.Zero
                    };

                    Marshal.StructureToPtr(zeroedConfig, buf, false);
                    bytesNeeded = size;
                    return true;
                }));

            // Mock QueryServiceConfig2 (Level 1: Description)
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(
                It.IsAny<SafeServiceHandle>(),
                SERVICE_CONFIG_DESCRIPTION,
                It.IsAny<IntPtr>(),
                It.IsAny<int>(),
                out It.Ref<int>.IsAny))
                .Returns(new QueryConfig2Delegate((SafeServiceHandle h, uint lvl, IntPtr buf, int size, ref int req) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        req = totalNeeded;
                        return false; // Real Win32 size-probe behavior
                    }

                    // Safe contiguous unmanaged writing without leaking AllocHGlobal blocks
                    // Append the actual string array payload directly after the structure boundary block
                    IntPtr stringTargetPtr = IntPtr.Add(buf, structSize);
                    Marshal.Copy(expectedDescription.ToCharArray(), 0, stringTargetPtr, expectedDescription.Length);
                    // Add the null terminator character manually at the end of the target span
                    Marshal.WriteInt16(IntPtr.Add(stringTargetPtr, expectedDescription.Length * 2), 0);

                    var descStruct = new SERVICE_DESCRIPTION
                    {
                        lpDescription = stringTargetPtr // Points safely inside the allocated 'buf' block
                    };

                    Marshal.StructureToPtr(descStruct, buf, false);
                    req = size;
                    return true;
                }));

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(result);
            Assert.Equal(expectedDescription, result[0].Description);

            // Verify cleanup
            Assert.True(svcHandle.IsClosed, "Service handle was not disposed.");
            Assert.True(scmHandle.IsClosed, "SCM handle was not disposed.");
        }

        [Fact]
        public void GetAllServices_ShouldHandleNullDescriptionPointer_ByReturningEmptyString()
        {
            // Arrange
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("NoDescService");
            mockSvc.Setup(s => s.Status).Returns(ServiceControllerStatus.Running);
            mockSvc.Setup(s => s.StartType).Returns(ServiceStartMode.Manual);

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scmHandle);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "NoDescService", It.IsAny<uint>())).Returns(svcHandle);

            // Robust simulation of the dual-pass QueryServiceConfig pattern
            int configStructSize = Marshal.SizeOf<QUERY_SERVICE_CONFIG>();
            _mockWindowsServiceApi
                .Setup(x => x.QueryServiceConfig(It.IsAny<SafeServiceHandle>(), It.IsAny<IntPtr>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
                .Returns(new QueryConfigDelegate((SafeServiceHandle h, IntPtr buf, int size, out int bytesNeeded) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        bytesNeeded = configStructSize;
                        return false; // Win32 behavior: return false on size probes
                    }

                    // Zero out the structure memory block so it doesn't contain garbage unmanaged data pointers
                    var zeroedConfig = new QUERY_SERVICE_CONFIG
                    {
                        lpServiceStartName = IntPtr.Zero, // Explicitly safe null string pointer
                        lpBinaryPathName = IntPtr.Zero,
                        lpDependencies = IntPtr.Zero,
                        lpDisplayName = IntPtr.Zero,
                        lpLoadOrderGroup = IntPtr.Zero
                    };

                    Marshal.StructureToPtr(zeroedConfig, buf, false);
                    bytesNeeded = size;
                    return true;
                }));

            int structSize = Marshal.SizeOf(typeof(SERVICE_DESCRIPTION));

            // Mock QueryServiceConfig2 to return a structure with a NULL pointer for lpDescription
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(
                svcHandle,
                SERVICE_CONFIG_DESCRIPTION,
                It.IsAny<IntPtr>(),
                It.IsAny<int>(),
                out It.Ref<int>.IsAny))
                .Returns(new QueryConfig2Delegate((SafeServiceHandle h, uint lvl, IntPtr buf, int size, ref int req) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        req = structSize;
                        return false;
                    }

                    var descStruct = new SERVICE_DESCRIPTION
                    {
                        lpDescription = IntPtr.Zero // Triggers the '?? string.Empty' branch
                    };

                    Marshal.StructureToPtr(descStruct, buf, false);
                    req = size;
                    return true;
                }));

            // Act
            var result = _serviceManager.GetAllServices(TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(result);
            Assert.Equal(string.Empty, result[0].Description);
            Assert.NotNull(result[0].Description);
        }

        private delegate bool QueryConfigDelegate(
            SafeServiceHandle h,
            IntPtr buf,
            int size,
            out int bytesNeeded);

        private delegate bool QueryConfig2DelayedStartDelegate(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            ref SERVICE_DELAYED_AUTO_START_INFO lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        // Delegate required for Moq to handle 'ref int' in QueryServiceConfig2
        private delegate bool QueryConfig2Delegate(SafeServiceHandle hService, uint dwInfoLevel, IntPtr lpBuffer, int cbBufSize, ref int pcbBytesNeeded);

        // Delegate needed for Moq to handle 'out' parameters in Callback
        private delegate void QueryConfigOut(SafeServiceHandle handle, IntPtr buffer, int size, out int required);

        #endregion

        #region GetDependencies

        [Fact]
        public void GetDependencies_ShouldReturnDependencies()
        {
            // Arrange
            var deps = new ServiceDependencyNode("ServiceName", "ServiceDisplayName");

            _mockController
                .Setup(c => c.GetDependencies(It.IsAny<CancellationToken>()))
                .Returns(deps);

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "TestService", DisplayName = "TestServiceDisplayName" }
                 });

            // Act
            var result = _serviceManager.GetDependencies("TestService", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(deps, result);

            _mockController.Verify(c => c.GetDependencies(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetDependencies_ShouldReturnNull()
        {
            // Arrange
            var deps = new ServiceDependencyNode("ServiceName", "ServiceDisplayName");

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(Array.Empty<WindowsServiceInfo>());

            // Act
            var result = _serviceManager.GetDependencies("TestService", TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void GetDependencies_InvalidServiceName_ShouldThrowArgumentException(string serviceName)
        {
            // Assert
            Assert.Throws<ArgumentException>(() => _serviceManager.GetDependencies(serviceName, TestContext.Current.CancellationToken));
        }

        [Fact]
        public void GetDependencies_ControllerThrows_ShouldPropagateException()
        {
            // Arrange
            _mockController
                .Setup(c => c.GetDependencies(It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Boom!"));

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "TestService", DisplayName = "TestServiceDisplayName" }
                 });

            // Assert
            Assert.Throws<InvalidOperationException>(() => _serviceManager.GetDependencies("TestService", TestContext.Current.CancellationToken));

            _mockController.Verify(c => c.GetDependencies(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetDependencies_ShouldDisposeController()
        {
            // Arrange
            _mockController
                .Setup(c => c.GetDependencies(It.IsAny<CancellationToken>()))
                .Returns(new ServiceDependencyNode("S", "D"));

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "TestService", DisplayName = "TestServiceDisplayName" }
                 });

            // Act
            _serviceManager.GetDependencies("TestService", TestContext.Current.CancellationToken);

            // Assert
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion

        #region SafeHandle Helper Factory Methods

        private static SafeScmHandle CreateScmHandle(int value = 1)
        {
            var handle = (SafeScmHandle)Activator.CreateInstance(typeof(SafeScmHandle), true)!;

            // If the test explicitly passes 0, keep it as IntPtr.Zero so handle.IsInvalid evaluates to true.
            // Otherwise, allocate valid unmanaged space to prevent native Access Violations (0xC0000005) on Dispose.
            IntPtr ptrToInject = (value == 0) ? IntPtr.Zero : Marshal.AllocHGlobal(64);
            // TestReflection automatically ascends the inheritance chain to locate and invoke 'SetHandle' on SafeHandle
            TestReflection.InvokeNonPublic(handle, "SetHandle", ptrToInject);

            return handle;
        }

        private static SafeServiceHandle CreateServiceHandle(int value = 1)
        {
            var handle = (SafeServiceHandle)Activator.CreateInstance(typeof(SafeServiceHandle), true)!;

            // Same rule for service handles: 0 translates directly to an invalid IntPtr.Zero handle wrapper.
            IntPtr ptrToInject = (value == 0) ? IntPtr.Zero : Marshal.AllocHGlobal(64);

            // TestReflection automatically ascends the inheritance chain to locate and invoke 'SetHandle' on SafeHandle
            TestReflection.InvokeNonPublic(handle, "SetHandle", ptrToInject);

            return handle;
        }

        #endregion
    }
}

#pragma warning restore CS8625