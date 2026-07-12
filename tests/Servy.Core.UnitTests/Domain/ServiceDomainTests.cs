using Moq;
using Servy.Core.Common;
using Servy.Core.Domain;
using Servy.Core.Enums;
using Servy.Core.Services;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StartServiceAsync("TestService", true, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Start(CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.StartServiceAsync("TestService", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Stop_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StopServiceAsync("TestService", true, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Stop(CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.StopServiceAsync("TestService", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Restart_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.RestartServiceAsync("TestService", true, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Restart(CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.RestartServiceAsync("TestService", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetStatus_ShouldReturnNull_WhenServiceNotInstalled()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService", It.IsAny<CancellationToken>())).Returns(false);

            // Act
            var result = service.GetStatus(CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetStatus_ShouldReturnStatus_WhenInstalled()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService", It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(s => s.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);

            // Act
            var result = service.GetStatus(CancellationToken.None);

            // Assert
            Assert.Equal(ServiceControllerStatus.Running, result);
        }

        [Fact]
        public void IsInstalled_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService", It.IsAny<CancellationToken>())).Returns(true);

            // Act
            var result = service.IsInstalled(CancellationToken.None);

            // Assert
            Assert.True(result);
            _serviceManagerMock.Verify(s => s.IsServiceInstalled("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetServiceStartupType_ShouldDelegateToServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.GetServiceStartupType("TestService", It.IsAny<CancellationToken>()))
                .Returns(ServiceStartType.Automatic);

            // Act
            var result = service.GetServiceStartupType(CancellationToken.None);

            // Assert
            Assert.Equal(ServiceStartType.Automatic, result);
            _serviceManagerMock.Verify(s => s.GetServiceStartupType("TestService", It.IsAny<CancellationToken>()), Times.Once);
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
                EnableConsoleUI = false,
                StdoutPath = "C:\\stdout.log",
                StderrPath = "C:\\stderr.log",
                EnableSizeRotation = false,
                RotationSize = 3,
                EnableDateRotation = false,
                DateRotationType = DateRotationType.Daily,
                MaxRotations = 5,
                UseLocalTimeForRotation = false,
                EnableHealthMonitoring = false,
                HeartbeatInterval = 30,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.None,
                RecoveryOnCleanExit = false,
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
                EnvironmentVariables = "env1=val1;",
                ServiceDependencies = "SharedService;",
                PostLaunchExecutablePath = "C:\\post-launch.exe",
                PostLaunchStartupDirectory = "C:\\postLaunchDir",
                PostLaunchParameters = "--postArg",
                EnableDebugLogs = true,
                StartTimeout = 60,
                StopTimeout = 45,
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
                     o.DisplayName == service.DisplayName &&
                     o.Description == service.Description &&
                     o.RealExePath == service.ExecutablePath &&
                     o.WorkingDirectory == service.StartupDirectory &&
                     o.RealArgs == service.Parameters &&
                     o.StartType == service.StartupType &&
                     o.ProcessPriority == service.Priority &&
                     o.EnableConsoleUI == service.EnableConsoleUI &&
                     o.StdoutPath == service.StdoutPath &&
                     o.StderrPath == service.StderrPath &&
                     o.EnableSizeRotation == service.EnableSizeRotation &&
                     o.RotationSizeInBytes == 3 * 1024L * 1024L &&
                     o.EnableDateRotation == service.EnableDateRotation &&
                     o.DateRotationType == service.DateRotationType &&
                     o.MaxRotations == service.MaxRotations &&
                     o.UseLocalTimeForRotation == service.UseLocalTimeForRotation &&
                     o.EnableHealthMonitoring == service.EnableHealthMonitoring &&
                     o.HeartbeatInterval == service.HeartbeatInterval &&
                     o.MaxFailedChecks == service.MaxFailedChecks &&
                     o.RecoveryAction == service.RecoveryAction &&
                     o.RecoveryOnCleanExit == service.RecoveryOnCleanExit &&
                     o.MaxRestartAttempts == service.MaxRestartAttempts &&
                     o.Username == service.UserAccount &&
                     o.Password == service.Password &&
                     o.PreLaunchExePath == service.PreLaunchExecutablePath &&
                     o.PreLaunchWorkingDirectory == service.PreLaunchStartupDirectory &&
                     o.PreLaunchArgs == service.PreLaunchParameters &&
                     o.PreLaunchEnvironmentVariables == service.PreLaunchEnvironmentVariables &&
                     o.PreLaunchStdoutPath == service.PreLaunchStdoutPath &&
                     o.PreLaunchStderrPath == service.PreLaunchStderrPath &&
                     o.PreLaunchTimeout == service.PreLaunchTimeoutSeconds &&
                     o.PreLaunchRetryAttempts == service.PreLaunchRetryAttempts &&
                     o.PreLaunchIgnoreFailure == service.PreLaunchIgnoreFailure &&
                     o.FailureProgramPath == service.FailureProgramPath &&
                     o.FailureProgramWorkingDirectory == service.FailureProgramStartupDirectory &&
                     o.FailureProgramArgs == service.FailureProgramParameters &&
                     o.EnvironmentVariables == service.EnvironmentVariables &&
                     o.ServiceDependencies == service.ServiceDependencies &&
                     o.PostLaunchExePath == service.PostLaunchExecutablePath &&
                     o.PostLaunchWorkingDirectory == service.PostLaunchStartupDirectory &&
                     o.PostLaunchArgs == service.PostLaunchParameters &&
                     o.EnableDebugLogs == service.EnableDebugLogs &&
                     o.StartTimeout == service.StartTimeout &&
                     o.StopTimeout == service.StopTimeout &&
                     o.PreStopExePath == service.PreStopExecutablePath &&
                     o.PreStopWorkingDirectory == service.PreStopStartupDirectory &&
                     o.PreStopArgs == service.PreStopParameters &&
                     o.PreStopTimeout == service.PreStopTimeoutSeconds &&
                     o.PreStopLogAsError == service.PreStopLogAsError &&
                     o.PostStopExePath == service.PostStopExecutablePath &&
                     o.PostStopWorkingDirectory == service.PostStopStartupDirectory &&
                     o.PostStopArgs == service.PostStopParameters
                 ), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Success())
                 .Verifiable();

            // Act
            var result = await service.Install("C:\\wrapper", cancellationToken: CancellationToken.None);

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
                     o.RotationSizeInBytes == (long)service.RotationSize * 1024L * 1024L &&
                     o.EnableHealthMonitoring == true &&
                     o.RecoveryAction == service.RecoveryAction
                 ), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Success())
                 .Verifiable();

            // Act
            var result = await service.Install(cancellationToken: CancellationToken.None);

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
            var result = await service.Install(isCLI: true, cancellationToken: CancellationToken.None);

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
                ExecutablePath = null,
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
            var result = await service.Install(cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify();
        }

        [Fact]
        public async Task Uninstall_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.UninstallServiceAsync("TestService", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Uninstall(CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.UninstallServiceAsync("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}