using Moq;
using Servy.Core.Config;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Mappers;
using Servy.Core.Services;
using System.ServiceProcess;
using System.Threading;
using Xunit;

namespace Servy.Core.UnitTests.Mappers
{
    public class ServiceMapperTests
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Service _service;

        public ServiceMapperTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
            _service = new Service(_serviceManagerMock.Object)
            {
                Name = "TestService"
            };
        }


        [Fact]
        public void ToDto_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var service = new Service(null)
            {
                Name = "MyService",
                Description = "Test Service",
                ExecutablePath = @"C:\app\service.exe",
                StartupDirectory = @"C:\app",
                Parameters = "-arg1 -arg2",
                StartupType = ServiceStartType.Automatic,
                Priority = ProcessPriority.High,
                StdoutPath = "stdout.log",
                StderrPath = "stderr.log",
                EnableRotation = true,
                RotationSize = 2048,
                EnableDateRotation = true,
                DateRotationType = DateRotationType.Daily,
                MaxRotations = 5,
                EnableDebugLogs = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 60,
                MaxFailedChecks = 5,
                RecoveryAction = RecoveryAction.RestartService,
                MaxRestartAttempts = 10,
                FailureProgramPath = @"C:\apps\failure_prog.exe",
                FailureProgramParameters = "--param1",
                FailureProgramStartupDirectory = @"C:\apps",
                EnvironmentVariables = "KEY1=VALUE1;KEY2=VALUE2",
                ServiceDependencies = "Dep1,Dep2",
                RunAsLocalSystem = false,
                UserAccount = "User1",
                Password = "Secret",
                PreLaunchExecutablePath = @"C:\prelaunch.exe",
                PreLaunchStartupDirectory = @"C:\prelaunch",
                PreLaunchParameters = "-pre1",
                PreLaunchEnvironmentVariables = "PRE1=VAL1",
                PreLaunchStdoutPath = "pre_stdout.log",
                PreLaunchStderrPath = "pre_stderr.log",
                PreLaunchTimeoutSeconds = 45,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = @"C:\apps\post_launch\post_launch.exe",
                PostLaunchParameters = "--post-param1",
                PostLaunchStartupDirectory = @"C:\apps\post_launch\",
            };

            // Act
            var dto = ServiceMapper.ToDto(service);

            // Assert
            Assert.Equal(0, dto.Id); // Always 0 for inserts
            Assert.Equal(service.Name, dto.Name);
            Assert.Equal(service.Description, dto.Description);
            Assert.Equal(service.ExecutablePath, dto.ExecutablePath);
            Assert.Equal(service.StartupDirectory, dto.StartupDirectory);
            Assert.Equal(service.Parameters, dto.Parameters);
            Assert.Equal((int)service.StartupType, dto.StartupType);
            Assert.Equal((int)service.Priority, dto.Priority);
            Assert.Equal(service.StdoutPath, dto.StdoutPath);
            Assert.Equal(service.StderrPath, dto.StderrPath);
            Assert.Equal(service.EnableRotation, dto.EnableRotation);
            Assert.Equal(service.RotationSize, dto.RotationSize);
            Assert.Equal(service.EnableDateRotation, dto.EnableDateRotation);
            Assert.Equal((int)service.DateRotationType, dto.DateRotationType);
            Assert.Equal(service.MaxRotations, dto.MaxRotations);
            Assert.Equal(service.EnableDebugLogs, dto.EnableDebugLogs);
            Assert.Equal(service.EnableHealthMonitoring, dto.EnableHealthMonitoring);
            Assert.Equal(service.HeartbeatInterval, dto.HeartbeatInterval);
            Assert.Equal(service.MaxFailedChecks, dto.MaxFailedChecks);
            Assert.Equal((int)service.RecoveryAction, dto.RecoveryAction);
            Assert.Equal(service.MaxRestartAttempts, dto.MaxRestartAttempts);
            Assert.Equal(service.FailureProgramPath, dto.FailureProgramPath);
            Assert.Equal(service.FailureProgramStartupDirectory, dto.FailureProgramStartupDirectory);
            Assert.Equal(service.FailureProgramParameters, dto.FailureProgramParameters);
            Assert.Equal(service.EnvironmentVariables, dto.EnvironmentVariables);
            Assert.Equal(service.ServiceDependencies, dto.ServiceDependencies);
            Assert.Equal(service.RunAsLocalSystem, dto.RunAsLocalSystem);
            Assert.Equal(service.UserAccount, dto.UserAccount);
            Assert.Equal(service.Password, dto.Password);
            Assert.Equal(service.PreLaunchExecutablePath, dto.PreLaunchExecutablePath);
            Assert.Equal(service.PreLaunchStartupDirectory, dto.PreLaunchStartupDirectory);
            Assert.Equal(service.PreLaunchParameters, dto.PreLaunchParameters);
            Assert.Equal(service.PreLaunchEnvironmentVariables, dto.PreLaunchEnvironmentVariables);
            Assert.Equal(service.PreLaunchStdoutPath, dto.PreLaunchStdoutPath);
            Assert.Equal(service.PreLaunchStderrPath, dto.PreLaunchStderrPath);
            Assert.Equal(service.PreLaunchTimeoutSeconds, dto.PreLaunchTimeoutSeconds);
            Assert.Equal(service.PreLaunchRetryAttempts, dto.PreLaunchRetryAttempts);
            Assert.Equal(service.PreLaunchIgnoreFailure, dto.PreLaunchIgnoreFailure);
            Assert.Equal(service.PostLaunchExecutablePath, dto.PostLaunchExecutablePath);
            Assert.Equal(service.PostLaunchStartupDirectory, dto.PostLaunchStartupDirectory);
            Assert.Equal(service.PostLaunchParameters, dto.PostLaunchParameters);
        }

        [Fact]
        public void ToDomain_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "MyService",
                Description = "Test Service",
                ExecutablePath = @"C:\app\service.exe",
                StartupDirectory = @"C:\app",
                Parameters = "-arg1 -arg2",
                StartupType = (int)ServiceStartType.Manual,
                Priority = (int)ProcessPriority.BelowNormal,
                StdoutPath = "stdout.log",
                StderrPath = "stderr.log",
                EnableRotation = true,
                RotationSize = 4096,
                EnableDateRotation = true,
                DateRotationType = (int)DateRotationType.Weekly,
                MaxRotations = 5,
                EnableDebugLogs = true,
                EnableHealthMonitoring = false,
                HeartbeatInterval = 90,
                MaxFailedChecks = 7,
                RecoveryAction = (int)RecoveryAction.None,
                MaxRestartAttempts = 20,
                FailureProgramPath = @"C:\apps\failure_prog.exe",
                FailureProgramParameters = "--param1",
                FailureProgramStartupDirectory = @"C:\apps",
                EnvironmentVariables = "KEY1=VALUE1",
                ServiceDependencies = "DepA,DepB",
                RunAsLocalSystem = true,
                UserAccount = "User2",
                Password = "TopSecret",
                PreLaunchExecutablePath = @"C:\prelaunch.exe",
                PreLaunchStartupDirectory = @"C:\prelaunch",
                PreLaunchParameters = "-pre2",
                PreLaunchEnvironmentVariables = "PRE2=VAL2",
                PreLaunchStdoutPath = "pre_stdout.log",
                PreLaunchStderrPath = "pre_stderr.log",
                PreLaunchTimeoutSeconds = 50,
                PreLaunchRetryAttempts = 3,
                PreLaunchIgnoreFailure = false,
                PostLaunchExecutablePath = @"C:\apps\post_launch\post_launch.exe",
                PostLaunchParameters = "--post-param1",
                PostLaunchStartupDirectory = @"C:\apps\post_launch\",
            };

            // Act
            var service = ServiceMapper.ToDomain(null, dto);

            // Assert
            Assert.Equal(dto.Name, service.Name);
            Assert.Equal(dto.Description, service.Description);
            Assert.Equal(dto.ExecutablePath, service.ExecutablePath);
            Assert.Equal(dto.StartupDirectory, service.StartupDirectory);
            Assert.Equal(dto.Parameters, service.Parameters);
            Assert.Equal((ServiceStartType)dto.StartupType, service.StartupType);
            Assert.Equal((ProcessPriority)dto.Priority, service.Priority);
            Assert.Equal(dto.StdoutPath, service.StdoutPath);
            Assert.Equal(dto.StderrPath, service.StderrPath);
            Assert.Equal(dto.EnableRotation, service.EnableRotation);
            Assert.Equal(dto.RotationSize, service.RotationSize);
            Assert.Equal(dto.EnableDateRotation, service.EnableDateRotation);
            Assert.Equal(dto.DateRotationType, (int)service.DateRotationType);
            Assert.Equal(dto.MaxRotations, service.MaxRotations);
            Assert.Equal(dto.EnableDebugLogs, service.EnableDebugLogs);
            Assert.Equal(dto.EnableHealthMonitoring, service.EnableHealthMonitoring);
            Assert.Equal(dto.HeartbeatInterval, service.HeartbeatInterval);
            Assert.Equal(dto.MaxFailedChecks, service.MaxFailedChecks);
            Assert.Equal((RecoveryAction)dto.RecoveryAction, service.RecoveryAction);
            Assert.Equal(dto.MaxRestartAttempts, service.MaxRestartAttempts);
            Assert.Equal(dto.FailureProgramPath, service.FailureProgramPath);
            Assert.Equal(dto.FailureProgramStartupDirectory, service.FailureProgramStartupDirectory);
            Assert.Equal(dto.FailureProgramParameters, service.FailureProgramParameters);
            Assert.Equal(dto.EnvironmentVariables, service.EnvironmentVariables);
            Assert.Equal(dto.ServiceDependencies, service.ServiceDependencies);
            Assert.Equal(dto.RunAsLocalSystem, service.RunAsLocalSystem);
            Assert.Equal(dto.UserAccount, service.UserAccount);
            Assert.Equal(dto.Password, service.Password);
            Assert.Equal(dto.PreLaunchExecutablePath, service.PreLaunchExecutablePath);
            Assert.Equal(dto.PreLaunchStartupDirectory, service.PreLaunchStartupDirectory);
            Assert.Equal(dto.PreLaunchParameters, service.PreLaunchParameters);
            Assert.Equal(dto.PreLaunchEnvironmentVariables, service.PreLaunchEnvironmentVariables);
            Assert.Equal(dto.PreLaunchStdoutPath, service.PreLaunchStdoutPath);
            Assert.Equal(dto.PreLaunchStderrPath, service.PreLaunchStderrPath);
            Assert.Equal(dto.PreLaunchTimeoutSeconds, service.PreLaunchTimeoutSeconds);
            Assert.Equal(dto.PreLaunchRetryAttempts, service.PreLaunchRetryAttempts);
            Assert.Equal(dto.PreLaunchIgnoreFailure, service.PreLaunchIgnoreFailure);
            Assert.Equal(dto.PostLaunchExecutablePath, service.PostLaunchExecutablePath);
            Assert.Equal(dto.PostLaunchStartupDirectory, service.PostLaunchStartupDirectory);
            Assert.Equal(dto.PostLaunchParameters, service.PostLaunchParameters);
        }

        [Fact]
        public void ToDomain_AllNullablesNull_UsesDefaults()
        {
            var dto = new ServiceDto
            {
                Name = "MyService",
                Description = "Desc",
                ExecutablePath = "C:\\service.exe",
                StartupDirectory = "C:\\",
                Parameters = "",
                StartupType = null,
                Priority = null,
                StdoutPath = "stdout.log",
                StderrPath = "stderr.log",
                EnableRotation = null,
                RotationSize = null,
                EnableDateRotation = null,
                DateRotationType = null,
                MaxRotations = null,
                EnableDebugLogs = null,
                EnableHealthMonitoring = null,
                HeartbeatInterval = null,
                MaxFailedChecks = null,
                RecoveryAction = null,
                MaxRestartAttempts = null,
                FailureProgramPath = null,
                FailureProgramStartupDirectory = null,
                FailureProgramParameters = null,
                EnvironmentVariables = null,
                ServiceDependencies = null,
                RunAsLocalSystem = null,
                UserAccount = null,
                Password = null,
                PreLaunchExecutablePath = null,
                PreLaunchStartupDirectory = null,
                PreLaunchParameters = null,
                PreLaunchEnvironmentVariables = null,
                PreLaunchStdoutPath = null,
                PreLaunchStderrPath = null,
                PreLaunchTimeoutSeconds = null,
                PreLaunchRetryAttempts = null,
                PreLaunchIgnoreFailure = null,
                PostLaunchExecutablePath = null,
                PostLaunchStartupDirectory = null,
                PostLaunchParameters = null,
            };

            var service = ServiceMapper.ToDomain(null, dto);

            Assert.Equal(dto.Name, service.Name);
            Assert.Equal(dto.Description, service.Description);
            Assert.Equal(dto.ExecutablePath, service.ExecutablePath);
            Assert.Equal(ServiceStartType.Automatic, service.StartupType); // default branch
            Assert.Equal(ProcessPriority.Normal, service.Priority); // default branch
            Assert.False(service.EnableRotation); // default branch
            Assert.Equal(AppConfig.DefaultRotationSize, service.RotationSize); // default branch
            Assert.False(service.EnableDateRotation); // default branch
            Assert.Equal(DateRotationType.Daily, service.DateRotationType); // default branch
            Assert.Equal(AppConfig.DefaultMaxRotations, service.MaxRotations);
            Assert.False(service.EnableHealthMonitoring);
            Assert.Equal(AppConfig.DefaultHeartbeatInterval, service.HeartbeatInterval);
            Assert.Equal(AppConfig.DefaultMaxFailedChecks, service.MaxFailedChecks);
            Assert.Equal(RecoveryAction.RestartService, service.RecoveryAction); // default branch
            Assert.Equal(AppConfig.DefaultMaxRestartAttempts, service.MaxRestartAttempts);
            Assert.True(service.RunAsLocalSystem);
            Assert.Equal(AppConfig.DefaultPreLaunchTimeoutSeconds, service.PreLaunchTimeoutSeconds);
            Assert.Equal(AppConfig.DefaultPreLaunchRetryAttempts, service.PreLaunchRetryAttempts);
            Assert.False(service.PreLaunchIgnoreFailure);
        }

        [Fact]
        public void ToDomain_AllValuesSet_UsesDtoValues()
        {
            var dto = new ServiceDto
            {
                Name = "MyService",
                Description = "Desc",
                ExecutablePath = "C:\\service.exe",
                StartupDirectory = "C:\\",
                Parameters = "-arg",
                StartupType = (int)ServiceStartType.Manual,
                Priority = (int)ProcessPriority.High,
                StdoutPath = "stdout.log",
                StderrPath = "stderr.log",
                EnableRotation = true,
                RotationSize = 1234,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 99,
                MaxFailedChecks = 5,
                RecoveryAction = (int)RecoveryAction.RestartService,
                MaxRestartAttempts = 7,
                EnvironmentVariables = "key=val",
                ServiceDependencies = "dep1;dep2",
                RunAsLocalSystem = false,
                UserAccount = "user",
                Password = "pwd",
                PreLaunchExecutablePath = "pre.exe",
                PreLaunchStartupDirectory = "C:\\pre",
                PreLaunchParameters = "-prearg",
                PreLaunchEnvironmentVariables = "prekey=preval",
                PreLaunchStdoutPath = "preout.log",
                PreLaunchStderrPath = "preerr.log",
                PreLaunchTimeoutSeconds = 77,
                PreLaunchRetryAttempts = 9,
                PreLaunchIgnoreFailure = true
            };

            var service = ServiceMapper.ToDomain(null, dto);

            Assert.Equal(ServiceStartType.Manual, service.StartupType);
            Assert.Equal(ProcessPriority.High, service.Priority);
            Assert.True(service.EnableRotation);
            Assert.Equal(1234, service.RotationSize);
            Assert.True(service.EnableHealthMonitoring);
            Assert.Equal(99, service.HeartbeatInterval);
            Assert.Equal(5, service.MaxFailedChecks);
            Assert.Equal(RecoveryAction.RestartService, service.RecoveryAction);
            Assert.Equal(7, service.MaxRestartAttempts);
            Assert.False(service.RunAsLocalSystem);
            Assert.Equal(77, service.PreLaunchTimeoutSeconds);
            Assert.Equal(9, service.PreLaunchRetryAttempts);
            Assert.True(service.PreLaunchIgnoreFailure);

            Assert.Equal(dto.EnvironmentVariables, service.EnvironmentVariables);
            Assert.Equal(dto.ServiceDependencies, service.ServiceDependencies);
            Assert.Equal(dto.UserAccount, service.UserAccount);
            Assert.Equal(dto.Password, service.Password);
            Assert.Equal(dto.PreLaunchExecutablePath, service.PreLaunchExecutablePath);
            Assert.Equal(dto.PreLaunchStartupDirectory, service.PreLaunchStartupDirectory);
            Assert.Equal(dto.PreLaunchParameters, service.PreLaunchParameters);
            Assert.Equal(dto.PreLaunchEnvironmentVariables, service.PreLaunchEnvironmentVariables);
            Assert.Equal(dto.PreLaunchStdoutPath, service.PreLaunchStdoutPath);
            Assert.Equal(dto.PreLaunchStderrPath, service.PreLaunchStderrPath);
        }

        [Fact]
        public void Start_ReturnsTrue_WhenServiceManagerReturnsTrue()
        {
            _serviceManagerMock.Setup(sm => sm.StartService("TestService")).Returns(true);

            var result = _service.Start();

            Assert.True(result);
            _serviceManagerMock.Verify(sm => sm.StartService("TestService"), Times.Once);
        }

        [Fact]
        public void Start_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            _serviceManagerMock.Setup(sm => sm.StartService("TestService")).Returns(false);

            var result = _service.Start();

            Assert.False(result);
            _serviceManagerMock.Verify(sm => sm.StartService("TestService"), Times.Once);
        }

        [Fact]
        public void Stop_ReturnsTrue_WhenServiceManagerReturnsTrue()
        {
            _serviceManagerMock.Setup(sm => sm.StopService("TestService")).Returns(true);

            var result = _service.Stop();

            Assert.True(result);
            _serviceManagerMock.Verify(sm => sm.StopService("TestService"), Times.Once);
        }

        [Fact]
        public void Stop_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            _serviceManagerMock.Setup(sm => sm.StopService("TestService")).Returns(false);

            var result = _service.Stop();

            Assert.False(result);
            _serviceManagerMock.Verify(sm => sm.StopService("TestService"), Times.Once);
        }

        [Fact]
        public void Restart_ReturnsTrue_WhenServiceManagerReturnsTrue()
        {
            _serviceManagerMock.Setup(sm => sm.RestartService("TestService")).Returns(true);

            var result = _service.Restart();

            Assert.True(result);
            _serviceManagerMock.Verify(sm => sm.RestartService("TestService"), Times.Once);
        }

        [Fact]
        public void Restart_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            _serviceManagerMock.Setup(sm => sm.RestartService("TestService")).Returns(false);

            var result = _service.Restart();

            Assert.False(result);
            _serviceManagerMock.Verify(sm => sm.RestartService("TestService"), Times.Once);
        }

        [Fact]
        public void IsInstalled_ReturnsTrue_WhenServiceManagerReturnsTrue()
        {
            _serviceManagerMock.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);

            var result = _service.IsInstalled();

            Assert.True(result);
            _serviceManagerMock.Verify(sm => sm.IsServiceInstalled("TestService"), Times.Once);
        }

        [Fact]
        public void IsInstalled_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            _serviceManagerMock.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(false);

            var result = _service.IsInstalled();

            Assert.False(result);
            _serviceManagerMock.Verify(sm => sm.IsServiceInstalled("TestService"), Times.Once);
        }

        [Fact]
        public void GetStatus_ReturnsStatus_WhenServiceIsInstalled()
        {
            _serviceManagerMock.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(true);
            _serviceManagerMock.Setup(sm => sm.GetServiceStatus("TestService", It.IsAny<CancellationToken>())).Returns(ServiceControllerStatus.Running);

            var result = _service.GetStatus();

            Assert.Equal(ServiceControllerStatus.Running, result);
            _serviceManagerMock.Verify(sm => sm.IsServiceInstalled("TestService"), Times.Once);
            _serviceManagerMock.Verify(sm => sm.GetServiceStatus("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetStatus_ReturnsNull_WhenServiceIsNotInstalled()
        {
            _serviceManagerMock.Setup(sm => sm.IsServiceInstalled("TestService")).Returns(false);

            var result = _service.GetStatus();

            Assert.Null(result);
            _serviceManagerMock.Verify(sm => sm.IsServiceInstalled("TestService"), Times.Once);
            _serviceManagerMock.Verify(sm => sm.GetServiceStatus(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void GetServiceStartupType_ReturnsStartupType()
        {
            _serviceManagerMock.Setup(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>())).Returns(ServiceStartType.Automatic);

            var result = _service.GetServiceStartupType();

            Assert.Equal(ServiceStartType.Automatic, result);
            _serviceManagerMock.Verify(sm => sm.GetServiceStartupType("TestService", It.IsAny<CancellationToken>()), Times.Once);
        }

    }
}

