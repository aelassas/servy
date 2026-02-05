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
            // Ensure it doesn't look like a success exit (Code 0)
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.None);
            service.SetFailedChecks(0);

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert
            // Verify the internal counter incremented
            Assert.Equal(1, service.GetFailedChecks());

            // Verify the log matches the new unified format "Health check failed (1/3)."
            logger.Verify(l => l.Warning(It.Is<string>(s =>
                s.Contains("Health check failed") && s.Contains("(1/3)"))),
                Times.Once);
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

            // Setup the mock process to appear crashed
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1); // Non-zero exit code

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(3); // Ensure limit isn't hit
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert 

            // Verify the failure was logged
            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Health check failed (1/1)"))), Times.Once);

            // Verify the recovery log from ExecuteRecoveryAction
            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains($"Performing recovery action '{RecoveryAction.RestartProcess}' (1/3)"))), Times.Once);

            // Verify the helper was actually called to perform the restart
            helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<Action<string, string, string, List<EnvironmentVariable>>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(),
                It.IsAny<ILogger>(),
                It.IsAny<int>()),
                Times.Once);
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
                    It.IsAny<ILogger>(),
                    It.IsAny<int>()))
                .Verifiable();

            helper.Setup(h => h.RestartService(It.IsAny<ILogger>(), It.IsAny<string>())).Verifiable();

            helper.Setup(h => h.RestartComputer(It.IsAny<ILogger>())).Verifiable();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

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
                            It.IsAny<ILogger>(),
                            It.IsAny<int>()), Times.Once);
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
            var service = CreateService(out var logger, out var helper, out var swFactory,
                                       out var timerFactory, out var processFactory,
                                       out var pathValidator, out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            // Use this to control when the recovery "finishes"
            var recoveryStartedSignal = new TaskCompletionSource<bool>();
            var recoveryBlockSignal = new TaskCompletionSource<bool>();

            helper.Setup(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<Action<string, string, string, List<EnvironmentVariable>>>(),
                         It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                         It.IsAny<List<EnvironmentVariable>>(), It.IsAny<ILogger>(), It.IsAny<int>()))
                  .Callback(() => {
                      recoveryStartedSignal.TrySetResult(true);
                      recoveryBlockSignal.Task.Wait(); // HANG the thread here to keep _isRecovering = true
                  });

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            int calls = 20;
            var tasks = new List<Task>();
            for (int i = 0; i < calls; i++)
            {
                tasks.Add(Task.Run(() => service.InvokeCheckHealth(null, null), TestContext.Current.CancellationToken));
            }

            // Wait until at least one thread has entered the recovery block
            await recoveryStartedSignal.Task;

            // Give other threads a moment to attempt to enter the lock and get blocked
            await Task.Delay(100, TestContext.Current.CancellationToken);

            // Release the recovery mock so it can finish
            recoveryBlockSignal.SetResult(true);
            await Task.WhenAll(tasks);

            // Assert
            // Now it should be exactly 3. Threads 4-20 will have hit 'if (_isRecovering) return'
            // because Thread 3 was stuck in the helper mock, keeping the flag true.
            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Health check failed"))), Times.Exactly(3));

            helper.Verify(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<Action<string, string, string, List<EnvironmentVariable>>>(),
                          It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                          It.IsAny<List<EnvironmentVariable>>(), It.IsAny<ILogger>(), It.IsAny<int>()),
                          Times.Once);
        }
    }
}
