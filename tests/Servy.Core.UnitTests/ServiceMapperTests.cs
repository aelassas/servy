using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Mappers;
using Servy.Core.Models;

namespace Servy.Core.UnitTests
{
    public class ServiceMapperTests
    {
        [Fact]
        public void ToDto_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var service = new Service
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
                EnableHealthMonitoring = true,
                HeartbeatInterval = 60,
                MaxFailedChecks = 5,
                RecoveryAction = RecoveryAction.RestartService,
                MaxRestartAttempts = 10,
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
                PreLaunchIgnoreFailure = true
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
            Assert.Equal(service.EnableHealthMonitoring, dto.EnableHealthMonitoring);
            Assert.Equal(service.HeartbeatInterval, dto.HeartbeatInterval);
            Assert.Equal(service.MaxFailedChecks, dto.MaxFailedChecks);
            Assert.Equal((int)service.RecoveryAction, dto.RecoveryAction);
            Assert.Equal(service.MaxRestartAttempts, dto.MaxRestartAttempts);
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
                EnableHealthMonitoring = false,
                HeartbeatInterval = 90,
                MaxFailedChecks = 7,
                RecoveryAction = (int)RecoveryAction.None,
                MaxRestartAttempts = 20,
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
                PreLaunchIgnoreFailure = false
            };

            // Act
            var service = ServiceMapper.ToDomain(dto);

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
            Assert.Equal(dto.EnableHealthMonitoring, service.EnableHealthMonitoring);
            Assert.Equal(dto.HeartbeatInterval, service.HeartbeatInterval);
            Assert.Equal(dto.MaxFailedChecks, service.MaxFailedChecks);
            Assert.Equal((RecoveryAction)dto.RecoveryAction, service.RecoveryAction);
            Assert.Equal(dto.MaxRestartAttempts, service.MaxRestartAttempts);
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
        }
    }
}
