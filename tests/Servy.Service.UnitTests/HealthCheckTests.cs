using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using IServiceHelper = Servy.Service.Helpers.IServiceHelper;

namespace Servy.Service.UnitTests
{
    public class HealthCheckTests
    {
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public HealthCheckTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockProcessKiller = new Mock<IProcessKiller>();
        }

        // Helper to create service with injected mocks
        private TestableService CreateService(
            out Mock<IServyLogger> mockLogger,
            out Mock<IServiceHelper> mockHelper,
            out Mock<IStreamWriterFactory> mockStreamWriterFactory,
            out Mock<ITimerFactory> mockTimerFactory,
            out Mock<IProcessFactory> mockProcessFactory,
            out Mock<IPathValidator> mockPathValidator,
            out Mock<IServiceRepository> mockServiceRepository
            )
        {
            mockLogger = new Mock<IServyLogger>();
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
                mockServiceRepository.Object,
                _mockProcessHelper.Object,
                _mockProcessKiller.Object
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
            logger.Verify(l => l.Warn(It.Is<string>(s =>
                s.Contains("Health check failed") && s.Contains("(1/3)")), It.IsAny<Exception>()),
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
            logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Health check failed (1/1)")), It.IsAny<Exception>()), Times.Once);

            // Verify the recovery log from ExecuteRecoveryAction
            logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains($"Performing recovery action '{RecoveryAction.RestartProcess}' (1/3)")), It.IsAny<Exception>()), Times.Once);

            // Verify the helper was actually called to perform the restart
            helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(),
                It.IsAny<IServyLogger>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
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
                    It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<List<EnvironmentVariable>>(),
                    It.IsAny<IServyLogger>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable();

            helper.Setup(h => h.RestartService(It.IsAny<IServyLogger>(), It.IsAny<string>())).Verifiable();

            helper.Setup(h => h.RestartComputer(It.IsAny<IServyLogger>())).Verifiable();

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
                            It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<List<EnvironmentVariable>>(),
                            It.IsAny<IServyLogger>(),
                            It.IsAny<int>(),
                            It.IsAny<CancellationToken>()), Times.Once);
                        break;
                    case RecoveryAction.RestartService:
                        helper.Verify(h => h.RestartService(It.IsAny<IServyLogger>(), service.ServiceName), Times.Once);
                        break;
                    case RecoveryAction.RestartComputer:
                        helper.Verify(h => h.RestartComputer(It.IsAny<IServyLogger>()), Times.Once);
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
            logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Child process is healthy")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task CheckHealth_ThreadSafety_MultipleConcurrentCalls()
        {
            // Arrange
            var service = CreateService(out var logger, out var helper, out var swFactory,
                                       out var timerFactory, out var processFactory,
                                       out var pathValidator, out var serviceRepository);

            // Track the process state dynamically. Starts as crashed.
            bool processHasExited = true;
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(() => processHasExited);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            // This signal forces the test runner to wait for the background threads
            var recoveryTriggered = new TaskCompletionSource<bool>();

            helper.Setup(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                         It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                         It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .Callback(() =>
                  {
                      // 1. Mark process as healthy to prevent trailing threads from starting a second failure cycle
                      processHasExited = false;

                      // 2. Signal the main test thread that Thread 3 successfully reached recovery
                      recoveryTriggered.TrySetResult(true);
                  });

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            int calls = 20;
            var startingGun = new TaskCompletionSource<bool>();
            var tasks = new List<Task>();

            // Fire all 20 health checks onto the Thread Pool, but do NOT await them in the loop.
            for (int i = 0; i < calls; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    // All 20 threads will spin up and pause right here
                    await startingGun.Task;
                    service.InvokeCheckHealth(null, null);
                }));
            }

            // FIRE THE STARTING GUN! 
            // This releases all 20 tasks simultaneously, guaranteeing maximum contention
            // and a true test of the semaphore, regardless of the CPU core count.
            startingGun.SetResult(true);

            // Wait for the recovery to be triggered by the background threads. 
            // Increased to 15 seconds to prevent timeouts on slow GitHub CI runners.
            var completedTask = await Task.WhenAny(recoveryTriggered.Task, Task.Delay(TimeSpan.FromSeconds(15)));

            if (completedTask != recoveryTriggered.Task)
            {
                Assert.Fail("Timeout: RestartProcess was never called. The CI Thread Pool might be starved.");
            }

            // CRITICAL: GitHub CI runners are slow. Even though recovery triggered, the remaining 17 
            // concurrent calls need ample time to wake up, process the healthy state, and exit gracefully.
            // 2000ms ensures the 2-core runner finishes its queue before we hit the Mock.Verify.
            await Task.Delay(2000);

            // Assert
            logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Health check failed")), It.IsAny<Exception>()), Times.Exactly(3));

            helper.Verify(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                          It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                          It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                          Times.Once);
        }

    }
}
