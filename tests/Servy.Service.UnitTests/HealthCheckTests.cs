using Moq;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System.Timers;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class HealthCheckTests : IDisposable
    {
        private const string ServiceName = "HealthCheckTestService";

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
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
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
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 3);

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(3);   // already at 3 in the database => exhausted
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
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);

            // The counter is reset through the host so the next manual start begins from zero
            Assert.Equal(0, store.Attempts);
            Assert.Equal(new[] { 0 }, store.Writes);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsExhausted_ReadsAndResetsOnlyThisServicesCounter()
        {
            // Arrange: the counter is read and written through the Servy host, for this service's name only
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 12);

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(12);   // already at 12 in the database => exhausted
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert: one read and one reset, both for this service
            Assert.Equal(new[] { 0 }, store.Writes);
            Assert.Equal(new[] { ServiceName, ServiceName }, store.ServiceNames);
            _ctx.NamedPipesService.Verify(p => p.GetRestartAttemptsAsync(ServiceName, It.IsAny<CancellationToken>()), Times.Once);
            _ctx.NamedPipesService.Verify(p => p.UpdateRestartAttemptsAsync(ServiceName, 0, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsBelowCap_IncrementsAndPersistsCounter()
        {
            // Arrange
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 1);

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
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

            Assert.Equal(2, store.Attempts);
            Assert.Equal(new[] { 2 }, store.Writes);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsNeverWritten_StartsFromZeroAndRecovers()
        {
            // Arrange: a service whose counter was never written reads back as 0 with no timestamp
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 0);
            store.UpdatedAtUtc = null;

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            // A counter that was never written is a fresh start, not a failure: recovery runs as attempt 1 of 3
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.Contains($"Performing recovery action '{RecoveryAction.RestartProcess}' (1/3)")), It.IsAny<Exception>()),
                Times.Once);
            _ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // The incremented counter was saved
            Assert.Equal(1, store.Attempts);

            // The unreadable arm was not taken
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("is unreadable")), It.IsAny<Exception>()),
                Times.Never);
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
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
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
                        It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
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
                                                  It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
                                                  It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                                                  Times.Once);
        }

        /// <summary>
        /// Builds a <see cref="StartOptions"/> whose heartbeat fields pin the stability threshold that
        /// <c>ConditionalResetRestartAttemptsAsync</c> computes, so a test can choose a file age that
        /// falls on a known side of it.
        /// </summary>
        /// <param name="heartbeatIntervalInSeconds">The heartbeat interval, the first factor of the detection window.</param>
        /// <param name="maxFailedChecks">The failed-check allowance, the second factor of the detection window.</param>
        /// <param name="preLaunchTimeoutInSeconds">The pre-launch budget added to the threshold while pre-launch is enabled.</param>
        /// <returns>Options carrying the default paths plus the requested stability inputs.</returns>
        private static StartOptions CreateStabilityOptions(
            int heartbeatIntervalInSeconds,
            int maxFailedChecks,
            int preLaunchTimeoutInSeconds = 0)
        {
            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.HeartbeatIntervalInSeconds = heartbeatIntervalInSeconds;
            options.MaxFailedChecks = maxFailedChecks;
            options.PreLaunchTimeoutInSeconds = preLaunchTimeoutInSeconds;
            return options;
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public async Task CheckHealth_RestartAttemptsNegative_WarnsAndRewritesCounterToZero(int attempts)
        {
            // Arrange
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: attempts);

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.Contains("Invalid restart attempts counter received")), It.IsAny<Exception>()),
                Times.Once);

            // The counter is rewritten, not merely reported: a later read must find a usable value
            Assert.Equal(0, store.Attempts);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsUnreadable_AbortsRecoveryInsteadOfRestarting()
        {
            // Arrange: the Servy host cannot be reached, so the counter cannot be consulted
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 1);
            store.ReadFailure = new TimeoutException("The Servy host did not answer.");

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(3);
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("is unreadable") && s.Contains("MaxRestartAttempts cap cannot be enforced")), It.IsAny<Exception>()),
                Times.Once);

            // The null return is the contract under test: recovery aborts rather than restarting blind
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Failed to read restart attempts from persistent storage. Aborting recovery.")), It.IsAny<Exception>()),
                Times.Once);

            _ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.Empty(store.Writes);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsNotWritable_LogsSaveFailureAndStillRecovers()
        {
            // Arrange: the counter can be read but the host refuses the write
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 1);
            store.WriteFailure = new InvalidOperationException("Access denied.");

            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetMaxFailedChecks(1);
            service.SetMaxRestartAttempts(3);   // 1 of 3 used => the counter is incremented and saved
            service.SetRecoveryAction(RecoveryAction.RestartProcess);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains($"Failed to save the restart attempts counter of '{ServiceName}'")), It.IsAny<Exception>()),
                Times.Once);

            // A counter that cannot be persisted is logged, not fatal: the recovery action still runs
            _ctx.Helper.Verify(h => h.RestartProcess(
                It.IsAny<IProcessWrapper>(),
                It.IsAny<StartProcessCallback>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<EnvironmentVariable>>(), It.IsAny<bool>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // The write never landed, so the stored counter is unchanged
            Assert.Equal(1, store.Attempts);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsWrittenBeforeBoot_AnchorsCounterAndKeepsIt()
        {
            // Arrange: a year ago is before any plausible boot time, so the reboot-detection arm is taken
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 3, updatedAtUtc: DateTime.UtcNow.AddYears(-1));

            TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            // The quota survives the reboot, so a RestartComputer recovery cannot loop forever
            Assert.Equal(3, store.Attempts);

            // The unchanged counter is written again, so the host stamps it in the current session
            Assert.Equal(new[] { 3 }, store.Writes);
            Assert.True(store.UpdatedAtUtc > DateTime.UtcNow.AddMinutes(-5));

            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("Resetting restart attempts counter")), It.IsAny<Exception>()),
                Times.Never);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsNeverStamped_TreatedAsWrittenBeforeBoot()
        {
            // Arrange: a non-zero counter without a timestamp cannot be placed in this session
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 2);
            store.UpdatedAtUtc = null;

            TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert: kept and anchored, never reset
            Assert.Equal(2, store.Attempts);
            Assert.Equal(new[] { 2 }, store.Writes);
            Assert.NotNull(store.UpdatedAtUtc);
        }

        [Fact]
        public async Task CheckHealth_StableLongerThanResetThreshold_ResetsCounterToZero()
        {
            // Arrange: detection window 1s, buffer 30s => threshold 31s; 45s of stability clears it
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 3, updatedAtUtc: DateTime.UtcNow.AddSeconds(-45));

            TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("Resetting restart attempts counter. Stable for")), It.IsAny<Exception>()),
                Times.Once);

            Assert.Equal(0, store.Attempts);
            Assert.Equal(new[] { 0 }, store.Writes);
        }

        [Fact]
        public async Task CheckHealth_StableShorterThanResetThreshold_KeepsCounter()
        {
            // Arrange: same threshold as the reset test (31s), reached from the other side
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 3, updatedAtUtc: DateTime.UtcNow.AddSeconds(-5));

            TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            // A crash loop keeps its history: the counter is left alone below the threshold
            Assert.Equal(3, store.Attempts);
            Assert.Empty(store.Writes);

            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("Resetting restart attempts counter")), It.IsAny<Exception>()),
                Times.Never);
        }

        [Fact]
        public async Task CheckHealth_PreLaunchEnabled_AddsPreLaunchTimeoutToResetThreshold()
        {
            // Arrange: 45s clears the 31s base threshold, and must not clear 31s + 300s of pre-launch budget
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 3, updatedAtUtc: DateTime.UtcNow.AddSeconds(-45));

            TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1, preLaunchTimeoutInSeconds: 300));
            TestReflection.SetField(service, "_preLaunchEnabled", true);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            Assert.Equal(3, store.Attempts);
            Assert.Empty(store.Writes);

            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("Resetting restart attempts counter")), It.IsAny<Exception>()),
                Times.Never);
        }

        [Theory]
        [InlineData(3600, 2, 7200)]                                 // window over the cap
        [InlineData(int.MaxValue, 2, int.MaxValue)]                  // product over int.MaxValue, clamped
        public async Task CheckHealth_DetectionWindowExceedsCap_WarnsAndIgnoresTheCap(
            int heartbeatIntervalInSeconds, int maxFailedChecks, int expectedDetectionWindowSeconds)
        {
            // Arrange
            var service = _ctx.Build();
            var store = FakeRestartAttemptsStore.Attach(_ctx.NamedPipesService, attempts: 3, updatedAtUtc: DateTime.UtcNow.AddSeconds(-45));

            TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds, maxFailedChecks));

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);

            service.SetChildProcess(mockProcess.Object);
            service.SetRestartAttemptsServiceName(ServiceName);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            // The warning carries the clamped window, so it also pins the int.MaxValue guard
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.Contains(
                    $"Detection window ({expectedDetectionWindowSeconds}s) exceeds the reset cap ({AppConfig.ConditionalResetMaxThresholdSeconds}s)")),
                It.IsAny<Exception>()),
                Times.Once);

            // The detection window replaces the cap as the threshold, so 45s cannot reset the counter
            Assert.Equal(3, store.Attempts);
        }

        [Fact]
        public async Task CheckHealth_TimerElapsed_ForwardsToTheCoreCheck()
        {
            // Arrange
            var service = _ctx.Build();
            var options = ServiceTestContext.CreateDefaultStartOptions();
            TestReflection.SetField(service, "_options", options);
            TestReflection.SetField(service, "_recoveryActionEnabled", true);

            var timer = new Mock<ITimer>();
            _ctx.TimerFactory.Setup(f => f.Create(It.IsAny<double>())).Returns(timer.Object);
            service.InvokeSetupHealthMonitoring(options);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.None);
            service.SetFailedChecks(0);

            // Act
            timer.Raise(t => t.Elapsed += null, service, (ElapsedEventArgs?)null);

            // Assert
            // CheckHealth is an async void timer handler, so the effect lands off the raising thread
            var deadline = DateTime.UtcNow.AddMilliseconds(TestTimeouts.CiGenerousMs);
            while (service.GetFailedChecks() == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }

            Assert.Equal(1, service.GetFailedChecks());
        }

        [Fact]
        public async Task CheckHealth_SemaphoreDisposedBeforeWait_LogsTeardownAndSkipsTheCheck()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetFailedChecks(0);

            TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore").Dispose();

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Semaphore disposed during wait. Teardown in progress.")), It.IsAny<Exception>()), Times.Once);
            Assert.Equal(0, service.GetFailedChecks());
        }

        [Fact]
        public async Task CheckHealth_CancelledBeforeWait_LogsTeardownAndSkipsTheCheck()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetFailedChecks(0);

            var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            TestReflection.SetField(service, "_cancellationSource", cancellationSource);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("health check cancelled. Teardown in progress.")), It.IsAny<Exception>()), Times.Once);
            Assert.Equal(0, service.GetFailedChecks());
        }

        [Fact]
        public async Task CheckHealth_ChildProcessStateThrows_LogsCriticalError()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var stateFailure = new InvalidOperationException("process state unavailable");
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Throws(stateFailure);

            service.SetChildProcess(mockProcess.Object);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Error("Critical error in health check loop.", stateFailure), Times.Once);
        }

        [Fact]
        public async Task CheckHealth_SemaphoreDisposedInsideTheLock_SwallowsTheReleaseFailure()
        {
            // Arrange
            var service = _ctx.Build();
            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.RecoveryOnCleanExit = true;
            TestReflection.SetField(service, "_options", options);

            var semaphore = TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore");

            var mockProcess = new Mock<IProcessWrapper>();
            // Dispose only once the wait has already succeeded, so the release in the finally
            // is the first call that meets a disposed semaphore
            mockProcess.Setup(p => p.HasExited).Returns(() => { semaphore.Dispose(); return true; });
            mockProcess.Setup(p => p.ExitCode).Returns(0);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            // The check ran to completion: without the catch around the release, the
            // ObjectDisposedException would escape into the outer handler instead
            Assert.Equal(1, service.GetFailedChecks());
            _ctx.Logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Theory]
        [InlineData("_isTearingDown")]
        [InlineData("_isRebooting")]
        [InlineData("_isRecovering")]
        public async Task CheckHealth_BusyFlagSet_ReturnsBeforeTakingTheLock(string flag)
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(-1);

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetFailedChecks(0);
            TestReflection.SetField(service, flag, true);

            // Disposing the semaphore is what makes the fast-fail guard observable on its own: the
            // double-check inside the lock also returns for _isTearingDown and _isRecovering, so
            // "the process is never read" would stay green without the outer guard. Reaching the
            // wait at all now logs, and only the outer guard prevents that.
            TestReflection.GetField<SemaphoreSlim>(service, "_healthCheckSemaphore").Dispose();

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(s =>
                s.Contains("Semaphore disposed during wait")), It.IsAny<Exception>()), Times.Never);
            mockProcess.VerifyGet(p => p.HasExited, Times.Never);
            Assert.Equal(0, service.GetFailedChecks());
        }

        [Fact]
        public async Task CheckHealth_ExitCodeThrows_WarnsAndCountsTheFailureAsUnavailable()
        {
            // Arrange
            var service = _ctx.Build();
            TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Throws(new InvalidOperationException("boom"));

            service.SetChildProcess(mockProcess.Object);
            service.SetMaxFailedChecks(3);
            service.SetRecoveryAction(RecoveryAction.None);
            service.SetFailedChecks(0);

            // Act
            await service.InvokeCheckHealthAsync(null, null);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s =>
                s.Contains("Health check could not read ExitCode") && s.Contains("boom")), It.IsAny<Exception>()),
                Times.Once);
            // The unreadable exit code reaches EvaluateExitOutcome as null and is rendered
            // "unavailable", never as "(0x)" (#6047)
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s =>
                s.Contains("[CheckHealth]") && s.Contains("code unavailable")), It.IsAny<Exception>()),
                Times.Once);
            Assert.Equal(1, service.GetFailedChecks());
        }

        public void Dispose() => _ctx.Dispose();
    }
}
