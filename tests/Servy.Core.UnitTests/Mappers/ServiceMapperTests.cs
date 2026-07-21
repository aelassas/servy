using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Mappers;
using Servy.Core.Services;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Mappers
{
    public class ServiceMapperTests
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;

        public ServiceMapperTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
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

            // Verify the parameter name in the exception matches the code
            Assert.Equal("dto", exception.ParamName);
        }

        [Fact]
        public void ToDomain_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "MyService",
                DisplayName = "Custom Friendly Display Name",
                Description = "Test Service",
                ExecutablePath = @"C:\app\service.exe",
                StartupDirectory = @"C:\app",
                Parameters = "-arg1 -arg2",
                StartupType = (int)ServiceStartType.Manual,
                Priority = (int)ProcessPriority.BelowNormal,
                EnableConsoleUI = true,
                StdoutPath = "stdout.log",
                StderrPath = "stderr.log",
                EnableSizeRotation = true,
                RotationSize = 4096,
                EnableDateRotation = true,
                DateRotationType = (int)DateRotationType.Weekly,
                MaxRotations = 5,
                UseLocalTimeForRotation = true,
                EnableDebugLogs = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 90,
                MaxFailedChecks = 7,
                RecoveryAction = (int)RecoveryAction.None,
                RecoveryOnCleanExit = true,
                MaxRestartAttempts = 20,
                HeartbeatUrl = "https://hc-ping.com/test-uuid",
                HeartbeatUrlTimeoutSeconds = 10,
                EnableHeartbeatUrlFlags = true,
                FailureProgramPath = @"C:\apps\failure_prog.exe",
                FailureProgramParameters = "--param1",
                FailureProgramStartupDirectory = @"C:\apps",
                EnvironmentVariables = "KEY1=VALUE1",
                ServiceDependencies = "DepA,DepB",
                RunAsLocalSystem = false,
                UserAccount = "User2",
                Password = "TopSecret",
                Pid = 1234,
                ActiveStdoutPath = @"C:\app\active_out.log",
                ActiveStderrPath = @"C:\app\active_err.log",
                PreLaunchExecutablePath = @"C:\prelaunch.exe",
                PreLaunchStartupDirectory = @"C:\prelaunch",
                PreLaunchParameters = "-pre2",
                PreLaunchEnvironmentVariables = "PRE2=VAL2",
                PreLaunchStdoutPath = "pre_stdout.log",
                PreLaunchStderrPath = "pre_stderr.log",
                PreLaunchTimeoutSeconds = 50,
                PreLaunchRetryAttempts = 3,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = @"C:\apps\post_launch\post_launch.exe",
                PostLaunchParameters = "--post-param1",
                PostLaunchStartupDirectory = @"C:\apps\post_launch\",
                StartTimeout = 40,
                StopTimeout = 50,
                PreStopExecutablePath = @"C:\prestop.exe",
                PreStopStartupDirectory = @"C:\prestop",
                PreStopParameters = "-pre-stop",
                PreStopTimeoutSeconds = 15,
                PreStopLogAsError = true,
                PostStopExecutablePath = @"C:\poststop.exe",
                PostStopStartupDirectory = @"C:\poststop",
                PostStopParameters = "-post-stop"
            };

            // Act
            var service = ServiceMapper.ToDomain(_serviceManagerMock.Object, dto);

            // Assert
            Assert.Equal(dto.Name, service.Name);
            Assert.Equal(dto.DisplayName, service.DisplayName);
            Assert.Equal(dto.Description, service.Description);
            Assert.Equal(dto.ExecutablePath, service.ExecutablePath);
            Assert.Equal(dto.StartupDirectory, service.StartupDirectory);
            Assert.Equal(dto.Parameters, service.Parameters);
            Assert.Equal((ServiceStartType)dto.StartupType, service.StartupType);
            Assert.Equal((ProcessPriority)dto.Priority, service.Priority);
            Assert.Equal(dto.EnableConsoleUI, service.EnableConsoleUI);
            Assert.Equal(dto.StdoutPath, service.StdoutPath);
            Assert.Equal(dto.StderrPath, service.StderrPath);
            Assert.Equal(dto.EnableSizeRotation, service.EnableSizeRotation);
            Assert.Equal(dto.RotationSize, service.RotationSize);
            Assert.Equal(dto.EnableDateRotation, service.EnableDateRotation);
            Assert.Equal((DateRotationType)dto.DateRotationType, service.DateRotationType);
            Assert.Equal(dto.MaxRotations, service.MaxRotations);
            Assert.Equal(dto.UseLocalTimeForRotation, service.UseLocalTimeForRotation);
            Assert.Equal(dto.EnableDebugLogs, service.EnableDebugLogs);
            Assert.Equal(dto.EnableHealthMonitoring, service.EnableHealthMonitoring);
            Assert.Equal(dto.HeartbeatInterval, service.HeartbeatInterval);
            Assert.Equal(dto.MaxFailedChecks, service.MaxFailedChecks);
            Assert.Equal((RecoveryAction)dto.RecoveryAction, service.RecoveryAction);
            Assert.Equal(dto.RecoveryOnCleanExit, service.RecoveryOnCleanExit);
            Assert.Equal(dto.MaxRestartAttempts, service.MaxRestartAttempts);
            Assert.Equal(dto.HeartbeatUrl, service.HeartbeatUrl);
            Assert.Equal(dto.HeartbeatUrlTimeoutSeconds, service.HeartbeatUrlTimeoutSeconds);
            Assert.Equal(dto.EnableHeartbeatUrlFlags, service.EnableHeartbeatUrlFlags);
            Assert.Equal(dto.FailureProgramPath, service.FailureProgramPath);
            Assert.Equal(dto.FailureProgramStartupDirectory, service.FailureProgramStartupDirectory);
            Assert.Equal(dto.FailureProgramParameters, service.FailureProgramParameters);
            Assert.Equal(dto.EnvironmentVariables, service.EnvironmentVariables);
            Assert.Equal(dto.ServiceDependencies, service.ServiceDependencies);
            Assert.Equal(dto.RunAsLocalSystem, service.RunAsLocalSystem);
            Assert.Equal(dto.UserAccount, service.UserAccount);
            Assert.Equal(dto.Password, service.Password);
            Assert.Equal(dto.Pid, service.Pid);
            Assert.Equal(dto.ActiveStdoutPath, service.ActiveStdoutPath);
            Assert.Equal(dto.ActiveStderrPath, service.ActiveStderrPath);
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
            var dto = new ServiceDto { Name = "MyService", ExecutablePath = @"C:\service.exe" };

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => ServiceMapper.ToDomain(null, dto));
            Assert.Equal("serviceManager", ex.ParamName);
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
                HeartbeatUrlTimeoutSeconds = null,
                EnableHeartbeatUrlFlags = null,
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
            Assert.Equal(AppConfig.DefaultStartupType, service.StartupType); // StartupType == null branch
            Assert.Equal(AppConfig.DefaultProcessPriority, service.Priority);        // Priority == null branch
            Assert.False(service.EnableSizeRotation);                         // ?? false
            Assert.Equal(AppConfig.DefaultRotationSizeMB, service.RotationSize); // ?? Default
            Assert.False(service.EnableDateRotation);                     // ?? false
            Assert.Equal(AppConfig.DefaultDateRotationType, service.DateRotationType); // .HasValue == false branch
            Assert.Equal(AppConfig.DefaultMaxRotations, service.MaxRotations);
            Assert.Equal(AppConfig.DefaultUseLocalTimeForRotation, service.UseLocalTimeForRotation);
            Assert.False(service.EnableHealthMonitoring);
            Assert.Equal(AppConfig.DefaultHeartbeatInterval, service.HeartbeatInterval);
            Assert.Equal(AppConfig.DefaultMaxFailedChecks, service.MaxFailedChecks);
            Assert.Equal(AppConfig.DefaultRecoveryAction, service.RecoveryAction); // RecoveryAction == null branch
            Assert.Equal(AppConfig.DefaultMaxRestartAttempts, service.MaxRestartAttempts);
            Assert.True(service.RunAsLocalSystem);                                // ?? true
            Assert.Equal(AppConfig.DefaultPreLaunchTimeoutSeconds, service.PreLaunchTimeoutSeconds);
            Assert.Equal(AppConfig.DefaultPreLaunchRetryAttempts, service.PreLaunchRetryAttempts);
            Assert.False(service.PreLaunchIgnoreFailure);
            Assert.False(service.EnableDebugLogs);
            Assert.Equal(AppConfig.DefaultStartTimeout, service.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, service.StopTimeout);
            Assert.Equal(AppConfig.DefaultPreStopTimeoutSeconds, service.PreStopTimeoutSeconds);
            Assert.False(service.PreStopLogAsError);
            Assert.Equal(AppConfig.DefaultEnableConsoleUI, service.EnableConsoleUI);
            Assert.Equal(AppConfig.DefaultRecoveryOnCleanExit, service.RecoveryOnCleanExit);
            Assert.Equal(string.Empty, service.DisplayName);
        }
    }
}