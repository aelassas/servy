using Moq;
using Servy.Core.Common;
using Servy.Core.Domain;
using Servy.Core.Enums;
using Servy.Core.Services;
using System.ServiceProcess;

namespace Servy.Core.UnitTests.Domain
{
    public class ServiceDomainTests
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;

        public ServiceDomainTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
        }

        private Service CreateService(string name = "TestService")
        {
            return new Service(_serviceManagerMock.Object)
            {
                Name = name,
                ExecutablePath = @"C:\path\app.exe"
            };
        }

        [Fact]
        public async Task Start_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            var result = await service.Start(TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Stop_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            var result = await service.Stop(TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Restart_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            var result = await service.Restart(TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetStatus_ShouldReturnNull_WhenServiceNotInstalled()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService")).Returns(false);

            var result = service.GetStatus(TestContext.Current.CancellationToken);

            Assert.Null(result);
        }

        [Fact]
        public void GetStatus_ShouldReturnStatus_WhenInstalled()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService")).Returns(true);
            _serviceManagerMock.Setup(s => s.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);

            var result = service.GetStatus(TestContext.Current.CancellationToken);

            Assert.Equal(ServiceControllerStatus.Running, result);
        }

        [Fact]
        public void IsInstalled_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService")).Returns(true);

            var result = service.IsInstalled();

            Assert.True(result);
            _serviceManagerMock.Verify(s => s.IsServiceInstalled("TestService"), Times.Once);
        }

        [Fact]
        public void GetServiceStartupType_ShouldDelegateToServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.GetServiceStartupType("TestService", It.IsAny<CancellationToken>()))
                .Returns(ServiceStartType.Automatic);

            var result = service.GetServiceStartupType();

            Assert.Equal(ServiceStartType.Automatic, result);
        }

        [Fact]
        public async Task Install_ShouldCallServiceManagerWithCorrectArguments()
        {
            // Arrange
            var service = new Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                DisplayName = "TestService",
                Description = "My Test Service",
                ExecutablePath = "C:\\real.exe",
                StartupDirectory = @"C:\MyApp",
                Parameters = "--arg1",
                StartupType = ServiceStartType.Automatic,
                Priority = ProcessPriority.Normal,
                EnableSizeRotation = false,
                EnableHealthMonitoring = false,
                RecoveryAction = RecoveryAction.None,
                MaxRestartAttempts = 3,
                RunAsLocalSystem = false,
                UserAccount = @".\user",
                Password = "secret",
                PreLaunchExecutablePath = "C:\\pre-launch.exe",
                PreLaunchStartupDirectory = "C:\\preLaunchDir",
                PreLaunchParameters = "--preArg",
                PreLaunchEnvironmentVariables = "var1=val1;var2=val2;",
                PreLaunchStdoutPath = "C:\\pre-launch-stdout.log",
                PreLaunchStderrPath = "C:\\pre-launch-stderr.log",
                PreLaunchTimeoutSeconds = 30,
                PreLaunchRetryAttempts = 0,
                PreLaunchIgnoreFailure = true,
                FailureProgramPath = "C:\\failure-program.exe",
                FailureProgramStartupDirectory = "C:\\failureProgramDir",
                FailureProgramParameters = "--failureProgramArg",
                PreStopExecutablePath = "C:\\pre-stop.exe",
                PreStopStartupDirectory = "C:\\preStopDir",
                PreStopParameters = "--preStopArg",
                PreStopTimeoutSeconds = 20,
                PreStopLogAsError = true,
                PostStopExecutablePath = "C:\\post-stop.exe",
                PostStopStartupDirectory = "C:\\postStopDir",
                PostStopParameters = "--postStopArg",
            };

            _serviceManagerMock
                 .Setup(s => s.InstallServiceAsync(It.Is<InstallServiceOptions>(o =>
                     o.ServiceName == service.Name &&
                     o.Description == service.Description &&
                     o.RealExePath == service.ExecutablePath &&
                     o.WorkingDirectory == service.StartupDirectory &&
                     o.RealArgs == service.Parameters &&
                     o.Username == service.UserAccount &&
                     o.PreLaunchExePath == service.PreLaunchExecutablePath &&
                     o.PreStopTimeout == service.PreStopTimeoutSeconds &&
                     o.PostStopArgs == service.PostStopParameters
                 ), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Success())
                 .Verifiable();

            // Act
            var result = await service.Install("C:\\wrapper", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify();
        }

        [Fact]
        public async Task Install_ShouldCallServiceManagerWithCorrectArguments_NoWrapperExe()
        {
            // Arrange
            var service = new Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                ExecutablePath = @"C:\real.exe",
                EnableSizeRotation = true,
                RotationSize = 10,
                EnableHealthMonitoring = true,
                RecoveryAction = RecoveryAction.RestartService
            };

            _serviceManagerMock
                 .Setup(s => s.InstallServiceAsync(It.Is<InstallServiceOptions>(o =>
                     o.ServiceName == service.Name &&
                     o.EnableSizeRotation == true &&
                     o.RotationSizeInBytes == (ulong)service.RotationSize * 1024 * 1024 &&
                     o.EnableHealthMonitoring == true &&
                     o.RecoveryAction == service.RecoveryAction
                 ), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Success())
                 .Verifiable();

            // Act
            var result = await service.Install(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify();
        }

        [Fact]
        public async Task Install_ShouldCallServiceManagerWithCorrectArguments_IsCLI()
        {
            // Arrange
            var service = new Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                ExecutablePath = @"C:\real.exe"
            };

            _serviceManagerMock
                 .Setup(s => s.InstallServiceAsync(It.Is<InstallServiceOptions>(o =>
                     o.ServiceName == service.Name &&
                     o.WrapperExePath != null && o.WrapperExePath.Contains(".CLI")
                 ), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Success())
                 .Verifiable();

            // Act
            var result = await service.Install(isCLI: true, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify();
        }

        [Fact]
        public async Task Install_ShouldHandleNullStartupDirectoryAndExecutablePath()
        {
            // Arrange
            var service = new Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                ExecutablePath = null!,
                StartupDirectory = null
            };

            _serviceManagerMock
                .Setup(s => s.InstallServiceAsync(It.Is<InstallServiceOptions>(o =>
                    o.ServiceName == service.Name &&
                    o.WorkingDirectory == string.Empty &&
                    o.RealArgs == string.Empty
                ), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success())
                .Verifiable();

            // Act
            var result = await service.Install(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify();
        }

        [Fact]
        public async Task Uninstall_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.UninstallServiceAsync("TestService", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            var result = await service.Uninstall(TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.UninstallServiceAsync("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
