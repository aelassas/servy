using Moq;
using Servy.Core.Enums;
using Servy.Services;
using Xunit;

namespace Servy.UnitTests.Services
{
    public class ServiceCommandsTests
    {
        private readonly Mock<IServiceCommands> _mockServiceCommands;

        public ServiceCommandsTests()
        {
            _mockServiceCommands = new Mock<IServiceCommands>();
        }

        [Fact]
        public void InstallService_CalledWithCorrectParameters()
        {
            // Arrange
            var serviceName = "TestService";
            var serviceDescription = "Test Service Description";
            var processPath = @"C:\path\to\exe.exe";
            var startupDirectory = @"C:\path\to";
            var processParameters = "-arg1 -arg2";
            var startupType = ServiceStartType.Automatic;
            var processPriority = ProcessPriority.Normal;
            var stdoutPath = @"C:\logs\stdout.log";
            var stderrPath = @"C:\logs\stderr.log";
            var enableRotation = true;
            var rotationSize = "10485760"; // 10 MB
            var enableHealthMonitoring = true;
            var heartbeatInterval = "30";
            var maxFailedChecks = "3";
            var recoveryAction = RecoveryAction.RestartService;
            var maxRestartAttempts = "5";
            var envVars = "var1=val1;var2=val2";
            var dependencies = "DEP1; DEP2";
            var runAsLocalSystem = false;
            var username = @".\username";
            var password = "password";
            var confirmPassword = "password";

            var preLaunchExe = @"C:\pre-launch.exe";
            var preLaunchDir = @"C:\preLaunchDir";
            var preLaunchArgs = "preLaunchArgs";
            var preLaunchVars = "var1=val1;var2=val2;";
            var preLaunchStdoutPath = @"C:\pre-launch-stdout.log";
            var preLaunchStderrPath = @"C:\pre-launch-stderr.log";
            var preLaunchTimeout = "30";
            var preLaunchRetryAttempts = "0";
            var preLaunchIgnoreError = true;


            // Act
            _mockServiceCommands.Object.InstallService(
                serviceName,
                serviceDescription,
                processPath,
                startupDirectory,
                processParameters,
                startupType,
                processPriority,
                stdoutPath,
                stderrPath,
                enableRotation,
                rotationSize,
                enableHealthMonitoring,
                heartbeatInterval,
                maxFailedChecks,
                recoveryAction,
                maxRestartAttempts,
                envVars,
                dependencies,
                runAsLocalSystem,
                username,
                password,
                confirmPassword,

                preLaunchExe,
                preLaunchDir,
                preLaunchArgs,
                preLaunchVars,
                preLaunchStdoutPath,
                preLaunchStderrPath,
                preLaunchTimeout,
                preLaunchRetryAttempts,
                preLaunchIgnoreError
                );

            // Assert
            _mockServiceCommands.Verify(m => m.InstallService(
                serviceName,
                serviceDescription,
                processPath,
                startupDirectory,
                processParameters,
                startupType,
                processPriority,
                stdoutPath,
                stderrPath,
                enableRotation,
                rotationSize,
                enableHealthMonitoring,
                heartbeatInterval,
                maxFailedChecks,
                recoveryAction,
                maxRestartAttempts,
                envVars,
                dependencies,
                runAsLocalSystem,
                username,
                password,
                confirmPassword,

                preLaunchExe,
                preLaunchDir,
                preLaunchArgs,
                preLaunchVars,
                preLaunchStdoutPath,
                preLaunchStderrPath,
                preLaunchTimeout,
                preLaunchRetryAttempts,
                preLaunchIgnoreError
                ), Times.Once);
        }

        [Fact]
        public void UninstallService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.UninstallService(serviceName);

            _mockServiceCommands.Verify(m => m.UninstallService(serviceName), Times.Once);
        }

        [Fact]
        public void StartService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.StartService(serviceName);

            _mockServiceCommands.Verify(m => m.StartService(serviceName), Times.Once);
        }

        [Fact]
        public void StopService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.StopService(serviceName);

            _mockServiceCommands.Verify(m => m.StopService(serviceName), Times.Once);
        }

        [Fact]
        public void RestartService_CalledWithCorrectServiceName()
        {
            var serviceName = "TestService";

            _mockServiceCommands.Object.RestartService(serviceName);

            _mockServiceCommands.Verify(m => m.RestartService(serviceName), Times.Once);
        }
    }
}
