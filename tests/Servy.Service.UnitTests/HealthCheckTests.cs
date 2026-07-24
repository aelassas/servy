using Moq;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using Servy.Service.UnitTests.Utilities;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.UnitTests
{
    public class HealthCheckTests : IDisposable
    {
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly List<IDisposable> _disposableServices = new List<IDisposable>();

        public HealthCheckTests()
        {
            _mockProcessKiller = new Mock<IProcessKiller>();
        }

        [Fact]
        public void CheckHealth_ProcessExited_IncrementsFailedChecks_AndLogs()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.None);
            service.SetFailedChecks(0);

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert
            Assert.Equal(1, service.GetFailedChecks());
            ctx.Logger.Verify(l => l.Warn(It.Is<string>(s =>
                s.Contains("Health check failed") && s.Contains("(1/3)")), It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void CheckHealth_ExceedMaxFailedChecks_TriggersRecoveryAction()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert 
            ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Health check failed (1/1)")), It.IsAny<Exception>()), Times.Once);
            ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains($"Performing recovery action '{RecoveryAction.RestartProcess}' (1/3)")), It.IsAny<Exception>()), Times.Once);

            ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(RecoveryAction.RestartProcess)]
        [InlineData(RecoveryAction.RestartService)]
        [InlineData(RecoveryAction.RestartComputer)]
        [InlineData(RecoveryAction.None)]
        public void CheckHealth_RecoveryActions_ExecuteExpectedLogic(RecoveryAction action)
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

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

            // Assert
            switch (action)
            {
                case RecoveryAction.None:
                    ctx.Helper.VerifyNoOtherCalls();
                    break;
                case RecoveryAction.RestartProcess:
                    ctx.Helper.Verify(h => h.RestartProcess(
                        It.IsAny<IProcessWrapper>(),
                        It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
                    break;
                case RecoveryAction.RestartService:
                    ctx.Helper.Verify(h => h.RestartService(service.ServiceName, It.IsAny<IServyLogger>()), Times.Once);
                    break;
                case RecoveryAction.RestartComputer:
                    ctx.Helper.Verify(h => h.RestartComputer(It.IsAny<IServyLogger>()), Times.Once);
                    break;
            }
        }

        [Fact]
        public void CheckHealth_ProcessHealthy_ResetsFailedChecks_AndLogs()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetFailedChecks(3);

            // Act
            service.InvokeCheckHealth(null, null);

            // Assert
            Assert.Equal(0, service.GetFailedChecks());
            ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Child process is healthy")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task CheckHealth_ThreadSafety_MultipleConcurrentCalls()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

            bool processHasExited = true;
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(() => processHasExited);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            var recoveryTriggered = new TaskCompletionSource<bool>();

            ctx.Helper.Setup(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                                 It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                 It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .Callback(() => {
                      processHasExited = false;
                      recoveryTriggered.TrySetResult(true);
                  });

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            int calls = 20;
            var startingGun = new TaskCompletionSource<bool>();
            var tasks = new List<Task>();

            for (int i = 0; i < calls; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await startingGun.Task;
                    service.InvokeCheckHealth(null, null);
                }, CancellationToken.None));
            }

            // Act
            startingGun.SetResult(true);

            var completedTask = await Task.WhenAny(recoveryTriggered.Task, Task.Delay(TestTimeouts.CiGenerous, CancellationToken.None));
            if (completedTask != recoveryTriggered.Task)
            {
                Assert.Fail("Timeout: RestartProcess was never called. The CI Thread Pool might be starved.");
            }

            await Task.WhenAll(tasks);

            // Assert
            ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Health check failed")), It.IsAny<Exception>()), Times.Exactly(3));
            ctx.Helper.Verify(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<Action<string, string, string, List<EnvironmentVariable>, CancellationToken>>(),
                                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                  It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                                  Times.Once);
        }

        public void Dispose()
        {
            // Unified Cleanup: Iterate and safely drop transient test services to avoid CTS leaks
            foreach (var service in _disposableServices)
            {
                service?.Dispose();
            }
        }
    }
}