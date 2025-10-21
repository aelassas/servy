using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.UnitTests
{
    public class HealthCheckTests
    {
        // Helper to create service with injected mocks
        private static TestableService CreateService(
            out Mock<ILogger> mockLogger,
            out Mock<IServiceHelper> mockHelper,
            out Mock<IStreamWriterFactory> mockStreamWriterFactory,
            out Mock<ITimerFactory> mockTimerFactory,
            out Mock<IProcessFactory> mockProcessFactory,
            out Mock<IPathValidator> mockPathValidator,
            out Mock<IServiceRepository> mockServiceRepository
            )
        {
            mockLogger = new Mock<ILogger>();
            mockHelper = new Mock<IServiceHelper>();
            mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            mockTimerFactory = new Mock<ITimerFactory>();
            mockProcessFactory = new Mock<IProcessFactory>();
            mockPathValidator = new Mock<IPathValidator>();
            mockServiceRepository = new Mock<IServiceRepository>();

            return new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                mockStreamWriterFactory.Object,
                mockTimerFactory.Object,
                mockProcessFactory.Object,
                mockPathValidator.Object,
                mockServiceRepository.Object
                );
        }

        [Fact]
        public void CheckHealth_ProcessExited_IncrementsFailedChecks_AndLogs()
        {
            // Arrange
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.None);
            service.SetFailedChecks(0);

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert
            Assert.Equal(1, service.GetFailedChecks());
            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Child process is not running"))), Times.Once);
        }

        [Fact]
        public void CheckHealth_ExceedMaxFailedChecks_TriggersRecoveryAction()
        {
            // Arrange
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(1);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0); // Changed from 1 to 0

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert recovery action invoked and logged
            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains($"Health check failed ("))), Times.AtLeastOnce);
            helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<Action<string, string, string, List<EnvironmentVariable>>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(),
                It.IsAny<ILogger>()),
                Times.Once);

            // We might want to verify process restart logic (mock process kill/start etc.)
        }

        [Theory]
        [InlineData(RecoveryAction.RestartProcess)]
        [InlineData(RecoveryAction.RestartService)]
        [InlineData(RecoveryAction.RestartComputer)]
        [InlineData(RecoveryAction.None)]
        public void CheckHealth_RecoveryActions_ExecuteExpectedLogic_NoLogs(RecoveryAction action)
        {
            // Arrange
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            // Setup mocks for helper methods (just verify calls, no real implementations or logs)
            helper.Setup(h =>
                h.RestartProcess(
                    It.IsAny<IProcessWrapper>(),
                    It.IsAny<Action<string, string, string, List<EnvironmentVariable>>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<List<EnvironmentVariable>>(),
                    It.IsAny<ILogger>()))
                .Verifiable();

            helper.Setup(h => h.RestartService(It.IsAny<ILogger>(), It.IsAny<string>())).Verifiable();

            helper.Setup(h => h.RestartComputer(It.IsAny<ILogger>())).Verifiable();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(1);
            service.SetRecoveryAction(action);
            service.SetFailedChecks(1);
            service.SetMaxRestartAttempts(3);
            service.SetServiceName("Servy");

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert recovery helper methods were called as expected
            if (action == RecoveryAction.None)
            {
                helper.VerifyNoOtherCalls();
            }
            else
            {
                switch (action)
                {
                    case RecoveryAction.RestartProcess:
                        helper.Verify(h => h.RestartProcess(
                            It.IsAny<IProcessWrapper>(),
                            It.IsAny<Action<string, string, string, List<EnvironmentVariable>>>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<List<EnvironmentVariable>>(),
                            It.IsAny<ILogger>()), Times.Once);
                        break;
                    case RecoveryAction.RestartService:
                        helper.Verify(h => h.RestartService(It.IsAny<ILogger>(), service.ServiceName), Times.Once);
                        break;
                    case RecoveryAction.RestartComputer:
                        helper.Verify(h => h.RestartComputer(It.IsAny<ILogger>()), Times.Once);
                        break;
                }
            }
        }


        [Fact]
        public void CheckHealth_ProcessHealthy_ResetsFailedChecks_AndLogs()
        {
            // Arrange
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetFailedChecks(3);

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert failed checks reset and info logged
            Assert.Equal(0, service.GetFailedChecks());
            logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Child process is healthy"))), Times.Once);
        }

        [Fact]
        public async Task CheckHealth_ThreadSafety_MultipleConcurrentCalls()
        {
            // Arrange
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            int calls = 10;
            var tasks = new Task[calls];

            // Act - run multiple concurrent CheckHealth calls to test thread safety
            for (int i = 0; i < calls; i++)
            {
                tasks[i] = Task.Run(() => service.InvokeCheckHealth(null, null));
            }
            await Task.WhenAll(tasks);

            // Assert - failedChecks should not exceed maxFailedChecks (3)
            Assert.InRange(service.GetFailedChecks(), 1, 3);

            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Health check failed"))), Times.AtLeast(calls));

            logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Max failed health checks reached"))), Times.AtMostOnce());
        }
    }
}
