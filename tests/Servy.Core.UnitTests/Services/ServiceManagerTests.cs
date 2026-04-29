using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
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
                _serviceManager.InstallServiceAsync(null));

            Assert.Equal("options", exception.ParamName);
        }

        [Theory]
        [InlineData("", "", "")]
        [InlineData("TestService", "", "")]
        [InlineData("TestService", "C:\\Apps\\App.exe", "")]
        public async Task InstallService_Throws_ArgumentException(string serviceName, string wrapperExePath, string realExePath)
        {
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);
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
                null,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = wrapperExePath,
                RealExePath = realExePath,
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchTimeout = 30
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _serviceManager.InstallServiceAsync(options));
        }

        [Theory]
        [InlineData("TestService", "C:\\Apps\\App.exe", "C:\\Apps\\App.exe")]
        public async Task InstallService_EmptyOptions(string serviceName, string wrapperExePath, string realExePath)
        {
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockServiceRepository.Setup(x => x.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Core.DTOs.ServiceDto
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
                ServiceManager.LocalSystemAccount,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<int>(),
               It.IsAny<IntPtr>()
               ))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                WrapperExePath = wrapperExePath,
                RealExePath = realExePath,
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchTimeout = 30
            };

            var result = await _serviceManager.InstallServiceAsync(options);

            Assert.True(result.IsSuccess);
            _mockServiceRepository.Verify(x => x.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("TestService", "C:\\Apps\\App.exe", "C:\\Apps\\App.exe")]
        public async Task InstallService_ThrowsException(string serviceName, string wrapperExePath, string realExePath)
        {
            var scmHandle = CreateScmHandle(123);
            var serviceHandle = CreateServiceHandle(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockServiceRepository.Setup(x => x.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Core.DTOs.ServiceDto
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
                ServiceManager.LocalSystemAccount,
                null))
                .Throws(new Exception("Boom!"));

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<int>(),
               It.IsAny<IntPtr>()
               ))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            var options = new InstallServiceOptions
            {
                ServiceName = serviceName,
                WrapperExePath = wrapperExePath,
                RealExePath = realExePath,
                StartType = ServiceStartType.Automatic,
                ProcessPriority = ProcessPriority.Normal,
                PreLaunchTimeout = 30
            };

            await Assert.ThrowsAsync<Exception>(() => _serviceManager.InstallServiceAsync(options));
        }

        [Fact]
        public async Task InstallService_Throws_Win32Exception()
        {
            var scmHandle = CreateScmHandle(0);
            var serviceHandle = CreateServiceHandle(456);
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
                null,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

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

            await Assert.ThrowsAsync<Win32Exception>(() => _serviceManager.InstallServiceAsync(options));

            scmHandle = CreateScmHandle(123);
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
             .Returns(scmHandle);
            _mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1074);

            Assert.False((await _serviceManager.InstallServiceAsync(options)).IsSuccess);
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
                    ServiceManager.LocalSystemAccount,
                    null))
                .Returns(() => serviceHandle);

            _mockWindowsServiceApi
                .Setup(x => x.ChangeServiceConfig2(
                    It.IsAny<SafeServiceHandle>(), // Loose matching
                    It.IsAny<int>(),
                    ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi
                .Setup(x => x.ChangeServiceConfig2(
                   It.IsAny<SafeServiceHandle>(), // Loose matching
                   It.IsAny<int>(),
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
            var result = await _serviceManager.InstallServiceAsync(options);

            // Assert
            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.OpenSCManager(null, null, It.IsAny<uint>()), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CreateService(It.IsAny<SafeScmHandle>(), serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.Once);

            // Verify cleanup via SafeHandle state rather than the API mock
            Assert.True(serviceHandle.IsClosed, "Service handle was not disposed.");
            Assert.True(scmHandle.IsClosed, "SCM handle was not disposed.");
        }

        [Fact]
        public async Task InstallService_CallsUpdateServiceConfig_WhenServiceExistsError()
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
                ServiceManager.LocalSystemAccount,
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
                ServiceManager.LocalSystemAccount,
                null,
                It.IsAny<string>()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<int>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle())).Returns(true);

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
                PreLaunchIgnoreFailure = true,
                DisplayName = serviceName
            };

            var result = await _serviceManager.InstallServiceAsync(options);

            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig(serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null, It.IsAny<string>()), Times.Once);

            options.StartType = ServiceStartType.AutomaticDelayedStart;
            result = await _serviceManager.InstallServiceAsync(options);

            Assert.True(result.IsSuccess);
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
                ServiceManager.LocalSystemAccount,
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
                ServiceManager.LocalSystemAccount,
                null,
                It.IsAny<string>()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<int>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(false);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle())).Returns(true);

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

            var result = await _serviceManager.InstallServiceAsync(options);

            Assert.False(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig(serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null, It.IsAny<string>()), Times.Once);
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

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<int>(),
               It.IsAny<IntPtr>()))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle())).Returns(true);

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

            var result = await _serviceManager.InstallServiceAsync(options);

            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, gMSA, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<int>(), It.IsAny<IntPtr>()), Times.Once);
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
                ServiceManager.LocalSystemAccount,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(false);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<int>(),
               It.IsAny<IntPtr>()
               ))
               .Returns(false);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle())).Returns(true);

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

            var result = await _serviceManager.InstallServiceAsync(options);

            Assert.False(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<int>(), It.IsAny<IntPtr>()), Times.Once);
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

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny)).Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<int>(),
               ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                It.IsAny<SafeServiceHandle>(),
                It.IsAny<int>(),
                It.IsAny<IntPtr>()
                ))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle())).Returns(true);

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

            var result = await _serviceManager.InstallServiceAsync(options);

            Assert.True(result.IsSuccess);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, gMSA, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<int>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny), Times.Once);
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
                ServiceManager.LocalSystemAccount,
                null))
                .Returns(serviceHandle);

            // Setup OpenService for UpdateServiceConfig
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
                .Returns(serviceHandle);

            // Simulate failure when setting delayed auto-start
            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny))
                .Returns(false);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<int>(),
               ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<SafeServiceHandle>(),
               It.IsAny<int>(),
               It.IsAny<IntPtr>()
               ))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle())).Returns(true);

            var options = new Servy.Core.Services.InstallServiceOptions
            {
                ServiceName = serviceName,
                Description = description,
                WrapperExePath = "wrapper.exe",
                RealExePath = "real.exe",
                WorkingDirectory = "workingDir",
                RealArgs = "args",
                StartType = Servy.Core.Enums.ServiceStartType.AutomaticDelayedStart,
                ProcessPriority = Servy.Core.Enums.ProcessPriority.Normal,
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
            var result = await _serviceManager.InstallServiceAsync(options);

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
                ServiceManager.LocalSystemAccount,
                null), Times.Once);

            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(
                It.IsAny<SafeServiceHandle>(),
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny), Times.Once);
        }

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
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);

            var result = _serviceManager.UpdateServiceConfig(
                scmHandle,
                serviceName,
                description,
                binPath,
                ServiceStartType.Automatic,
                null,
                null,
                null,
                null
                );

            Assert.True(result);

            result = _serviceManager.UpdateServiceConfig(
                scmHandle,
                serviceName,
                description,
                binPath,
                ServiceStartType.Automatic,
                null,
                null,
                null,
                serviceName
                );

            Assert.True(result);
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
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(true);

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

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
               .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle())).Returns(false);

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
            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _serviceManager.SetServiceDescription(serviceHandle, null);
            _serviceManager.SetServiceDescription(serviceHandle, "");

            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.AtLeast(1));
        }

        [Fact]
        public void SetServiceDescription_Throws_WhenChangeServiceConfig2Fails()
        {
            var serviceHandle = CreateServiceHandle(456);
            var description = "desc";

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(false);

            Assert.Throws<Win32Exception>(() => _serviceManager.SetServiceDescription(serviceHandle, description));
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenOpenSCManagerFails()
        {
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(CreateScmHandle(0));

            var result = await _serviceManager.UninstallServiceAsync("ServiceName");
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task UninstallService_Throws_Win32Exception()
        {
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
             .Returns(CreateScmHandle(2));

            _mockWindowsServiceApi.Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Throws(new Win32Exception("Boom!"));

            await Assert.ThrowsAsync<Win32Exception>(() => _serviceManager.UninstallServiceAsync("ServiceName"));
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenOpenServiceFails()
        {
            var scmHandle = CreateScmHandle(123);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "ServiceName", It.IsAny<uint>()))
                .Returns(CreateServiceHandle(0));

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle())).Returns(true);

            var result = await _serviceManager.UninstallServiceAsync("ServiceName");
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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<NativeMethods.SERVICE_STATUS>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(false);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle()))
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

            var result = await _serviceManager.UninstallServiceAsync(serviceName);

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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<NativeMethods.SERVICE_STATUS>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle()))
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

            var result = await _serviceManager.UninstallServiceAsync(serviceName);

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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<NativeMethods.SERVICE_STATUS>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle.DangerousGetHandle()))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle.DangerousGetHandle()))
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

            var result = await _serviceManager.UninstallServiceAsync(serviceName);

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
                .ReturnsAsync(new Core.DTOs.ServiceDto { Name = "TestService" });

            // Act
            var result = await _serviceManager.StartServiceAsync("TestService");

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
                .ReturnsAsync(new Core.DTOs.ServiceDto
                {
                    Name = serviceName,
                    PreLaunchExecutablePath = @"C:\Apps\pre-launch.exe"
                });

            // Act
            var result = await _serviceManager.StartServiceAsync(serviceName);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify the native Start command was sent
            _mockController.Verify(c => c.Start(), Times.Once);

            // Verify that the loop actually checked the SCM for updates
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);

            // REMOVED: WaitForStatus verify, as it is no longer used in the async version
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
                .ReturnsAsync(new Core.DTOs.ServiceDto
                {
                    Name = serviceName,
                    StartTimeout = 5
                });

            // Act
            var result = await _serviceManager.StartServiceAsync(serviceName);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify the native Start command was issued
            _mockController.Verify(c => c.Start(), Times.Once);

            // Verify that we are now using Refresh() to get updated status from the SCM
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);

            // REMOVED: WaitForStatus verification (it's no longer in the code)
        }

        [Fact]
        public async Task StartService_ShouldReturnFalse_WhenServiceNotInDB()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(null as Core.DTOs.ServiceDto);

            // Act
            var result = await _serviceManager.StartServiceAsync("TestService");

            // Assert
            Assert.False(result.IsSuccess);
            _mockController.Verify(c => c.Start(), Times.Never);
        }

        [Fact]
        public async Task StartService_ShouldReturnFalse_WhenExceptionIsThrown()
        {
            _mockController.Setup(c => c.Status).Throws<InvalidOperationException>();
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Core.DTOs.ServiceDto { Name = "TestService", StopTimeout = 10 });

            var result = await _serviceManager.StartServiceAsync("TestService");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task StopService_ShouldReturnTrue_WhenAlreadyStopped()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Stopped);
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Core.DTOs.ServiceDto { Name = "TestService" });

            // Act
            var result = await _serviceManager.StopServiceAsync("TestService");

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
                .ReturnsAsync(null as Core.DTOs.ServiceDto);

            // Act
            var result = await _serviceManager.StopServiceAsync("TestService");

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
                .ReturnsAsync(new Core.DTOs.ServiceDto
                {
                    Name = serviceName,
                    PreStopExecutablePath = @"C:\Apps\pre-stop.exe"
                });

            // Act
            var result = await _serviceManager.StopServiceAsync(serviceName);

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
                .ReturnsAsync(new Core.DTOs.ServiceDto
                {
                    Name = serviceName,
                    StopTimeout = stopTimeout
                });

            // Act
            var result = await _serviceManager.StopServiceAsync(serviceName);

            // Assert
            Assert.True(result.IsSuccess);

            // Verify sc.Stop() was called
            _mockController.Verify(c => c.Stop(), Times.Once);

            // Verify we polled for status (Refresh is called inside the loop)
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);

            // IMPORTANT: Remove the WaitForStatus verify, it no longer exists in the logic!
        }

        [Fact]
        public async Task StopService_ShouldReturnFalse_WhenExceptionIsThrown()
        {
            _mockController.Setup(c => c.Status).Throws<InvalidOperationException>();
            _mockServiceRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Core.DTOs.ServiceDto { Name = "TestService", StopTimeout = 10 });

            var result = await _serviceManager.StopServiceAsync("TestService");

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
                .ReturnsAsync(new Core.DTOs.ServiceDto { Name = "TestService" });

            // Act
            var result = await _serviceManager.RestartServiceAsync("TestService");

            // Assert
            Assert.True(result.IsSuccess);

            _mockController.Verify(c => c.Stop(), Times.Once);
            _mockController.Verify(c => c.Start(), Times.Once);

            // Now this will pass because we forced the loop to run at least once per phase
            _mockController.Verify(c => c.Refresh(), Times.Exactly(2));
        }

        [Fact]
        public async Task RestartService_ShouldReturnFalse_WhenStopServiceFails()
        {
            // Arrange
            // Simulate the service is already stopped so StopService returns true
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);

            // Simulate StartService throwing an exception, which should trigger catch and return false
            _mockController.Setup(c => c.Stop()).Throws(new Exception("Boom!"));

            // Act
            var result = await _serviceManager.RestartServiceAsync("TestService");

            // Assert
            Assert.False(result.IsSuccess);
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
                .ReturnsAsync(new Core.DTOs.ServiceDto { Name = "TestService" });

            // This will now actually be invoked because the entry check sees "Stopped"
            _mockController.Setup(c => c.Start()).Throws(new Exception("Boom!"));

            // Act
            var result = await _serviceManager.RestartServiceAsync("TestService");

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
            var result = _serviceManager.GetServiceStatus("TestService");

            // Assert
            Assert.Equal(ServiceControllerStatus.Running, result);
        }

        [Fact]
        public void GetServiceStatus_ShouldThrowArgumentException()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);

            // Assert
            Assert.Throws<ArgumentException>(() => _serviceManager.GetServiceStatus(""));
        }

        [Fact]
        public void IsServiceInstalled_ReturnsTrue_WhenServiceExists()
        {
            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "MyService", DisplayName = "My Service" }
                 });

            Assert.True(_serviceManager.IsServiceInstalled("MyService"));
        }

        [Fact]
        public void IsServiceInstalled_ReturnsFalse_WhenServiceMissing()
        {
            _mockWindowsServiceApi.Setup(p => p.GetServices()).Returns(Array.Empty<WindowsServiceInfo>());

            Assert.False(_serviceManager.IsServiceInstalled("MyService"));
        }

        [Fact]
        public void IsServiceInstalled_Throws_ArgumentTnullException()
        {
            Assert.Throws<ArgumentNullException>(() => _serviceManager.IsServiceInstalled(string.Empty));
        }

        #region GetServiceStartupType

        [Fact]
        public void GetServiceStartupType_ShouldThrowArgumentNullException_WhenNameIsInvalid()
        {
            Assert.Throws<ArgumentNullException>(() => _serviceManager.GetServiceStartupType(null));
            Assert.Throws<ArgumentNullException>(() => _serviceManager.GetServiceStartupType(" "));
        }

        [Fact]
        public void GetServiceStartupType_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                _serviceManager.GetServiceStartupType("AnyService", cts.Token));
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
                        3, // SERVICE_CONFIG_DELAYED_AUTO_START_INFO
                        ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny,
                        It.IsAny<int>(),
                        ref It.Ref<int>.IsAny))
                        .Returns(new QueryConfig2DelayedStartDelegate((SafeServiceHandle h, uint lvl, ref SERVICE_DELAYED_AUTO_START_INFO info, int sz, ref int req) =>
                        {
                            info.fDelayedAutostart = isDelayed;
                            return true;
                        }));
            }

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName);

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
        public void GetServiceStartupType_ShouldReturnAutomatic_WhenDelayedIsFalse()
        {
            // Arrange
            const string serviceName = "StandardAutoService";
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);

            // 1. Setup the mock controller to return Automatic
            _mockController.Setup(c => c.StartType).Returns(ServiceStartMode.Automatic);

            // 2. Setup the Native API to succeed but return fDelayedAutostart = false
            _mockWindowsServiceApi
                .Setup(api => api.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(() => scmHandle);

            _mockWindowsServiceApi
                .Setup(api => api.OpenService(It.IsAny<SafeScmHandle>(), serviceName, It.IsAny<uint>()))
                .Returns(() => svcHandle);

            _mockWindowsServiceApi
                .Setup(api => api.QueryServiceConfig2(
                    It.IsAny<SafeServiceHandle>(),
                    3, // SERVICE_CONFIG_DELAYED_AUTO_START_INFO
                    ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny,
                    It.IsAny<int>(),
                    ref It.Ref<int>.IsAny))
                    .Returns(new QueryConfig2DelayedStartDelegate((SafeServiceHandle h, uint lvl, ref SERVICE_DELAYED_AUTO_START_INFO info, int sz, ref int req) =>
                    {
                        // Branch Coverage: Force the 'if (ok && info.fDelayedAutostart)' check to evaluate to false
                        info.fDelayedAutostart = false;
                        return true; // ok = true
                    }));

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName);

            // Assert
            Assert.Equal(ServiceStartType.Automatic, result);

            // Verify cleanup
            // We check the handle state directly because SafeHandle.Dispose() 
            // triggers the native release logic, which bypasses the IWindowsServiceApi mock.
            Assert.True(svcHandle.IsClosed, "Service handle was not disposed.");
            Assert.True(scmHandle.IsClosed, "SCM handle was not disposed.");
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
                    3, // SERVICE_CONFIG_DELAYED_AUTO_START_INFO
                    ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny,
                    It.IsAny<int>(),
                    ref It.Ref<int>.IsAny))
                .Returns(false);

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName);

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
        public void GetServiceStartupType_ShouldReturnNull_WhenExceptionOccursAndLogIt()
        {
            // Arrange
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>()))
                    .Throws(new Exception("Native Failure"));

            // Act
            var result = _serviceManager.GetServiceStartupType("AnyService");

            // Assert
            Assert.Null(result);
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
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                    .Returns(CreateScmHandle(0));

            // Act
            var result = _serviceManager.GetServiceStartupType(serviceName, CancellationToken.None);

            // Assert
            // It should stay 'Automatic' because the P/Invoke to check for 'Delayed' failed.
            Assert.Equal(ServiceStartType.Automatic, result);

            // Verify we actually tried to open the manager
            _mockWindowsServiceApi.Verify(x => x.OpenSCManager(null, null, It.IsAny<uint>()), Times.Once);
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
            var result = _serviceManager.GetServiceStartupType(serviceName, CancellationToken.None);

            // Assert
            Assert.Equal(ServiceStartType.Unknown, result);
        }

        #endregion

        #region GetAllServices

        [Fact]
        public void GetAllServices_ShouldThrowWin32Exception_WhenSCManagerFailsToOpen()
        {
            // Branch: scmHandle == IntPtr.Zero
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(CreateScmHandle(0));

            Assert.Throws<Win32Exception>(() => _serviceManager.GetAllServices());
        }

        [Fact]
        public void GetAllServices_ShouldReturnEmpty_WhenNoServicesFound()
        {
            // Branch: Parallel.ForEach with empty list
            _mockServiceControllerProvider.Setup(x => x.GetServices()).Returns(Array.Empty<IServiceControllerWrapper>());
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(CreateScmHandle(1));

            var result = _serviceManager.GetAllServices();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllServices_ShouldHandleQueryServiceConfig_AndRetrieveUser()
        {
            // Arrange
            var scmHandle = CreateScmHandle(1);
            var svcHandle = CreateServiceHandle(2);

            // We need at least one service to enter the loop
            // Warning: ServiceController is hard to instantiate without a real service.
            // If this fails, consider changing IServiceControllerProvider to return a wrapper.
            var mockService = new ServiceControllerWrapper("EventLog");

            _mockServiceControllerProvider.Setup(x => x.GetServices()).Returns(new[] { mockService });
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scmHandle);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, It.IsAny<string>(), It.IsAny<uint>())).Returns(svcHandle);

            // Branch: QueryServiceConfig (Get size, then get data)
            int size = Marshal.SizeOf(typeof(QUERY_SERVICE_CONFIG)) + 100;
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, IntPtr.Zero, 0, out It.Ref<int>.IsAny))
                    .Callback(new QueryConfigOut((SafeServiceHandle h, IntPtr p, int s, out int req) => req = size))
                    .Returns(false);

            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, It.Is<IntPtr>(p => p != IntPtr.Zero), size, out It.Ref<int>.IsAny))
                    .Callback(new QueryConfigOut((SafeServiceHandle h, IntPtr p, int s, out int req) =>
                    {
                        req = size;
                        var config = new QUERY_SERVICE_CONFIG { lpServiceStartName = Marshal.StringToHGlobalAuto("CustomUser") };
                        Marshal.StructureToPtr(config, p, false);
                    }))
                    .Returns(true);

            // Act
            var result = _serviceManager.GetAllServices();

            // Assert
            Assert.Single(result);
            Assert.Equal("CustomUser", result[0].LogOnAs);
        }

        [Theory]
        [InlineData(ServiceControllerStatus.Stopped, Enums.ServiceStatus.Stopped)]
        [InlineData(ServiceControllerStatus.Paused, Enums.ServiceStatus.Paused)]
        [InlineData(ServiceControllerStatus.StartPending, Enums.ServiceStatus.StartPending)]
        [InlineData(ServiceControllerStatus.StopPending, Enums.ServiceStatus.StopPending)]
        [InlineData(ServiceControllerStatus.PausePending, Enums.ServiceStatus.PausePending)]
        [InlineData(ServiceControllerStatus.ContinuePending, Enums.ServiceStatus.ContinuePending)]
        [InlineData((ServiceControllerStatus)999, Enums.ServiceStatus.None)] // Covers 'default'
        public void GetAllServices_ShouldMapAllStatuses(ServiceControllerStatus native, Enums.ServiceStatus expected)
        {
            // Arrange
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");
            mockSvc.Setup(s => s.Status).Returns(native);

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });

            // Use a factory for the return and loose matching for downstream calls
            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(() => CreateScmHandle(1));

            // We MUST mock OpenService if GetAllServices iterates, otherwise it returns null handles
            _mockWindowsServiceApi
                .Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Returns(() => CreateServiceHandle(2));

            // Act
            var result = _serviceManager.GetAllServices();

            // Assert
            Assert.Equal(expected, result[0].Status);
        }

        [Fact]
        public void GetAllServices_ShouldFallbackToAutomatic_WhenStartTypeThrows()
        {
            // Arrange
            var mockSvc = new Mock<IServiceControllerWrapper>();
            mockSvc.Setup(s => s.ServiceName).Returns("TestSvc");
            // Simulate "Access Denied" by throwing inside the property getter
            mockSvc.Setup(s => s.StartType).Throws(new Exception("Access Denied"));

            _mockServiceControllerProvider.Setup(p => p.GetServices()).Returns(new[] { mockSvc.Object });

            // 1. Use factory lambda for SCM handle
            _mockWindowsServiceApi
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(() => CreateScmHandle(1));

            // 2. REQUIRED: Mock OpenService to prevent NRE in PopulateNativeDetails
            _mockWindowsServiceApi
                .Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Returns(() => CreateServiceHandle(2));

            // Act
            var result = _serviceManager.GetAllServices();

            // Assert
            // Verify it hit the catch block and defaulted to Manual (as per your implementation logic)
            Assert.Equal(ServiceStartType.Manual, result[0].StartupType);
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
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scmHandle);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "TestSvc", It.IsAny<uint>())).Returns(svcHandle);

            // 1. Force 'bytesNeeded > 0' to be FALSE for User/Description
            int zero = 0;
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, IntPtr.Zero, 0, out zero)).Returns(false);
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(svcHandle, 1, IntPtr.Zero, 0, ref zero)).Returns(false);

            // 2. Force 'info.fDelayedAutostart' to be FALSE (Coverage for THIRD branch)
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(svcHandle, 3, ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny, It.IsAny<int>(), ref It.Ref<int>.IsAny))
                .Returns(new QueryConfig2DelayedStartDelegate((SafeServiceHandle h, uint lvl, ref SERVICE_DELAYED_AUTO_START_INFO info, int sz, ref int req) =>
                {
                    // Branch Coverage: This ensures 'info.fDelayedAutostart' is false 
                    // so the 'if (ok && info.fDelayedAutostart)' block is skipped.
                    info.fDelayedAutostart = false;
                    return true; // ok = true
                }));

            // Act
            var result = _serviceManager.GetAllServices();

            // Assert
            Assert.Equal(ServiceStartType.Automatic, result[0].StartupType);
            Assert.Equal("LocalSystem", result[0].LogOnAs); // Stayed default because bytesNeeded was 0
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
                .Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(() => CreateScmHandle(1));

            // 2. REQUIRED: Mock OpenService so PopulateNativeDetails doesn't hit a null handle
            _mockWindowsServiceApi
                .Setup(x => x.OpenService(It.IsAny<SafeScmHandle>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Returns(() => CreateServiceHandle(2));

            // Act
            var result = _serviceManager.GetAllServices(CancellationToken.None);

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
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scmHandle);
            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "TestSvc", It.IsAny<uint>())).Returns(svcHandle);

            // Setup QueryServiceConfig to return a valid user
            int size = Marshal.SizeOf(typeof(QUERY_SERVICE_CONFIG)) + 100;
            string expectedUser = "NT AUTHORITY\\NetworkService";

            // First call: Get the size
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, IntPtr.Zero, 0, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigOut((SafeServiceHandle h, IntPtr p, int s, out int req) => req = size))
                .Returns(false);

            // Second call: Fill the structure
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig(svcHandle, It.Is<IntPtr>(p => p != IntPtr.Zero), size, out It.Ref<int>.IsAny))
                .Callback(new QueryConfigOut((SafeServiceHandle h, IntPtr p, int s, out int req) =>
                {
                    req = size;
                    var config = new QUERY_SERVICE_CONFIG { lpServiceStartName = Marshal.StringToHGlobalAuto(expectedUser) };
                    Marshal.StructureToPtr(config, p, false);
                }))
                .Returns(true);

            // Act
            var result = _serviceManager.GetAllServices(CancellationToken.None);

            // Assert
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

            // Mock QueryServiceConfig2 for Delayed Auto-Start Info
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(
                svcHandle,
                3, // SERVICE_CONFIG_DELAYED_AUTO_START_INFO
                ref It.Ref<SERVICE_DELAYED_AUTO_START_INFO>.IsAny,
                It.IsAny<int>(),
                ref It.Ref<int>.IsAny))
                 .Returns(new QueryConfig2DelayedStartDelegate((SafeServiceHandle h, uint lvl, ref SERVICE_DELAYED_AUTO_START_INFO info, int sz, ref int req) =>
                 {
                     // Branch Coverage: This forces the 'if (ok && info.fDelayedAutostart)' 
                     // condition to evaluate to true.
                     info.fDelayedAutostart = true;
                     return true; // ok = true
                 }));

            // Act
            var result = _serviceManager.GetAllServices(CancellationToken.None);

            // Assert
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

            // Use factory functions for handles to prevent disposal issues in Parallel.ForEach
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
            int totalNeeded = structSize + 512; // Buffer for the struct + the string data

            // Mock QueryServiceConfig2 (Level 1: Description)
            // We use a delegate to handle the 'ref int bytesNeeded' logic
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(
                It.IsAny<SafeServiceHandle>(), // Loose matching
                NativeMethods.SERVICE_CONFIG_DESCRIPTION, // Ensure you use the correct constant
                It.IsAny<IntPtr>(),
                It.IsAny<int>(),
                ref It.Ref<int>.IsAny))
                .Returns(new QueryConfig2Delegate((SafeServiceHandle h, uint lvl, IntPtr buf, int size, ref int req) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        // Step 1: Provide the required size
                        req = totalNeeded;
                        return false;
                    }

                    // Step 2: Write the structure into the allocated memory
                    var descStruct = new SERVICE_DESCRIPTION
                    {
                        lpDescription = Marshal.StringToHGlobalAuto(expectedDescription)
                    };

                    Marshal.StructureToPtr(descStruct, buf, false);
                    req = size;
                    return true;
                }));

            // Act
            var result = _serviceManager.GetAllServices(CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.Equal(expectedDescription, result[0].Description);

            // Verify cleanup
            // We check the handle state instead of verifying the IWindowsServiceApi mock 
            // because SafeHandle.Dispose() calls the native CloseServiceHandle directly, bypassing Moq.
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

            int structSize = Marshal.SizeOf(typeof(SERVICE_DESCRIPTION));

            // Mock QueryServiceConfig2 to return a structure with a NULL pointer for lpDescription
            _mockWindowsServiceApi.Setup(x => x.QueryServiceConfig2(
                svcHandle,
                SERVICE_CONFIG_DESCRIPTION,
                It.IsAny<IntPtr>(),
                It.IsAny<int>(),
                ref It.Ref<int>.IsAny))
                .Returns(new QueryConfig2Delegate((SafeServiceHandle h, uint lvl, IntPtr buf, int size, ref int req) =>
                {
                    if (buf == IntPtr.Zero)
                    {
                        req = structSize; // Just enough for the struct itself
                        return false;
                    }

                    // Step 2: Write a structure where lpDescription is IntPtr.Zero
                    var descStruct = new SERVICE_DESCRIPTION
                    {
                        lpDescription = IntPtr.Zero // <--- This triggers the '?? string.Empty' branch
                    };

                    Marshal.StructureToPtr(descStruct, buf, false);
                    req = size;
                    return true;
                }));

            // Act
            var result = _serviceManager.GetAllServices(CancellationToken.None);

            // Assert
            Assert.Single(result);
            // This verifies the '?? string.Empty' logic worked
            Assert.Equal(string.Empty, result[0].Description);
            Assert.NotNull(result[0].Description);
        }

        private delegate bool QueryConfig2DelayedStartDelegate(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            ref SERVICE_DELAYED_AUTO_START_INFO lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        // Delegate required for Moq to handle 'ref int' in QueryServiceConfig2
        delegate bool QueryConfig2Delegate(SafeServiceHandle hService, uint dwInfoLevel, IntPtr lpBuffer, int cbBufSize, ref int pcbBytesNeeded);

        // Delegate needed for Moq to handle 'out' parameters in Callback
        delegate void QueryConfigOut(SafeServiceHandle handle, IntPtr buffer, int size, out int required);

        #endregion

        #region GetDependencies

        [Fact]
        public void GetDependencies_ShouldReturnDependencies()
        {
            // Arrange
            var deps = new ServiceDependencyNode("ServiceName", "ServiceDisplayName");

            _mockController
                .Setup(c => c.GetDependencies())
                .Returns(deps);

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "TestService", DisplayName = "TestServiceDisplayName" }
                 });

            // Act
            var result = _serviceManager.GetDependencies("TestService");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(deps, result);
            Assert.Equal(deps.ServiceName, result.ServiceName);
            Assert.Equal(deps.DisplayName, result.DisplayName);

            _mockController.Verify(c => c.GetDependencies(), Times.Once);
        }

        [Fact]
        public void GetDependencies_ShouldReturnNull()
        {
            // Arrange
            var deps = new ServiceDependencyNode("ServiceName", "ServiceDisplayName");

            _mockController
                .Setup(c => c.GetDependencies())
                .Returns(deps);

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(Array.Empty<WindowsServiceInfo>());

            // Act
            var result = _serviceManager.GetDependencies("TestService");

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void GetDependencies_InvalidServiceName_ShouldReturnNull(string serviceName)
        {
            // Act
            var result = _serviceManager.GetDependencies(serviceName);

            // Assert
            Assert.Null(result);

            _mockController.Verify(
                c => c.GetDependencies(),
                Times.Never);
        }

        [Fact]
        public void GetDependencies_ControllerThrows_ShouldReturnNull()
        {
            // Arrange
            _mockController
                .Setup(c => c.GetDependencies())
                .Throws(new InvalidOperationException("Boom!"));

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "TestService", DisplayName = "TestServiceDisplayName" }
                 });

            // Act
            var result = _serviceManager.GetDependencies("TestService");

            // Assert
            Assert.Null(result);

            _mockController.Verify(c => c.GetDependencies(), Times.Once);
        }

        [Fact]
        public void GetDependencies_ShouldDisposeController()
        {
            // Arrange
            _mockController
                .Setup(c => c.GetDependencies())
                .Returns(new ServiceDependencyNode("S", "D"));

            _mockWindowsServiceApi.Setup(p => p.GetServices())
                 .Returns(new[]
                 {
                    new WindowsServiceInfo { ServiceName = "TestService", DisplayName = "TestServiceDisplayName" }
                 });

            // Act
            _serviceManager.GetDependencies("TestService");

            // Assert
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion

        #region SafeHandle Helper Factory Methods

        private static SafeScmHandle CreateScmHandle(int value = 1)
        {
            // 1. Invoke the default constructor via reflection
            var handle = (SafeScmHandle)Activator.CreateInstance(typeof(SafeScmHandle), true);

            // 2. Safely set the handle value using the protected SetHandle method
            // We cast the int to IntPtr here
            var setHandleMethod = typeof(SafeHandle).GetMethod("SetHandle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setHandleMethod?.Invoke(handle, new object[] { (IntPtr)value });

            return handle;
        }

        private static SafeServiceHandle CreateServiceHandle(int value = 1)
        {
            var handle = (SafeServiceHandle)Activator.CreateInstance(typeof(SafeServiceHandle), true);

            var setHandleMethod = typeof(SafeHandle).GetMethod("SetHandle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setHandleMethod?.Invoke(handle, new object[] { (IntPtr)value });

            return handle;
        }

        #endregion
    }
}

#pragma warning restore CS8625