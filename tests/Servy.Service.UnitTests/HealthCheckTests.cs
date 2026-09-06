using Moq;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;

namespace Servy.Service.UnitTests
{
    public class HealthCheckTests : IDisposable
    {
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        [Fact]
        public async Task CheckHealth_ProcessExited_IncrementsFailedChecks_AndLogs()
        {
            // Arrange
            var service = _ctx.Build();

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.None);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            Assert.Equal(1, service.GetFailedChecks());
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s =>
                s.Contains("Health check failed") && s.Contains("(1/3)")), It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public async Task CheckHealth_ExceedMaxFailedChecks_TriggersRecoveryAction()
        {
            // Arrange
            var service = _ctx.Build();

            var pingLogged = new TaskCompletionSource<string>();
            _ctx.Logger
                .Setup(l => l.Debug(It.Is<string>(s => s.Contains("Emitting heartbeat ping to:")), It.IsAny<Exception>()))
                .Callback<string, Exception>((msg, ex) => pingLogged.TrySetResult(msg));

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions("https://127.0.0.1:1/fail-heartbeat"));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Health check failed (1/1)")), It.IsAny<Exception>()), Times.Once);
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains($"Performing recovery action '{RecoveryAction.RestartProcess}' (1/3)")), It.IsAny<Exception>()), Times.Once);

            _ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify Placement 1: Failure threshold reached emits fail-flag ping
            var completedTask = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));
            Assert.Same(pingLogged.Task, completedTask);
            Assert.Contains("(flag: fail)", await pingLogged.Task);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsExhausted_LogsErrorAndResetsCounter()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            await File.WriteAllTextAsync(attemptsFile, "3", TestContext.Current.CancellationToken);

            try
            {
                TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(true);
                mockProcess.Setup(p => p.ExitCode).Returns(-1);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetMaxFailedChecks(1);
                service.SetMaxRestartAttempts(3);   // already at 3 on disk => exhausted
                service.SetRecoveryAction(RecoveryAction.RestartProcess);
                service.SetFailedChecks(0);

                // Act
                await service.InvokeCheckHealthAsync(null, null);

                // Assert
                _ctx.Logger.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Maximum restart attempts reached (3)")), It.IsAny<Exception>()),
                    Times.Once);

                // No restart is attempted once the cap is hit
                _ctx.Helper.Verify(h => h.RestartProcess(
                    It.IsAny<IProcessWrapper>(),
                    It.IsAny<StartProcessCallback>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                    Times.Never);

                // The counter is reset on disk so the next manual start begins from zero
                Assert.Equal("0", (await File.ReadAllTextAsync(attemptsFile, TestContext.Current.CancellationToken)).Trim());
            }
            finally
            {
                if (File.Exists(attemptsFile))
                {
                    try { File.Delete(attemptsFile); } catch { /* teardown is best-effort */ }
                }
            }
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsBelowCap_IncrementsAndPersistsCounter()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            await File.WriteAllTextAsync(attemptsFile, "1", TestContext.Current.CancellationToken);

            try
            {
                TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(true);
                mockProcess.Setup(p => p.ExitCode).Returns(-1);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetMaxFailedChecks(1);
                service.SetMaxRestartAttempts(3);   // 1 of 3 used => recovery still allowed
                service.SetRecoveryAction(RecoveryAction.RestartProcess);
                service.SetFailedChecks(0);

                // Act
                await service.InvokeCheckHealthAsync(null, null);

                // Assert
                _ctx.Logger.Verify(l => l.Warn(
                    It.Is<string>(s => s.Contains($"Performing recovery action '{RecoveryAction.RestartProcess}' (2/3)")), It.IsAny<Exception>()),
                    Times.Once);

                Assert.Equal("2", (await File.ReadAllTextAsync(attemptsFile, TestContext.Current.CancellationToken)).Trim());
            }
            finally
            {
                if (File.Exists(attemptsFile))
                {
                    try { File.Delete(attemptsFile); } catch { /* teardown is best-effort */ }
                }
            }
        }

        [Fact]
        public async Task CheckHealth_UnlimitedRestartAttempts_RecoversWithoutConsultingTheCounter()
        {
            // Arrange
            var service = _ctx.Build();

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(0);   // documented as unlimited
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.Contains($"Performing recovery action '{RecoveryAction.RestartProcess}' (unlimited)")), It.IsAny<Exception>()),
                Times.Once);

            _ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(RecoveryAction.RestartProcess)]
        [InlineData(RecoveryAction.RestartService)]
        [InlineData(RecoveryAction.RestartComputer)]
        [InlineData(RecoveryAction.None)]
        public async Task CheckHealth_RecoveryActions_ExecuteExpectedLogic(RecoveryAction action)
        {
            // Arrange
            var service = _ctx.Build();

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

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
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            switch (action)
            {
                case RecoveryAction.None:
                    _ctx.Helper.VerifyNoOtherCalls();
                    break;
                case RecoveryAction.RestartProcess:
                    _ctx.Helper.Verify(h => h.RestartProcess(
                        It.IsAny<IProcessWrapper>(),
                        It.IsAny<StartProcessCallback>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
                    break;
                case RecoveryAction.RestartService:
                    _ctx.Helper.Verify(h => h.RestartService(service.ServiceName, It.IsAny<IServyLogger>()), Times.Once);
                    break;
                case RecoveryAction.RestartComputer:
                    _ctx.Helper.Verify(h => h.RestartComputer(It.IsAny<IServyLogger>()), Times.Once);
                    break;
            }
        }

        [Fact]
        public async Task CheckHealth_ProcessHealthy_ResetsFailedChecks_AndLogs()
        {
            // Arrange
            var service = _ctx.Build();

            var pingLogged = new TaskCompletionSource<string>();
            _ctx.Logger
                .Setup(l => l.Debug(It.Is<string>(s => s.Contains("Emitting heartbeat ping to:")), It.IsAny<Exception>()))
                .Callback<string, Exception>((msg, ex) => pingLogged.TrySetResult(msg));

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions("https://127.0.0.1:1/start-heartbeat"));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetFailedChecks(3);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            Assert.Equal(0, service.GetFailedChecks());
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Child process is healthy")), It.IsAny<Exception>()), Times.Once);

            // Verify Placement 2: Healthy again after failures emits start-flag ping
            var completedTask = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));
            Assert.Same(pingLogged.Task, completedTask);
            Assert.Contains("(flag: start)", await pingLogged.Task);
        }

        [Fact]
        public async Task CheckHealth_ProcessHealthy_RoutineTick_EmitsRoutineHeartbeatPing()
        {
            // Arrange
            var service = _ctx.Build();

            var pingLogged = new TaskCompletionSource<string>();
            _ctx.Logger
                .Setup(l => l.Debug(It.Is<string>(s => s.Contains("Emitting heartbeat ping to:")), It.IsAny<Exception>()))
                .Callback<string, Exception>((msg, ex) => pingLogged.TrySetResult(msg));

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions("https://127.0.0.1:1/routine-heartbeat"));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            Assert.Equal(0, service.GetFailedChecks());

            // Verify Placement 3: Routine healthy tick emits empty-flag ping
            var completedTask = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));
            Assert.Same(pingLogged.Task, completedTask);
            Assert.Contains("(flag: routine)", await pingLogged.Task);
        }

        [Fact]
        public async Task CheckHealth_ThreadSafety_MultipleConcurrentCalls()
        {
            // Arrange
            var service = _ctx.Build();

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            bool processHasExited = true;
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(() => processHasExited);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            var recoveryTriggered = new TaskCompletionSource<bool>();

            _ctx.Helper.Setup(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<StartProcessCallback>(),
                                                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                                  It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .Callback(() =>
                  {
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
                    await service.InvokeCheckHealthAsync(null, null);
                }, TestContext.Current.CancellationToken));
            }

            // Act
            startingGun.SetResult(true);

            var completedTask = await Task.WhenAny(recoveryTriggered.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));
            if (completedTask != recoveryTriggered.Task)
            {
                Assert.Fail("Timeout: RestartProcess was never called. The CI Thread Pool might be starved.");
            }

            await Task.WhenAll(tasks);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Health check failed")), It.IsAny<Exception>()), Times.Exactly(3));
            _ctx.Helper.Verify(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<StartProcessCallback>(),
                                                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                                  It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                                                  Times.Once);
        }

        public void Dispose() => _ctx.Dispose();
    }
}
