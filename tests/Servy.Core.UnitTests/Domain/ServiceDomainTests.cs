using Moq;
using Servy.Core.Config;
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
        public void Start_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StartService("TestService")).Returns(true);

            var result = service.Start();

            Assert.True(result);
            _serviceManagerMock.Verify(s => s.StartService("TestService"), Times.Once);
        }

        [Fact]
        public void Stop_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.StopService("TestService")).Returns(true);

            var result = service.Stop();

            Assert.True(result);
            _serviceManagerMock.Verify(s => s.StopService("TestService"), Times.Once);
        }

        [Fact]
        public void Restart_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.RestartService("TestService")).Returns(true);

            var result = service.Restart();

            Assert.True(result);
            _serviceManagerMock.Verify(s => s.RestartService("TestService"), Times.Once);
        }

        [Fact]
        public void GetStatus_ShouldReturnNull_WhenServiceNotInstalled()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService")).Returns(false);

            var result = service.GetStatus();

            Assert.Null(result);
        }

        [Fact]
        public void GetStatus_ShouldReturnStatus_WhenInstalled()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.IsServiceInstalled("TestService")).Returns(true);
            _serviceManagerMock.Setup(s => s.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);

            var result = service.GetStatus();

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
                EnableRotation = false,
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
            };

            _serviceManagerMock
                 .Setup(s => s.InstallService(
                     service.Name,
                     service.Description,
                     It.IsAny<string>(),          // wrapperExePath
                     service.ExecutablePath,
                     service.StartupDirectory,          // workingDirectory
                     service.Parameters ?? string.Empty,
                     service.StartupType,
                     service.Priority,
                     null,                        // stdoutPath
                     null,                        // stderrPath
                     false,
                     (ulong)service.RotationSize * 1024 * 1024,  // rotationSizeInBytes
                     false,
                     service.HeartbeatInterval, // heartbeatInterval
                     service.MaxFailedChecks,   // maxFailedChecks
                     service.RecoveryAction,
                     service.MaxRestartAttempts,
                     null,                        // environmentVariables
                     null,                        // serviceDependencies
                     service.UserAccount,                        // username
                     service.Password,                        // password
                     service.PreLaunchExecutablePath,
                     service.PreLaunchStartupDirectory,
                     service.PreLaunchParameters,
                     service.PreLaunchEnvironmentVariables,
                     service.PreLaunchStdoutPath,
                     service.PreLaunchStderrPath,
                     service.PreLaunchTimeoutSeconds,
                     service.PreLaunchRetryAttempts,
                     service.PreLaunchIgnoreFailure,

                     service.FailureProgramPath,
                     service.FailureProgramStartupDirectory,
                     service.FailureProgramParameters,

                     service.PostLaunchExecutablePath,
                     service.PostLaunchStartupDirectory,
                     service.PostLaunchParameters,

                     service.EnableDebugLogs,
                     service.DisplayName,
                     service.MaxRotations,

                     service.EnableDateRotation,
                     service.DateRotationType,

                     service.StartTimeout,
                     service.StopTimeout
                 ))
                 .ReturnsAsync(true)
                 .Verifiable();


            // Act
            var result = await service.Install("C:\\wrapper");

            // Assert
            Assert.True(result);
            _serviceManagerMock.Verify();
        }

        [Fact]
        public async Task Install_ShouldCallServiceManagerWithCorrectArguments_NoWrapperExe()
        {
            // Arrange
            var service = new Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                DisplayName = "TestService",
                Description = null,
                ExecutablePath = @"C:\real.exe",
                StartupDirectory = null,
                Parameters = null,
                StartupType = ServiceStartType.Automatic,
                Priority = ProcessPriority.Normal,
                EnableRotation = true,
                RotationSize = 10 * 1024 * 1024,
                HeartbeatInterval = 30,
                EnableHealthMonitoring = true,
                RecoveryAction = RecoveryAction.RestartService,
                MaxFailedChecks = 1,
                MaxRestartAttempts = 3,
                RunAsLocalSystem = true,
                PreLaunchExecutablePath = null,
                PreLaunchStartupDirectory = null,
                PreLaunchParameters = null,
                PreLaunchEnvironmentVariables = null,
                PreLaunchStdoutPath = null,
                PreLaunchStderrPath = null,
                PreLaunchTimeoutSeconds = 30,
                PreLaunchRetryAttempts = 0,
                PreLaunchIgnoreFailure = true,

                FailureProgramPath = null,
                FailureProgramStartupDirectory = null,
                FailureProgramParameters = null,
            };

            _serviceManagerMock
                 .Setup(s => s.InstallService(
                     service.Name,
                     It.IsAny<string>(),
                     It.IsAny<string>(),          // wrapperExePath
                     service.ExecutablePath,
                     It.IsAny<string>(),    // workingDirectory
                     It.IsAny<string>(),
                     service.StartupType,
                     service.Priority,
                     null,                        // stdoutPath
                     null,                        // stderrPath
                     true,
                     (ulong)service.RotationSize * 1024 * 1024,  // rotationSizeInBytes
                     true,
                     service.HeartbeatInterval,   // heartbeatInterval
                     service.MaxFailedChecks,     // maxFailedChecks
                     service.RecoveryAction,
                     service.MaxRestartAttempts,
                     null,                        // environmentVariables
                     null,                        // serviceDependencies
                     null,                        // username
                     null,                        // password
                     service.PreLaunchExecutablePath,
                     service.PreLaunchStartupDirectory,
                     service.PreLaunchParameters,
                     service.PreLaunchEnvironmentVariables,
                     service.PreLaunchStdoutPath,
                     service.PreLaunchStderrPath,
                     service.PreLaunchTimeoutSeconds,
                     service.PreLaunchRetryAttempts,
                     service.PreLaunchIgnoreFailure,

                     service.FailureProgramPath,
                     service.FailureProgramStartupDirectory,
                     service.FailureProgramParameters,

                     service.PostLaunchExecutablePath,
                     service.PostLaunchStartupDirectory,
                     service.PostLaunchParameters,

                     service.EnableDebugLogs,
                     service.DisplayName,
                     service.MaxRotations,

                     service.EnableDateRotation,
                     service.DateRotationType,

                     service.StartTimeout,
                     service.StopTimeout
                 ))
                 .ReturnsAsync(true)
                 .Verifiable();


            // Act
            var result = await service.Install();

            // Assert
            Assert.True(result);
            _serviceManagerMock.Verify();
        }

        [Fact]
        public async Task Install_ShouldHandleNullStartupDirectoryAndExecutablePath()
        {
            // Arrange
            var service = new Service(_serviceManagerMock.Object)
            {
                Name = "TestService",
                DisplayName = "TestService",
                Description = null,
                ExecutablePath = null, // forces Path.GetDirectoryName(null) => null
                StartupDirectory = null,
                Parameters = null,
                StartupType = ServiceStartType.Manual,
                Priority = ProcessPriority.Normal,
                EnableRotation = false,
                EnableHealthMonitoring = false,
                RecoveryAction = RecoveryAction.None,
                MaxRestartAttempts = 0,
                RunAsLocalSystem = true
            };

            _serviceManagerMock
                .Setup(s => s.InstallService(
                    service.Name,                        // serviceName
                    It.IsAny<string>(),                 // description
                    It.IsAny<string>(),                   // wrapperExePath
                    service.ExecutablePath,              // realExePath
                    string.Empty,                        // workingDirectory
                    string.Empty,                        // realArgs
                    service.StartupType,                 // startType
                    service.Priority,                    // processPriority
                    null,                                // stdoutPath
                    null,                                // stderrPath
                    false,
                    (ulong)service.RotationSize * 1024 * 1024,  // rotationSizeInBytes
                    false,
                    service.HeartbeatInterval,           // heartbeatInterval
                    service.MaxFailedChecks,             // maxFailedChecks
                    service.RecoveryAction,              // recoveryAction
                    service.MaxRestartAttempts,          // maxRestartAttempts
                    null,                                // environmentVariables
                    null,                                // serviceDependencies
                    null,                                // username
                    null,                                // password
                    null,                                // preLaunchExePath
                    null,                                // preLaunchWorkingDirectory
                    null,                                // preLaunchArgs
                    null,                                // preLaunchEnvironmentVariables
                    null,                                // preLaunchStdoutPath
                    null,                                // preLaunchStderrPath
                    It.IsAny<int>(),                     // preLaunchTimeout
                    It.IsAny<int>(),                     // preLaunchRetryAttempts
                    It.IsAny<bool>(),                    // preLaunchIgnoreFailure
                    null,                                // failureProgramPath
                    null,                                // failureProgramWorkingDirectory
                    null,                                // failureProgramArgs
                    null,                                // postLaunchExePath
                    null,                                // postLaunchWorkingDirectory
                    null,                                // postLaunchArgs
                    It.IsAny<bool>(),                    // enableDebugLogs
                    service.DisplayName,                 // displayName
                    service.MaxRotations,                 // maxRotations

                     service.EnableDateRotation,
                     service.DateRotationType,

                     service.StartTimeout,
                     service.StopTimeout
                ))
                .ReturnsAsync(true)
                .Verifiable();


            // Act
            var result = await service.Install();

            // Assert
            Assert.True(result);
            _serviceManagerMock.Verify();
        }


        [Fact]
        public async Task Uninstall_ShouldCallServiceManager()
        {
            var service = CreateService();
            _serviceManagerMock.Setup(s => s.UninstallService("TestService")).ReturnsAsync(true);

            var result = await service.Uninstall();

            Assert.True(result);
            _serviceManagerMock.Verify(s => s.UninstallService("TestService"), Times.Once);
        }
    }
}
