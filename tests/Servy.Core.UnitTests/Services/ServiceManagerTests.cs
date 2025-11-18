using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management;
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
        private readonly Mock<IWindowsServiceApi> _mockWindowsServiceApi;
        private readonly Mock<IWin32ErrorProvider> _mockWin32ErrorProvider;
        private readonly Mock<IServiceRepository> _mockServiceRepository;
        private readonly Mock<IWmiSearcher> _mockWmiSearcher;
        private ServiceManager _serviceManager;

        public ServiceManagerTests()
        {
            _mockController = new Mock<IServiceControllerWrapper>();
            _mockWindowsServiceApi = new Mock<IWindowsServiceApi>();
            _mockWin32ErrorProvider = new Mock<IWin32ErrorProvider>();
            _mockServiceRepository = new Mock<IServiceRepository>();
            _mockWmiSearcher = new Mock<IWmiSearcher>();

            _serviceManager = new ServiceManager(_ =>
            _mockController.Object,
            _mockWindowsServiceApi.Object,
            _mockWin32ErrorProvider.Object,
            _mockServiceRepository.Object,
            _mockWmiSearcher.Object
            );
        }


        [Theory]
        [InlineData("", "", "")]
        [InlineData("TestService", "", "")]
        [InlineData("TestService", "C:\\Apps\\App.exe", "")]
        public async Task InstallService_Throws_ArgumentNullException(string serviceName, string wrapperExePath, string realExePath)
        {
            var scmHandle = new IntPtr(123);
            var serviceHandle = new IntPtr(456);
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
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            await Assert.ThrowsAsync<ArgumentNullException>(() => _serviceManager.InstallService(
                serviceName,
                description,
                wrapperExePath,
                realExePath,
                "workingDir",
                "args",
                ServiceStartType.Automatic,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                null,
                null,

                null,
                null,
                null,
                null,
                null,
                null,
                30,
                0,
                false
                ));
        }

        [Theory]
        [InlineData("TestService", "C:\\Apps\\App.exe", "C:\\Apps\\App.exe")]
        public async Task InstallService_EmptyOptions(string serviceName, string wrapperExePath, string realExePath)
        {
            var scmHandle = new IntPtr(123);
            var serviceHandle = new IntPtr(456);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockServiceRepository.Setup(x => x.GetByNameAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DTOs.ServiceDto
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
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            var result = await _serviceManager.InstallService(
                 serviceName,
                 null,
                 wrapperExePath,
                 realExePath,
                 null,
                 null,
                 ServiceStartType.Automatic,
                 ProcessPriority.Normal,
                 null,
                 null,
                 false,
                 0,
                 false,
                 0,
                 0,
                 RecoveryAction.None,
                 0,
                 null,
                 null,
                 null,
                 null,

                 null,
                 null,
                 null,
                 null,
                 null,
                 null,
                 30,
                 0,
                 false
                 );

            Assert.True(result);
            _mockServiceRepository.Verify(x => x.GetByNameAsync(serviceName, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_Throws_Win32Exception()
        {
            var scmHandle = IntPtr.Zero;
            var serviceHandle = new IntPtr(456);
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
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            await Assert.ThrowsAsync<Win32Exception>(() => _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.Automatic,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                @".\username",
                "password",

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true
                ));

            scmHandle = new IntPtr(123);
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
             .Returns(scmHandle);
            _mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1074);

            await Assert.ThrowsAsync<Win32Exception>(() => _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.Automatic,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                @".\username",
                "password",

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true
                ));

        }

        [Fact]
        public async Task InstallService_CreatesService_AndSetsDescription_WhenServiceDoesNotExist()
        {
            var scmHandle = new IntPtr(123);
            var serviceHandle = new IntPtr(456);
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
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            var result = await _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.Automatic,
                ProcessPriority.Normal,
                null,
                null,
                true,
                1024 * 1024,
                true,
                30,
                3,
                RecoveryAction.None,
                1,
                string.Empty,
                null,
                null,
                null,

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true,
                @"C:\Apps\App\app.exe",
                @"C:\Apps\App",
                "--arg1 val1",
                @"C:\Apps\App\app.exe",
                @"C:\Apps\App",
                "--arg1 val1"
                );

            Assert.True(result);

            _mockWindowsServiceApi.Verify(x => x.OpenSCManager(null, null, It.IsAny<uint>()), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<ServiceDescription>.IsAny), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CloseServiceHandle(serviceHandle), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CloseServiceHandle(scmHandle), Times.Once);
        }

        [Fact]
        public async Task InstallService_CallsUpdateServiceConfig_WhenServiceExistsError()
        {
            var scmHandle = new IntPtr(123);
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
                .Returns(IntPtr.Zero);

            // Simulate ERROR_SERVICE_EXISTS
            //_mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1073);
            _mockWindowsServiceApi.Setup(x => x.GetServices()).Returns(new List<WindowsServiceInfo> { new WindowsServiceInfo { ServiceName = serviceName } });

            // Setup OpenService for UpdateServiceConfig
            var serviceHandle = new IntPtr(456);
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
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<IntPtr>(), It.IsAny<int>(), ref It.Ref<ServiceDelayedAutoStartInfo>.IsAny)).Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle)).Returns(true);

            var result = await _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.Automatic,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                null,
                null,

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true,
                serviceName
                );

            Assert.True(result);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig(serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null, It.IsAny<string>()), Times.Once);

            result = await _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.AutomaticDelayedStart,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                null,
                null,

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true,
                serviceName
                );

            Assert.True(result);
        }

        [Fact]
        public async Task InstallService_CallsUpdateServiceConfig2_WhenServiceExistsError()
        {
            var scmHandle = new IntPtr(123);
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
                .Returns(IntPtr.Zero);

            // Simulate ERROR_SERVICE_EXISTS
            //_mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1073);
            _mockWindowsServiceApi.Setup(x => x.GetServices()).Returns(new List<WindowsServiceInfo> { new WindowsServiceInfo { ServiceName = serviceName } });

            // Setup OpenService for UpdateServiceConfig
            var serviceHandle = new IntPtr(456);
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
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<IntPtr>(), It.IsAny<int>(), ref It.Ref<ServiceDelayedAutoStartInfo>.IsAny)).Returns(false);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle)).Returns(true);

            var result = await _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.Automatic,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                null,
                null,

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true
                );

            Assert.False(result);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig(serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task InstallService_DelayedAutoStart()
        {
            var scmHandle = new IntPtr(123);
            var serviceName = "TestService";
            var description = "Test Description";
            var gMSA = @"TEST\gMSA$";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            var serviceHandle = new IntPtr(456);
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

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<ServiceDelayedAutoStartInfo>.IsAny)).Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<IntPtr>(),
               It.IsAny<int>(),
               ref It.Ref<ServiceDescription>.IsAny))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle)).Returns(true);

            var result = await _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.AutomaticDelayedStart,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                gMSA,
                null,

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true
                );

            Assert.True(result);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, gMSA, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<IntPtr>(), It.IsAny<int>(), ref It.Ref<ServiceDelayedAutoStartInfo>.IsAny), Times.Once);
        }

        [Fact]
        public async Task InstallService_DelayedAutoStart_Error()
        {
            var scmHandle = new IntPtr(123);
            var serviceName = "TestService";
            var description = "Test Description";

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            var serviceHandle = new IntPtr(456);
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

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<ServiceDelayedAutoStartInfo>.IsAny)).Returns(false);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
               It.IsAny<IntPtr>(),
               It.IsAny<int>(),
               ref It.Ref<ServiceDescription>.IsAny))
               .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle)).Returns(true);

            var result = await _serviceManager.InstallService(
                serviceName,
                description,
                "wrapper.exe",
                "real.exe",
                "workingDir",
                "args",
                ServiceStartType.AutomaticDelayedStart,
                ProcessPriority.Normal,
                null,
                null,
                false,
                0,
                false,
                0,
                0,
                RecoveryAction.None,
                0,
                string.Empty,
                null,
                null,
                null,

                "pre-launch.exe",
                "preLaunchDir",
                "preLaunchArgs",
                "var1=val1;var2=val2;",
                "pre-launch-stdout.log",
                "pre-launch-stderr.log",
                30,
                0,
                true
                );

            Assert.False(result);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, ServiceDependenciesParser.NoDependencies, ServiceManager.LocalSystemAccount, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(It.IsAny<IntPtr>(), It.IsAny<int>(), ref It.Ref<ServiceDelayedAutoStartInfo>.IsAny), Times.Once);
        }

        [Fact]
        public void UpdateServiceConfig_Succeeds_WhenServiceIsOpenedAndConfigChanged()
        {
            var scmHandle = new IntPtr(123);
            var serviceHandle = new IntPtr(456);
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
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);

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
            var scmHandle = new IntPtr(123);
            var serviceHandle = IntPtr.Zero;
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
                ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);

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

            serviceHandle = new IntPtr(123);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, serviceName, It.IsAny<uint>()))
               .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(false);

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
        public void SetServiceDescription_ReturnsImmediately_WhenDescriptionIsNullOrEmpty()
        {
            var serviceHandle = new IntPtr(456);

            // Should not call ChangeServiceConfig2 if description is null or empty
            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<ServiceDescription>.IsAny))
                .Returns(true);

            _serviceManager.SetServiceDescription(serviceHandle, null);
            _serviceManager.SetServiceDescription(serviceHandle, "");

            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<ServiceDescription>.IsAny), Times.Never);
        }

        [Fact]
        public void SetServiceDescription_Throws_WhenChangeServiceConfig2Fails()
        {
            var serviceHandle = new IntPtr(456);
            var description = "desc";

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<ServiceDescription>.IsAny))
                .Returns(false);

            Assert.Throws<Win32Exception>(() => _serviceManager.SetServiceDescription(serviceHandle, description));
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenOpenSCManagerFails()
        {
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(IntPtr.Zero);

            var result = await _serviceManager.UninstallService("ServiceName");
            Assert.False(result);
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenOpenServiceFails()
        {
            var scmHandle = new IntPtr(123);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "ServiceName", It.IsAny<uint>()))
                .Returns(IntPtr.Zero);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle)).Returns(true);

            var result = await _serviceManager.UninstallService("ServiceName");
            Assert.False(result);
        }

        [Fact]
        public async Task UninstallService_ReturnsFalse_WhenDeleteServiceFails()
        {
            var serviceName = "ServiceName";
            var scmHandle = new IntPtr(123);
            var serviceHandle = new IntPtr(456);

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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<Native.NativeMethods.ServiceStatus>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(false);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle))
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
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object,
                _mockWmiSearcher.Object
                );

            var result = await _serviceManager.UninstallService(serviceName);

            Assert.False(result);
        }

        [Fact]
        public async Task UninstallService_StopsAndDeletesServiceSuccessfully()
        {
            var serviceName = "ServiceName";
            var scmHandle = new IntPtr(123);
            var serviceHandle = new IntPtr(456);

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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<Native.NativeMethods.ServiceStatus>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle))
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
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object,
                _mockWmiSearcher.Object
                );

            var result = await _serviceManager.UninstallService(serviceName);

            Assert.True(result);

            mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);
            mockController.VerifyGet(c => c.Status, Times.AtLeastOnce);
        }


        [Fact]
        public async Task UninstallService_StopsAndDeletesServiceSuccessfully_WithPolling()
        {
            var serviceName = "ServiceName";
            var scmHandle = new IntPtr(123);
            var serviceHandle = new IntPtr(456);

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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<Native.NativeMethods.ServiceStatus>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.DeleteService(serviceHandle))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle))
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
                _mockWindowsServiceApi.Object,
                _mockWin32ErrorProvider.Object,
                _mockServiceRepository.Object,
                _mockWmiSearcher.Object
            );

            var result = await _serviceManager.UninstallService(serviceName);

            Assert.True(result);

            // Verify the methods were called at least once
            mockController.Verify(sc => sc.Refresh(), Times.AtLeastOnce);
            mockController.Verify(sc => sc.Status, Times.AtLeastOnce);
        }


        [Fact]
        public void StartService_ShouldReturnTrue_WhenAlreadyRunning()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);

            // Act
            var result = _serviceManager.StartService("TestService");

            // Assert
            Assert.True(result);
            _mockController.Verify(c => c.Start(), Times.Never);
        }

        [Fact]
        public void StartService_ShouldStartAndWait_WhenNotRunning()
        {
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Stopped);

            var result = _serviceManager.StartService("TestService");

            Assert.True(result);
            _mockController.Verify(c => c.Start(), Times.Once);
            _mockController.Verify(c => c.WaitForStatus(ServiceControllerStatus.Running, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public void StartService_ShouldReturnFalse_WhenExceptionIsThrown()
        {
            _mockController.Setup(c => c.Status).Throws<InvalidOperationException>();

            var result = _serviceManager.StartService("TestService");

            Assert.False(result);
        }

        [Fact]
        public void StopService_ShouldReturnTrue_WhenAlreadyStopped()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Stopped);

            // Act
            var result = _serviceManager.StopService("TestService");

            // Assert
            Assert.True(result);
            _mockController.Verify(c => c.Stop(), Times.Never);
        }

        [Fact]
        public void StopService_ShouldStopAndWait_WhenNotStopped()
        {
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);

            var result = _serviceManager.StopService("TestService");

            Assert.True(result);
            _mockController.Verify(c => c.Stop(), Times.Once);
            _mockController.Verify(c => c.WaitForStatus(ServiceControllerStatus.Stopped, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public void StopService_ShouldReturnFalse_WhenExceptionIsThrown()
        {
            _mockController.Setup(c => c.Status).Throws<InvalidOperationException>();

            var result = _serviceManager.StopService("TestService");

            Assert.False(result);
        }

        [Fact]
        public void RestartService_ShouldStopAndStart_WhenStopSucceeds()
        {
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running)
                .Returns(ServiceControllerStatus.Stopped)
                .Returns(ServiceControllerStatus.Running);

            var result = _serviceManager.RestartService("TestService");

            Assert.True(result);
            _mockController.Verify(c => c.Stop(), Times.Once);
            _mockController.Verify(c => c.Start(), Times.Once);
        }

        [Fact]
        public void RestartService_ShouldReturnFalse_WhenStopServiceFails()
        {
            // Arrange
            // Simulate the service is already stopped so StopService returns true
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Running);

            // Simulate StartService throwing an exception, which should trigger catch and return false
            _mockController.Setup(c => c.Stop()).Throws(new Exception("Boom!"));

            // Act
            var result = _serviceManager.RestartService("TestService");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RestartService_ShouldReturnFalse_WhenStartServiceFails()
        {
            // Arrange
            // Simulate the service is already stopped so StopService returns true
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.Stopped);

            // Simulate StartService throwing an exception, which should trigger catch and return false
            _mockController.Setup(c => c.Start()).Throws(new Exception("Boom!"));

            // Act
            var result = _serviceManager.RestartService("TestService");

            // Assert
            Assert.False(result);
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

        [Fact]
        public void GetServiceStartupType_ReturnsExpectedEnum()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["StartMode"] = "Auto";

            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(new[] { mo });

            var result = _serviceManager.GetServiceStartupType("MyService");

            Assert.Equal(ServiceStartType.Automatic, result);
        }

        [Fact]
        public void GetServiceStartupType_ReturnsNull_WhenServiceNull()
        {
            // Arrange
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Enumerable.Empty<ManagementObject>());

            // Act
            var result = _serviceManager.GetServiceStartupType("Unknown");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceStartupType_ReturnsNull_WhenNull()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["StartMode"] = null;

            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(new[] { mo });

            var result = _serviceManager.GetServiceStartupType("MyService");

            Assert.Null(result);
        }

        [Fact]
        public void GetServiceStartupType_ReturnsNull_WhenUnknownMode()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["StartMode"] = "Unknown";

            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(new[] { mo });

            var result = _serviceManager.GetServiceStartupType("MyService");

            Assert.Null(result);
        }

        [Fact]
        public void GetServiceStartupType_Throws_ArgumentTnullException()
        {
            Assert.Throws<ArgumentNullException>(() => _serviceManager.GetServiceStartupType(string.Empty));
        }

        #region GetServiceDescription Tests

        [Fact]
        public void GetServiceDescription_ServiceExists_ReturnsDescription()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["Description"] = "My Service Description";

            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(new[] { mo });

            var result = _serviceManager.GetServiceDescription("MyService");
            Assert.Equal("My Service Description", result);
        }

        [Fact]
        public void GetServiceDescription_ServiceExistsButDescriptionNull_ReturnsNull()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["Description"] = null;

            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(new[] { mo });

            var result = _serviceManager.GetServiceDescription("MyService");
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceDescription_NoServices_ReturnsNull()
        {
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Array.Empty<ManagementObject>());

            var result = _serviceManager.GetServiceDescription("NonExistentService");
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceDescription_ExceptionThrown_ReturnsNull()
        {
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Throws(new Exception("Boom"));

            var result = _serviceManager.GetServiceDescription("Service");
            Assert.Null(result);
        }

        #endregion

        #region GetServiceUser Tests

        [Fact]
        public void GetServiceUser_ServiceExists_ReturnsAccount()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["StartName"] = "LocalSystem";

            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(new[] { mo });

            var result = _serviceManager.GetServiceUser("MyService");
            Assert.Equal("LocalSystem", result);
        }

        [Fact]
        public void GetServiceUser_ServiceExistsButStartNameNull_ReturnsNull()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["StartName"] = null;

            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(new[] { mo });

            var result = _serviceManager.GetServiceUser("MyService");
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceUser_NoServices_ReturnsNull()
        {
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Array.Empty<ManagementObject>());

            var result = _serviceManager.GetServiceUser("NonExistentService");
            Assert.Null(result);
        }

        #endregion

        #region GetAllServices Tests

        [Fact]
        public void GetAllServices_ShouldReturnEmptyList_WhenNoServices()
        {
            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(new ManagementObject[0]);

            var result = _serviceManager.GetAllServices();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllServices_ShouldThrowsException()
        {
            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(IntPtr.Zero);
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(new ManagementObject[0]);

            Assert.Throws<Win32Exception>(() => _serviceManager.GetAllServices());
        }

        [Theory]
        [InlineData("Running", Enums.ServiceStatus.Running)]
        [InlineData("Stopped", Enums.ServiceStatus.Stopped)]
        [InlineData("Paused", Enums.ServiceStatus.Paused)]
        [InlineData("Start Pending", Enums.ServiceStatus.StartPending)]
        [InlineData("Stop Pending", Enums.ServiceStatus.StopPending)]
        [InlineData("Pause Pending", Enums.ServiceStatus.PausePending)]
        [InlineData("Continue Pending", Enums.ServiceStatus.ContinuePending)]
        [InlineData(null, Enums.ServiceStatus.None)]
        [InlineData("UnknownState", Enums.ServiceStatus.None)]
        public void GetAllServices_ShouldMapStateCorrectly(string wmiState, Enums.ServiceStatus expectedStatus)
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["State"] = wmiState;
            mo["StartMode"] = "Manual";
            mo["StartName"] = "LocalSystem";
            mo["Description"] = "desc";
            mo["Name"] = "svc1";

            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(new[] { mo });

            var result = _serviceManager.GetAllServices();

            var service = Assert.Single(result);
            Assert.Equal(expectedStatus, service.Status);
        }

        [Theory]
        [InlineData("Auto", ServiceStartType.Automatic)]
        [InlineData("Automatic", ServiceStartType.Automatic)]
        [InlineData("Manual", ServiceStartType.Manual)]
        [InlineData("Disabled", ServiceStartType.Disabled)]
        [InlineData(null, ServiceStartType.Automatic)]
        [InlineData("Unknown", ServiceStartType.Automatic)]
        public void GetAllServices_ShouldMapStartModeCorrectly(string wmiStartMode, ServiceStartType expectedType)
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["State"] = "Running";
            mo["StartMode"] = wmiStartMode;
            mo["StartName"] = "LocalSystem";
            mo["Description"] = "desc";
            mo["Name"] = "svc1";

            var info = new ServiceDelayedAutoStartInfo();
            int bytesNeeded = 0;
            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWindowsServiceApi.Setup(s => s.OpenService(It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWindowsServiceApi.Setup(s => s.QueryServiceConfig2(It.IsAny<IntPtr>(), It.IsAny<uint>(), ref info, It.IsAny<int>(), ref bytesNeeded)).Returns(false);
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(new[] { mo });

            var result = _serviceManager.GetAllServices();

            var service = Assert.Single(result);
            Assert.Equal(expectedType, service.StartupType);
        }

        delegate void QueryServiceConfig2Delegate(IntPtr hService, uint infoLevel, ref ServiceDelayedAutoStartInfo info, int bufSize, ref int bytesNeeded);

        [Fact]
        public void GetAllServices_ShouldTestDelayedAutoStart()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["State"] = "Running";
            mo["StartMode"] = "Automatic";
            mo["StartName"] = "LocalSystem";
            mo["Description"] = "desc";
            mo["Name"] = "svc1";

            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWindowsServiceApi.Setup(s => s.OpenService(It.IsAny<IntPtr>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWindowsServiceApi
                .Setup(s => s.QueryServiceConfig2(
                    It.IsAny<IntPtr>(),
                    It.IsAny<uint>(),
                    ref It.Ref<ServiceDelayedAutoStartInfo>.IsAny,
                    It.IsAny<int>(),
                    ref It.Ref<int>.IsAny))
                .Callback(new QueryServiceConfig2Delegate((IntPtr hService, uint infoLevel, ref ServiceDelayedAutoStartInfo info, int bufSize, ref int bytesNeeded) =>
                {
                    // Simulate delayed auto-start being enabled
                    info.fDelayedAutostart = true;
                    bytesNeeded = Marshal.SizeOf(typeof(ServiceDelayedAutoStartInfo));
                }))
                .Returns(true); _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(new[] { mo });

            var result = _serviceManager.GetAllServices();

            var service = Assert.Single(result);
            Assert.Equal(ServiceStartType.AutomaticDelayedStart, service.StartupType);
        }

        [Fact]
        public void GetAllServices_ShouldUseDefaults_WhenFieldsAreNull()
        {
            var mo = new ManagementClass("Win32_Service").CreateInstance();
            mo["State"] = null;
            mo["StartMode"] = null;
            mo["StartName"] = null;
            mo["Description"] = null;
            mo["Name"] = null;

            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(new[] { mo });

            var result = _serviceManager.GetAllServices();

            var service = Assert.Single(result);
            Assert.Equal(Enums.ServiceStatus.None, service.Status);
            Assert.Equal(ServiceStartType.Automatic, service.StartupType);
            Assert.Equal("LocalSystem", service.UserSession);
            Assert.Equal(string.Empty, service.Description);
            Assert.Equal(string.Empty, service.Name);
        }

        [Fact]
        public void GetAllServices_ShouldHandleMultipleServices()
        {
            var mo1 = new ManagementClass("Win32_Service").CreateInstance();
            mo1["State"] = "Running";
            mo1["StartMode"] = "Manual";
            mo1["StartName"] = "User1";
            mo1["Description"] = "desc1";
            mo1["Name"] = "svc1";

            var mo2 = new ManagementClass("Win32_Service").CreateInstance();
            mo2["State"] = "Stopped";
            mo2["StartMode"] = "Disabled";
            mo2["StartName"] = "User2";
            mo2["Description"] = "desc2";
            mo2["Name"] = "svc2";

            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(new[] { mo1, mo2 });

            var result = _serviceManager.GetAllServices();

            Assert.Equal(2, result.Count);

            Assert.Equal("svc1", result[0].Name);
            Assert.Equal(Enums.ServiceStatus.Running, result[0].Status);
            Assert.Equal(ServiceStartType.Manual, result[0].StartupType);
            Assert.Equal("User1", result[0].UserSession);

            Assert.Equal("svc2", result[1].Name);
            Assert.Equal(Enums.ServiceStatus.Stopped, result[1].Status);
            Assert.Equal(ServiceStartType.Disabled, result[1].StartupType);
            Assert.Equal("User2", result[1].UserSession);
        }

        [Fact]
        public void GetAllServices_ShouldMapAllServiceStates_CoverNullToString()
        {
            // Arrange
            var moNull = new ManagementClass("Win32_Service").CreateInstance();
            moNull["State"] = null;        // Simulate null state
            moNull["StartMode"] = "Auto";
            moNull["StartName"] = null;
            moNull["Description"] = null;
            moNull["Name"] = "NullStateService";

            var moRunning = new ManagementClass("Win32_Service").CreateInstance();
            moRunning["State"] = "Running";
            moRunning["StartMode"] = "Manual";
            moRunning["StartName"] = "User1";
            moRunning["Description"] = "Running Service";
            moRunning["Name"] = "RunningService";

            var moStopped = new ManagementClass("Win32_Service").CreateInstance();
            moStopped["State"] = "Stopped";
            moStopped["StartMode"] = "Disabled";
            moStopped["StartName"] = "User2";
            moStopped["Description"] = "Stopped Service";
            moStopped["Name"] = "StoppedService";

            var mos = new[] { moNull, moRunning, moStopped };

            _mockWindowsServiceApi.Setup(s => s.OpenSCManager(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>())).Returns(new IntPtr(1));
            _mockWmiSearcher.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(mos);

            // Act
            var services = _serviceManager.GetAllServices();

            // Assert
            Assert.Equal(3, services.Count);

            var nullStateService = services.First(s => s.Name == "NullStateService");
            Assert.Equal(Enums.ServiceStatus.None, nullStateService.Status);       // branch with stateObj null
            Assert.Equal(ServiceStartType.Automatic, nullStateService.StartupType);
            Assert.Equal("LocalSystem", nullStateService.UserSession);
            Assert.Equal(string.Empty, nullStateService.Description);

            var runningService = services.First(s => s.Name == "RunningService");
            Assert.Equal(Enums.ServiceStatus.Running, runningService.Status);
            Assert.Equal(ServiceStartType.Manual, runningService.StartupType);
            Assert.Equal("User1", runningService.UserSession);
            Assert.Equal("Running Service", runningService.Description);

            var stoppedService = services.First(s => s.Name == "StoppedService");
            Assert.Equal(Enums.ServiceStatus.Stopped, stoppedService.Status);
            Assert.Equal(ServiceStartType.Disabled, stoppedService.StartupType);
            Assert.Equal("User2", stoppedService.UserSession);
            Assert.Equal("Stopped Service", stoppedService.Description);
        }


        #endregion

    }
}

#pragma warning restore CS8625
