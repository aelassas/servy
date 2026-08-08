using Moq;
using Servy.Core.Common;
using Servy.Core.Config;
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

        private Core.Domain.Service CreateService(string name = "TestService")
        {
            return new Core.Domain.Service(_serviceManagerMock.Object)
            {
                Name = name,
                ExecutablePath = @"C:\path\app.exe"
            };
        }

        #region Operational Process Control Lifecycle Hook Tests

        [Fact]
        public async Task Start_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StartServiceAsync("TestService", true, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Start(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.StartServiceAsync("TestService", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Start_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to start service."));

            // Act
            var result = await service.Start(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Stop_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StopServiceAsync("TestService", true, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Stop(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.StopServiceAsync("TestService", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Stop_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to stop service."));

            // Act
            var result = await service.Stop(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Restart_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.RestartServiceAsync("TestService", true, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Restart(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.RestartServiceAsync("TestService", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Restart_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(sm => sm.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to restart service."));

            // Act
            var result = await service.Restart(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Metadata Lookup Discovery Queries

        [Fact]
        public void GetStatus_ShouldReturnNull_WhenServiceNotInstalled()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService", It.IsAny<CancellationToken>())).Returns(false);
            _serviceManagerMock.Setup(s => s.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Returns((ServiceControllerStatus?)null);

            // Act
            var result = service.GetStatus(TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
            _serviceManagerMock.Verify(s => s.GetServiceStatus("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetStatus_ShouldReturnStatus_WhenInstalled()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService", It.IsAny<CancellationToken>())).Returns(true);
            _serviceManagerMock.Setup(s => s.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);

            // Act
            var result = service.GetStatus(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ServiceControllerStatus.Running, result);
            _serviceManagerMock.Verify(s => s.GetServiceStatus("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void IsInstalled_ShouldCallServiceManager()
        {
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService", It.IsAny<CancellationToken>())).Returns(true);

            // Act
            var result = service.IsInstalled(TestContext.Current.CancellationToken);

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
            var result = service.GetServiceStartupType(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ServiceStartType.Automatic, result);
            _serviceManagerMock.Verify(s => s.GetServiceStartupType("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Installation Registry Integration Handlers

        [Fact]
        public async Task Install_ShouldCallServiceManagerWithCorrectArguments()
        {
            // Arrange
            var service = new Core.Domain.Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                DisplayName = "TestService",
                Description = "My Test Service",
                ExecutablePath = "C:\\real.exe",
                StartupDirectory = @"C:\MyApp",
                Parameters = "--arg1",
                StartupType = ServiceStartType.Automatic,
                Priority = ProcessPriority.Normal,
                CpuAffinity = "0-3",
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
                HeartbeatUrl = "http://localhost:8080/health",
                HeartbeatUrlTimeoutSeconds = 10,
                EnableHeartbeatUrlFlags = true,
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

            InstallServiceOptions? captured = null;
            _serviceManagerMock
                .Setup(s => s.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Callback<InstallServiceOptions, CancellationToken>((o, _) => captured = o)
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Install("C:\\wrapper", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Equal(service.Name, captured!.ServiceName);
            Assert.Equal(service.DisplayName, captured.DisplayName);
            Assert.Equal(service.Description, captured.Description);
            Assert.Equal(service.ExecutablePath, captured.RealExePath);
            Assert.Equal(service.StartupDirectory, captured.StartupDirectory);
            Assert.Equal(service.Parameters, captured.RealArgs);
            Assert.Equal(service.StartupType, captured.StartType);
            Assert.Equal(service.Priority, captured.ProcessPriority);
            Assert.Equal(service.CpuAffinity, captured.CpuAffinity);
            Assert.Equal(service.EnableConsoleUI, captured.EnableConsoleUI);
            Assert.Equal(service.StdoutPath, captured.StdoutPath);
            Assert.Equal(service.StderrPath, captured.StderrPath);
            Assert.Equal(service.EnableSizeRotation, captured.EnableSizeRotation);
            Assert.Equal(3 * 1024L * 1024L, captured.RotationSizeInBytes);
            Assert.Equal(service.EnableDateRotation, captured.EnableDateRotation);
            Assert.Equal(service.DateRotationType, captured.DateRotationType);
            Assert.Equal(service.MaxRotations, captured.MaxRotations);
            Assert.Equal(service.UseLocalTimeForRotation, captured.UseLocalTimeForRotation);
            Assert.Equal(service.EnableHealthMonitoring, captured.EnableHealthMonitoring);
            Assert.Equal(service.HeartbeatInterval, captured.HeartbeatInterval);
            Assert.Equal(service.MaxFailedChecks, captured.MaxFailedChecks);
            Assert.Equal(service.HeartbeatUrl, captured.HeartbeatUrl);
            Assert.Equal(service.HeartbeatUrlTimeoutSeconds, captured.HeartbeatUrlTimeoutSeconds);
            Assert.Equal(service.EnableHeartbeatUrlFlags, captured.EnableHeartbeatUrlFlags);
            Assert.Equal(service.RecoveryAction, captured.RecoveryAction);
            Assert.Equal(service.RecoveryOnCleanExit, captured.RecoveryOnCleanExit);
            Assert.Equal(service.MaxRestartAttempts, captured.MaxRestartAttempts);
            Assert.Equal(service.UserAccount, captured.Username);
            Assert.Equal(service.Password, captured.Password);
            Assert.Equal(service.PreLaunchExecutablePath, captured.PreLaunchExePath);
            Assert.Equal(service.PreLaunchStartupDirectory, captured.PreLaunchStartupDirectory);
            Assert.Equal(service.PreLaunchParameters, captured.PreLaunchArgs);
            Assert.Equal(service.PreLaunchEnvironmentVariables, captured.PreLaunchEnvironmentVariables);
            Assert.Equal(service.PreLaunchStdoutPath, captured.PreLaunchStdoutPath);
            Assert.Equal(service.PreLaunchStderrPath, captured.PreLaunchStderrPath);
            Assert.Equal(service.PreLaunchTimeoutSeconds, captured.PreLaunchTimeout);
            Assert.Equal(service.PreLaunchRetryAttempts, captured.PreLaunchRetryAttempts);
            Assert.Equal(service.PreLaunchIgnoreFailure, captured.PreLaunchIgnoreFailure);
            Assert.Equal(service.FailureProgramPath, captured.FailureProgramPath);
            Assert.Equal(service.FailureProgramStartupDirectory, captured.FailureProgramStartupDirectory);
            Assert.Equal(service.FailureProgramParameters, captured.FailureProgramArgs);
            Assert.Equal(service.EnvironmentVariables, captured.EnvironmentVariables);
            Assert.Equal(service.ServiceDependencies, captured.ServiceDependencies);
            Assert.Equal(service.PostLaunchExecutablePath, captured.PostLaunchExePath);
            Assert.Equal(service.PostLaunchStartupDirectory, captured.PostLaunchStartupDirectory);
            Assert.Equal(service.PostLaunchParameters, captured.PostLaunchArgs);
            Assert.Equal(service.EnableDebugLogs, captured.EnableDebugLogs);
            Assert.Equal(service.StartTimeout, captured.StartTimeout);
            Assert.Equal(service.StopTimeout, captured.StopTimeout);
            Assert.Equal(service.PreStopExecutablePath, captured.PreStopExePath);
            Assert.Equal(service.PreStopStartupDirectory, captured.PreStopStartupDirectory);
            Assert.Equal(service.PreStopParameters, captured.PreStopArgs);
            Assert.Equal(service.PreStopTimeoutSeconds, captured.PreStopTimeout);
            Assert.Equal(service.PreStopLogAsError, captured.PreStopLogAsError);
            Assert.Equal(service.PostStopExecutablePath, captured.PostStopExePath);
            Assert.Equal(service.PostStopStartupDirectory, captured.PostStopStartupDirectory);
            Assert.Equal(service.PostStopParameters, captured.PostStopArgs);
        }

        [Fact]
        public async Task Install_WithWrapperExeDir_HonoursItInDebugAndIgnoresItInRelease()
        {
            // Arrange
            var service = CreateService();
            InstallServiceOptions? captured = null;

            _serviceManagerMock
                .Setup(s => s.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Callback<InstallServiceOptions, CancellationToken>((o, _) => captured = o)
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Install(@"C:\customWrapper", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);

#if DEBUG
            Assert.Equal(Path.Combine(@"C:\customWrapper", AppConfig.ServyServiceUIExe), captured!.WrapperExePath);
#else
            Assert.Equal(Path.Combine(AppConfig.ProgramDataPath, AppConfig.ServyServiceUIExe), captured!.WrapperExePath);
#endif
        }

        [Fact]
        public async Task Install_WithRunAsLocalSystem_DoesNotForwardCredentials()
        {
            // Arrange
            var service = new Core.Domain.Service(_serviceManagerMock.Object)
            {
                Name = "LocalSystemService",
                ExecutablePath = @"C:\real.exe",
                RunAsLocalSystem = true,
                UserAccount = @".\user",
                Password = "secretPassword"
            };

            InstallServiceOptions? captured = null;
            _serviceManagerMock
                .Setup(s => s.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Callback<InstallServiceOptions, CancellationToken>((o, _) => captured = o)
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Install(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Null(captured!.Username);
            Assert.Null(captured.Password);
        }

        [Fact]
        public async Task Install_ShouldCallServiceManagerWithCorrectArguments_NoWrapperExe()
        {
            // Arrange
            var service = new Core.Domain.Service(_serviceManagerMock.Object)
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
                    o.RotationSizeInBytes == AppConfig.ToBytes(Math.Max(1, service.RotationSize)) &&
                    o.EnableHealthMonitoring == true &&
                    o.RecoveryAction == service.RecoveryAction &&

                    // Symmetrical Hardening: Firmly assert that the fallback mechanism routes to
                    // the default UI executable path and does not leak the alternative CLI variant.
                    !string.IsNullOrWhiteSpace(o.WrapperExePath) &&
                    o.WrapperExePath.Contains(AppConfig.ServyServiceUIExe) &&
                    o.WrapperExePath.IndexOf(".CLI", StringComparison.OrdinalIgnoreCase) == -1
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
        public async Task Install_WithZeroRotationSize_ClampsToMinimumOneMegabyte()
        {
            // Arrange
            var service = new Core.Domain.Service(_serviceManagerMock.Object)
            {
                Name = "TestServiceZeroSize",
                ExecutablePath = @"C:\real.exe",
                EnableSizeRotation = true,
                RotationSize = 0, // Lower bounds target value to test the Math.Max floor clamp logic
                EnableHealthMonitoring = true,
                RecoveryAction = RecoveryAction.RestartService
            };

            _serviceManagerMock
                .Setup(s => s.InstallServiceAsync(It.Is<InstallServiceOptions>(o =>
                    o.ServiceName == service.Name &&
                    o.EnableSizeRotation == true &&
                    o.RotationSizeInBytes == AppConfig.ToBytes(Math.Max(1, service.RotationSize)) && // Should compute to 1 MB in bytes
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
            var service = new Core.Domain.Service(_serviceManagerMock.Object)
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
            var service = new Core.Domain.Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                ExecutablePath = null!,
                StartupDirectory = null
            };

            _serviceManagerMock
                .Setup(s => s.InstallServiceAsync(It.Is<InstallServiceOptions>(o =>
                    o.ServiceName == service.Name &&
                    o.RealExePath == null &&
                    o.StartupDirectory == null &&
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
            // Arrange
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.UninstallServiceAsync("TestService", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await service.Uninstall(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(s => s.UninstallServiceAsync("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}
