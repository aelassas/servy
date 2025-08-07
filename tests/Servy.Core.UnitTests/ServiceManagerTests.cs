using Moq;
using Servy.Core.Enums;
using Servy.Core.Interfaces;
using System;
using Xunit;

namespace Servy.Core.UnitTests
{
    public class ServiceManagerTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;

        public ServiceManagerTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
        }

        [Fact]
        public void InstallService_ValidParameters_ReturnsTrue()
        {
            // Arrange
            _mockServiceManager
                .Setup(sm => sm.InstallService(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ServiceStartType>(),
                    It.IsAny<ProcessPriority>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<RecoveryAction>(),
                    It.IsAny<int>()))
                .Returns(true);

            // Act
            bool result = _mockServiceManager.Object.InstallService(
                "MyService",
                "desc",
                @"C:\wrapper.exe",
                @"C:\real.exe",
                @"C:\workingdir",
                "-arg1",
                ServiceStartType.Automatic,
                ProcessPriority.Normal);

            // Assert
            Assert.True(result);

            _mockServiceManager.Verify(sm => sm.InstallService(
                "MyService",
                "desc",
                @"C:\wrapper.exe",
                @"C:\real.exe",
                @"C:\workingdir",
                "-arg1",
                ServiceStartType.Automatic,
                ProcessPriority.Normal,
                null,
                null,
                0,
                0,
                0,
                RecoveryAction.None,
                0), Times.Once);
        }

        [Fact]
        public void InstallService_NullOrEmptyRequiredParameters_ThrowsArgumentNullException()
        {
            // Setup mock to throw when serviceName is null or whitespace
            _mockServiceManager
                .Setup(sm => sm.InstallService(
                    It.Is<string>(s => string.IsNullOrWhiteSpace(s)),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ServiceStartType>(),
                    It.IsAny<ProcessPriority>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<RecoveryAction>(),
                    It.IsAny<int>()))
                .Throws<ArgumentNullException>();

            // Setup mock to throw when wrapperExePath is null or whitespace
            _mockServiceManager
                .Setup(sm => sm.InstallService(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<string>(s => string.IsNullOrWhiteSpace(s)),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ServiceStartType>(),
                    It.IsAny<ProcessPriority>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<RecoveryAction>(),
                    It.IsAny<int>()))
                .Throws<ArgumentNullException>();

            // Setup mock to throw when realExePath is null or whitespace
            _mockServiceManager
                .Setup(sm => sm.InstallService(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<string>(s => string.IsNullOrWhiteSpace(s)),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ServiceStartType>(),
                    It.IsAny<ProcessPriority>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<RecoveryAction>(),
                    It.IsAny<int>()))
                .Throws<ArgumentNullException>();

            Assert.Throws<ArgumentNullException>(() => _mockServiceManager.Object.InstallService(
                string.Empty, "desc", @"C:\wrapper.exe", @"C:\real.exe", @"C:\workingdir", "-arg1",
                ServiceStartType.Automatic, ProcessPriority.Normal));

            Assert.Throws<ArgumentNullException>(() => _mockServiceManager.Object.InstallService(
                "Service", "desc", string.Empty, @"C:\real.exe", @"C:\workingdir", "-arg1",
                ServiceStartType.Automatic, ProcessPriority.Normal));

            Assert.Throws<ArgumentNullException>(() => _mockServiceManager.Object.InstallService(
                "Service", "desc", @"C:\wrapper.exe", string.Empty, @"C:\workingdir", "-arg1",
                ServiceStartType.Automatic, ProcessPriority.Normal));
        }

        [Fact]
        public void UninstallService_ValidServiceName_ReturnsTrue()
        {
            _mockServiceManager
                .Setup(sm => sm.UninstallService(It.IsAny<string>()))
                .Returns(true);

            bool result = _mockServiceManager.Object.UninstallService("MyService");

            Assert.True(result);
            _mockServiceManager.Verify(sm => sm.UninstallService("MyService"), Times.Once);
        }

        [Fact]
        public void UninstallService_InvalidServiceName_ReturnsFalse()
        {
            _mockServiceManager
                .Setup(sm => sm.UninstallService(It.Is<string>(s => string.IsNullOrEmpty(s))))
                .Returns(false);

            bool result = _mockServiceManager.Object.UninstallService(string.Empty);

            Assert.False(result);
        }

        [Fact]
        public void StartService_ValidServiceName_ReturnsTrue()
        {
            _mockServiceManager
                .Setup(sm => sm.StartService(It.IsAny<string>()))
                .Returns(true);

            bool result = _mockServiceManager.Object.StartService("MyService");

            Assert.True(result);
            _mockServiceManager.Verify(sm => sm.StartService("MyService"), Times.Once);
        }

        [Fact]
        public void StartService_InvalidServiceName_ReturnsFalse()
        {
            _mockServiceManager
                .Setup(sm => sm.StartService(It.Is<string>(s => string.IsNullOrEmpty(s))))
                .Returns(false);

            bool result = _mockServiceManager.Object.StartService(string.Empty);

            Assert.False(result);
        }

        [Fact]
        public void StopService_ValidServiceName_ReturnsTrue()
        {
            _mockServiceManager
                .Setup(sm => sm.StopService(It.IsAny<string>()))
                .Returns(true);

            bool result = _mockServiceManager.Object.StopService("MyService");

            Assert.True(result);
            _mockServiceManager.Verify(sm => sm.StopService("MyService"), Times.Once);
        }

        [Fact]
        public void StopService_InvalidServiceName_ReturnsFalse()
        {
            _mockServiceManager
                .Setup(sm => sm.StopService(It.Is<string>(s => string.IsNullOrEmpty(s))))
                .Returns(false);

            bool result = _mockServiceManager.Object.StopService(string.Empty);

            Assert.False(result);
        }

        [Fact]
        public void RestartService_ValidServiceName_ReturnsTrue()
        {
            _mockServiceManager
                .Setup(sm => sm.RestartService(It.IsAny<string>()))
                .Returns(true);

            bool result = _mockServiceManager.Object.RestartService("MyService");

            Assert.True(result);
            _mockServiceManager.Verify(sm => sm.RestartService("MyService"), Times.Once);
        }

        [Fact]
        public void RestartService_InvalidServiceName_ReturnsFalse()
        {
            _mockServiceManager
                .Setup(sm => sm.RestartService(It.Is<string>(s => string.IsNullOrEmpty(s))))
                .Returns(false);

            bool result = _mockServiceManager.Object.RestartService(string.Empty);

            Assert.False(result);
        }
    }
}
