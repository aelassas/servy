using Moq;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Mappers;
using Servy.Core.Services;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
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
        public void ToDto_WhenDomainIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Service nullDomain = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                ServiceMapper.ToDto(nullDomain)
            );

            // Verify the correct parameter name is reported
            Assert.Equal("domain", exception.ParamName);
        }

        [Fact]
        public void ToDto_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var service = new Service(_serviceManagerMock.Object)
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
                EnableSizeRotation = true,
                RotationSize = 2048,
                EnableDateRotation = true,
                DateRotationType = DateRotationType.Daily,
                MaxRotations = 5,
                UseLocalTimeForRotation = true, // Added
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
                StartTimeout = 40, // Added
                StopTimeout = 50,  // Added
                PreStopExecutablePath = @"C:\prestop.exe", // Added
                PreStopStartupDirectory = @"C:\prestop",    // Added
                PreStopParameters = "-pre-stop",           // Added
                PreStopTimeoutSeconds = 15,                // Added
                PreStopLogAsError = true,                 // Added
                PostStopExecutablePath = @"C:\poststop.exe", // Added
                PostStopStartupDirectory = @"C:\poststop",   // Added
                PostStopParameters = "-post-stop"           // Added
            };

            // Act
            var dto = ServiceMapper.ToDto(service, 123); // Passing an ID to test the mapping

            // Assert
            Assert.Equal(123, dto.Id);
            Assert.Equal(service.Name, dto.Name);
            Assert.Equal(service.Description, dto.Description);
            Assert.Equal(service.ExecutablePath, dto.ExecutablePath);
            Assert.Equal(service.StartupDirectory, dto.StartupDirectory);
            Assert.Equal(service.Parameters, dto.Parameters);
            Assert.Equal((int)service.StartupType, dto.StartupType);
            Assert.Equal((int)service.Priority, dto.Priority);
            Assert.Equal(service.StdoutPath, dto.StdoutPath);
            Assert.Equal(service.StderrPath, dto.StderrPath);
            Assert.Equal(service.EnableSizeRotation, dto.EnableSizeRotation);
            Assert.Equal(service.RotationSize, dto.RotationSize);
            Assert.Equal(service.EnableDateRotation, dto.EnableDateRotation);
            Assert.Equal((int)service.DateRotationType, dto.DateRotationType);
            Assert.Equal(service.MaxRotations, dto.MaxRotations);
            Assert.Equal(service.UseLocalTimeForRotation, dto.UseLocalTimeForRotation); // Added
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
            Assert.Equal(service.StartTimeout, dto.StartTimeout); // Added
            Assert.Equal(service.StopTimeout, dto.StopTimeout);   // Added
            Assert.Equal(service.PreStopExecutablePath, dto.PreStopExecutablePath); // Added
            Assert.Equal(service.PreStopStartupDirectory, dto.PreStopStartupDirectory); // Added
            Assert.Equal(service.PreStopParameters, dto.PreStopParameters); // Added
            Assert.Equal(service.PreStopTimeoutSeconds, dto.PreStopTimeoutSeconds); // Added
            Assert.Equal(service.PreStopLogAsError, dto.PreStopLogAsError); // Added
            Assert.Equal(service.PostStopExecutablePath, dto.PostStopExecutablePath); // Added
            Assert.Equal(service.PostStopStartupDirectory, dto.PostStopStartupDirectory); // Added
            Assert.Equal(service.PostStopParameters, dto.PostStopParameters); // Added
        }

        [Fact]
        public void ToDomain_WhenDtoIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            ServiceDto nullDto = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                ServiceMapper.ToDomain(_serviceManagerMock.Object, nullDto)
            );

            // Verify the parameter name in the exception matches your code
            Assert.Equal("dto", exception.ParamName);
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
                EnableSizeRotation = true,
                RotationSize = 4096,
                EnableDateRotation = true,
                DateRotationType = (int)DateRotationType.Weekly,
                MaxRotations = 5,
                UseLocalTimeForRotation = true, // Added
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
                StartTimeout = 40, // Added
                StopTimeout = 50,  // Added
                PreStopExecutablePath = @"C:\prestop.exe", // Added
                PreStopStartupDirectory = @"C:\prestop",    // Added
                PreStopParameters = "-pre-stop",           // Added
                PreStopTimeoutSeconds = 15,                // Added
                PreStopLogAsError = true,                 // Added
                PostStopExecutablePath = @"C:\poststop.exe", // Added
                PostStopStartupDirectory = @"C:\poststop",   // Added
                PostStopParameters = "-post-stop"           // Added
            };

            // Act
            var service = ServiceMapper.ToDomain(_serviceManagerMock.Object, dto);

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
            Assert.Equal(dto.EnableSizeRotation, service.EnableSizeRotation);
            Assert.Equal(dto.RotationSize, service.RotationSize);
            Assert.Equal(dto.EnableDateRotation, service.EnableDateRotation);
            Assert.Equal(dto.DateRotationType, (int)service.DateRotationType);
            Assert.Equal(dto.MaxRotations, service.MaxRotations);
            Assert.Equal(dto.UseLocalTimeForRotation, service.UseLocalTimeForRotation); // Added
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

            Assert.Equal(dto.StartTimeout, service.StartTimeout);
            Assert.Equal(dto.StopTimeout, service.StopTimeout);
            Assert.Equal(dto.PreStopExecutablePath, service.PreStopExecutablePath);
            Assert.Equal(dto.PreStopStartupDirectory, service.PreStopStartupDirectory);
            Assert.Equal(dto.PreStopParameters, service.PreStopParameters);
            Assert.Equal(dto.PreStopTimeoutSeconds, service.PreStopTimeoutSeconds);
            Assert.Equal(dto.PreStopLogAsError, service.PreStopLogAsError);
            Assert.Equal(dto.PostStopExecutablePath, service.PostStopExecutablePath);
            Assert.Equal(dto.PostStopStartupDirectory, service.PostStopStartupDirectory);
            Assert.Equal(dto.PostStopParameters, service.PostStopParameters);
        }

        [Fact]
        public void ToDomain_NullRepository_ThrowsArgumentNullException()
        {
            // Arrange
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
                EnableSizeRotation = null,
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

            // Act & Assert
            // We wrap the call in a lambda. Assert.Throws will catch the exception 
            // and fail the test if the exception is not thrown.
            var ex = Assert.Throws<ArgumentNullException>(() => ServiceMapper.ToDomain(null, dto));

            // Optional: Verify which parameter caused the exception
            Assert.Equal("serviceManager", ex.ParamName);
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
                EnableSizeRotation = true,
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

            var service = ServiceMapper.ToDomain(_serviceManagerMock.Object, dto);

            Assert.Equal(ServiceStartType.Manual, service.StartupType);
            Assert.Equal(ProcessPriority.High, service.Priority);
            Assert.True(service.EnableSizeRotation);
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
        public async Task Start_ReturnsTrue_WhenServiceManagerReturnsTrue()
        {
            _serviceManagerMock.Setup(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            var result = await _service.Start();

            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Start_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            _serviceManagerMock.Setup(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to start service."));

            var result = await _service.Start();

            Assert.False(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.StartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Stop_ReturnsTrue_WhenServiceManagerReturnsTrue()
        {
            _serviceManagerMock.Setup(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            var result = await _service.Stop();

            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Stop_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            _serviceManagerMock.Setup(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to stop service."));

            var result = await _service.Stop();

            Assert.False(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.StopServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Restart_ReturnsTrue_WhenServiceManagerReturnsTrue()
        {
            _serviceManagerMock.Setup(sm => sm.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            var result = await _service.Restart();  

            Assert.True(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Restart_ReturnsFalse_WhenServiceManagerReturnsFalse()
        {
            _serviceManagerMock.Setup(sm => sm.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("Failed to restart service."));

            var result = await _service.Restart();

            Assert.False(result.IsSuccess);
            _serviceManagerMock.Verify(sm => sm.RestartServiceAsync("TestService", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
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

        [Fact]
        public void ToDomain_NullOptionalValues_UsesDefaultFallbacks()
        {
            // Arrange
            // Create a DTO with only the absolute required fields; others are null
            var dto = new ServiceDto
            {
                Name = "MinimalService",
                ExecutablePath = @"C:\app\service.exe",
                // Every nullable field below is left as null to test fallbacks
                StartupType = null,
                Priority = null,
                EnableSizeRotation = null,
                RotationSize = null,
                EnableDateRotation = null,
                DateRotationType = null,
                MaxRotations = null,
                UseLocalTimeForRotation = null,
                EnableHealthMonitoring = null,
                HeartbeatInterval = null,
                MaxFailedChecks = null,
                RecoveryAction = null,
                MaxRestartAttempts = null,
                RunAsLocalSystem = null,
                PreLaunchTimeoutSeconds = null,
                PreLaunchRetryAttempts = null,
                PreLaunchIgnoreFailure = null,
                EnableDebugLogs = null,
                StartTimeout = null,
                StopTimeout = null,
                PreStopTimeoutSeconds = null,
                PreStopLogAsError = null
            };

            // Act
            var service = ServiceMapper.ToDomain(_serviceManagerMock.Object, dto);

            // Assert: Verify every fallback branch was hit correctly
            Assert.Equal(ServiceStartType.Automatic, service.StartupType); // StartupType == null branch
            Assert.Equal(ProcessPriority.Normal, service.Priority);        // Priority == null branch
            Assert.False(service.EnableSizeRotation);                         // ?? false
            Assert.Equal(AppConfig.DefaultRotationSizeMB, service.RotationSize); // ?? Default
            Assert.False(service.EnableDateRotation);                     // ?? false
            Assert.Equal(DateRotationType.Daily, service.DateRotationType); // .HasValue == false branch
            Assert.Equal(AppConfig.DefaultMaxRotations, service.MaxRotations);
            Assert.Equal(AppConfig.DefaultUseLocalTimeForRotation, service.UseLocalTimeForRotation);
            Assert.False(service.EnableHealthMonitoring);
            Assert.Equal(AppConfig.DefaultHeartbeatInterval, service.HeartbeatInterval);
            Assert.Equal(AppConfig.DefaultMaxFailedChecks, service.MaxFailedChecks);
            Assert.Equal(RecoveryAction.RestartService, service.RecoveryAction); // RecoveryAction == null branch
            Assert.Equal(AppConfig.DefaultMaxRestartAttempts, service.MaxRestartAttempts);
            Assert.True(service.RunAsLocalSystem);                        // ?? true
            Assert.Equal(AppConfig.DefaultPreLaunchTimeoutSeconds, service.PreLaunchTimeoutSeconds);
            Assert.Equal(AppConfig.DefaultPreLaunchRetryAttempts, service.PreLaunchRetryAttempts);
            Assert.False(service.PreLaunchIgnoreFailure);
            Assert.False(service.EnableDebugLogs);
            Assert.Equal(AppConfig.DefaultStartTimeout, service.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, service.StopTimeout);
            Assert.Equal(AppConfig.DefaultPreStopTimeoutSeconds, service.PreStopTimeoutSeconds);
            Assert.False(service.PreStopLogAsError);
        }

    }
}
