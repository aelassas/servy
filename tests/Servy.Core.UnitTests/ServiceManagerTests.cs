using Moq;
using Servy.Core.Enums;
using Servy.Core.Interfaces;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ServiceProcess;
using Xunit;
using static Servy.Core.Native.NativeMethods;

#pragma warning disable CS8625

namespace Servy.Core.UnitTests
{
    public class ServiceManagerTests
    {
        private readonly Mock<IServiceControllerWrapper> _mockController;
        private readonly Mock<IWindowsServiceApi> _mockWindowsServiceApi;
        private readonly Mock<IWin32ErrorProvider> _mockWin32ErrorProvider;
        private ServiceManager _serviceManager;

        public ServiceManagerTests()
        {
            _mockController = new Mock<IServiceControllerWrapper>();
            _mockWindowsServiceApi = new Mock<IWindowsServiceApi>();
            _mockWin32ErrorProvider = new Mock<IWin32ErrorProvider>();
            _serviceManager = new ServiceManager(_ => _mockController.Object, _mockWindowsServiceApi.Object, _mockWin32ErrorProvider.Object);
        }

        [Theory]
        [InlineData("", "", "")]
        [InlineData("TestService", "", "")]
        [InlineData("TestService", "C:\\Apps\\App.exe", "")]
        public void InstallService_Throws_ArgumentNullException(string serviceName, string wrapperExePath, string realExePath)
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
                null,
                null,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            Assert.Throws<ArgumentNullException>(() => _serviceManager.InstallService(
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
                0,
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

        [Fact]
        public void InstallService_Throws_Win32Exception()
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
                null,
                null,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            Assert.Throws<Win32Exception>(() => _serviceManager.InstallService(
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
                0,
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

            Assert.Throws<Win32Exception>(() => _serviceManager.InstallService(
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
                0,
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
        public void InstallService_CreatesService_AndSetsDescription_WhenServiceDoesNotExist()
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
                null,
                null,
                null))
                .Returns(serviceHandle);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(It.IsAny<IntPtr>())).Returns(true);

            var result = _serviceManager.InstallService(
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
                0,
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

            Assert.True(result);

            _mockWindowsServiceApi.Verify(x => x.OpenSCManager(null, null, It.IsAny<uint>()), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, null, null, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CloseServiceHandle(serviceHandle), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.CloseServiceHandle(scmHandle), Times.Once);
        }

        [Fact]
        public void InstallService_CallsUpdateServiceConfig_WhenServiceExistsError()
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
                null,
                null,
                null))
                .Returns(IntPtr.Zero);

            // Simulate ERROR_SERVICE_EXISTS
            _mockWin32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1073);

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

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);
            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle)).Returns(true);

            var result = _serviceManager.InstallService(
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
                0,
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

            Assert.True(result);

            _mockWindowsServiceApi.Verify(x => x.CreateService(scmHandle, serviceName, serviceName, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, null, null, null), Times.Once);
            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig(serviceHandle, It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>(), null, IntPtr.Zero, null, null, null, null), Times.Once);
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
                null))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(
                serviceHandle,
                It.IsAny<int>(),
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);

            var result = _serviceManager.UpdateServiceConfig(
                scmHandle,
                serviceName,
                description,
                binPath,
                ServiceStartType.Automatic);

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
                ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(serviceHandle)).Returns(true);

            Assert.Throws<Win32Exception>(() =>
                _serviceManager.UpdateServiceConfig(
                    scmHandle,
                    serviceName,
                    description,
                    binPath,
                    ServiceStartType.Automatic)
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
                    ServiceStartType.Automatic)
            );
        }

        [Fact]
        public void SetServiceDescription_ReturnsImmediately_WhenDescriptionIsNullOrEmpty()
        {
            var serviceHandle = new IntPtr(456);

            // Should not call ChangeServiceConfig2 if description is null or empty
            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(true);

            _serviceManager.SetServiceDescription(serviceHandle, null);
            _serviceManager.SetServiceDescription(serviceHandle, "");

            _mockWindowsServiceApi.Verify(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny), Times.Never);
        }

        [Fact]
        public void SetServiceDescription_Throws_WhenChangeServiceConfig2Fails()
        {
            var serviceHandle = new IntPtr(456);
            string description = "desc";

            _mockWindowsServiceApi.Setup(x => x.ChangeServiceConfig2(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_DESCRIPTION>.IsAny))
                .Returns(false);

            Assert.Throws<Win32Exception>(() => _serviceManager.SetServiceDescription(serviceHandle, description));
        }

        [Fact]
        public void UninstallService_ReturnsFalse_WhenOpenSCManagerFails()
        {
            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(IntPtr.Zero);

            var result = _serviceManager.UninstallService("ServiceName");
            Assert.False(result);
        }

        [Fact]
        public void UninstallService_ReturnsFalse_WhenOpenServiceFails()
        {
            var scmHandle = new IntPtr(123);

            _mockWindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>()))
                .Returns(scmHandle);

            _mockWindowsServiceApi.Setup(x => x.OpenService(scmHandle, "ServiceName", It.IsAny<uint>()))
                .Returns(IntPtr.Zero);

            _mockWindowsServiceApi.Setup(x => x.CloseServiceHandle(scmHandle)).Returns(true);

            var result = _serviceManager.UninstallService("ServiceName");
            Assert.False(result);
        }

        [Fact]
        public void UninstallService_StopsAndDeletesServiceSuccessfully()
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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_STATUS>.IsAny))
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
                _mockWin32ErrorProvider.Object);

            var result = _serviceManager.UninstallService(serviceName);

            Assert.True(result);

            mockController.Verify(c => c.Refresh(), Times.AtLeastOnce);
            mockController.VerifyGet(c => c.Status, Times.AtLeastOnce);
        }


        [Fact]
        public void UninstallService_StopsAndDeletesServiceSuccessfully_WithPolling()
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

            _mockWindowsServiceApi.Setup(x => x.ControlService(serviceHandle, It.IsAny<int>(), ref It.Ref<SERVICE_STATUS>.IsAny))
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
                _mockWin32ErrorProvider.Object
            );

            var result = _serviceManager.UninstallService(serviceName);

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

    }
}

#pragma warning restore CS8625
