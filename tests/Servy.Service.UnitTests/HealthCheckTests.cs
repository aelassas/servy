using Moq;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Xunit;
using ITimer = Servy.Service.Timers.ITimer;

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
            var completedTask = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, CancellationToken.None));
            Assert.Same(pingLogged.Task, completedTask);
            Assert.Contains("(flag: fail)", await pingLogged.Task);
        }

        [Fact]
        public async Task CheckHealth_RestartAttemptsExhausted_LogsErrorAndResetsCounter()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "3");

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
                Assert.Equal("0", File.ReadAllText(attemptsFile).Trim());
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
            File.WriteAllText(attemptsFile, "1");

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

                Assert.Equal("2", File.ReadAllText(attemptsFile).Trim());
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
            var completedTask = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, CancellationToken.None));
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
            var completedTask = await Task.WhenAny(pingLogged.Task, Task.Delay(TestTimeouts.CiGenerous, CancellationToken.None));
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
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Health check failed")), It.IsAny<Exception>()), Times.Exactly(3));
            _ctx.Helper.Verify(h => h.RestartProcess(It.IsAny<IProcessWrapper>(), It.IsAny<StartProcessCallback>(),
                                                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                                  It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                                                  Times.Once);
        }

        /// <summary>
        /// Builds a <see cref="StartOptions"/> whose heartbeat fields pin the stability threshold that
        /// <c>ConditionalResetRestartAttempts</c> computes, so a test can choose a file age that
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
        [InlineData("abc")]         // not an integer at all
        [InlineData("-1")]          // parses, but fails the attempts >= 0 test
        public async Task CheckHealth_RestartAttemptsFileCorrupt_WarnsAndRewritesCounterToZero(string content)
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, content);

            try
            {
                TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(false);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetFailedChecks(0);

                // Act
                await service.InvokeCheckHealthAsync(null, null);

                // Assert
                _ctx.Logger.Verify(l => l.Warn(
                    It.Is<string>(s => s.Contains("Corrupt or invalid content found in restart attempts file")), It.IsAny<Exception>()),
                    Times.Once);

                // The counter is rewritten, not merely reported: a later read must find a usable value
                Assert.Equal("0", File.ReadAllText(attemptsFile).Trim());
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
        public async Task CheckHealth_RestartAttemptsFileUnreadable_AbortsRecoveryInsteadOfRestarting()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "1");

            try
            {
                TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(true);
                mockProcess.Setup(p => p.ExitCode).Returns(-1);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetMaxFailedChecks(1);
                service.SetMaxRestartAttempts(3);
                service.SetRecoveryAction(RecoveryAction.RestartProcess);
                service.SetFailedChecks(0);

                // FileShare.None denies the reader inside the service, so the counter cannot be consulted
                using (new FileStream(attemptsFile, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // Act
                    await service.InvokeCheckHealthAsync(null, null);
                }

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
                    It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                    Times.Never);
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
        public async Task CheckHealth_RestartAttemptsFileNotWritable_LogsSaveFailureAndStillRecovers()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "1");

            try
            {
                TestReflection.SetField(service, "_options", ServiceTestContext.CreateDefaultStartOptions());

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(true);
                mockProcess.Setup(p => p.ExitCode).Returns(-1);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetMaxFailedChecks(1);
                service.SetMaxRestartAttempts(3);   // 1 of 3 used => the counter is incremented and saved
                service.SetRecoveryAction(RecoveryAction.RestartProcess);
                service.SetFailedChecks(0);

                // FileShare.Read lets the counter be read but denies the atomic replace that persists it
                using (new FileStream(attemptsFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Act
                    await service.InvokeCheckHealthAsync(null, null);
                }

                // Assert
                _ctx.Logger.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Failed to save restart attempts to")), It.IsAny<Exception>()),
                    Times.Once);

                // A counter that cannot be persisted is logged, not fatal: the recovery action still runs
                _ctx.Helper.Verify(h => h.RestartProcess(
                    It.IsAny<IProcessWrapper>(),
                    It.IsAny<StartProcessCallback>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<List<EnvironmentVariable>>(), It.IsAny<IServyLogger>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                    Times.Once);

                // The write never landed, so the on-disk counter is unchanged
                Assert.Equal("1", File.ReadAllText(attemptsFile).Trim());
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
        public async Task CheckHealth_RestartAttemptsFileOlderThanBoot_TouchesFileAndKeepsCounter()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "3");

            try
            {
                // A year ago is before any plausible boot time, so the reboot-detection arm is taken
                File.SetLastWriteTimeUtc(attemptsFile, DateTime.UtcNow.AddYears(-1));

                TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1));

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(false);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetFailedChecks(0);

                // Act
                await service.InvokeCheckHealthAsync(null, null);

                // Assert
                // The quota survives the reboot, so a RestartComputer recovery cannot loop forever
                Assert.Equal("3", File.ReadAllText(attemptsFile).Trim());

                // The file is anchored to the current session so in-session uptime decides the next check
                Assert.True(File.GetLastWriteTimeUtc(attemptsFile) > DateTime.UtcNow.AddMinutes(-5));

                _ctx.Logger.Verify(l => l.Info(
                    It.Is<string>(s => s.Contains("Resetting restart attempts counter")), It.IsAny<Exception>()),
                    Times.Never);
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
        public async Task CheckHealth_StableLongerThanResetThreshold_ResetsCounterToZero()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "3");

            try
            {
                // Detection window 1s, buffer 30s => threshold 31s; 45s of stability clears it
                File.SetLastWriteTimeUtc(attemptsFile, DateTime.UtcNow.AddSeconds(-45));

                TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1));

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(false);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetFailedChecks(0);

                // Act
                await service.InvokeCheckHealthAsync(null, null);

                // Assert
                _ctx.Logger.Verify(l => l.Info(
                    It.Is<string>(s => s.Contains("Resetting restart attempts counter. Stable for")), It.IsAny<Exception>()),
                    Times.Once);

                Assert.Equal("0", File.ReadAllText(attemptsFile).Trim());
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
        public async Task CheckHealth_StableShorterThanResetThreshold_KeepsCounter()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "3");

            try
            {
                // Same threshold as the reset test (31s), reached from the other side
                File.SetLastWriteTimeUtc(attemptsFile, DateTime.UtcNow.AddSeconds(-5));

                TestReflection.SetField(service, "_options", CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1));

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(false);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetFailedChecks(0);

                // Act
                await service.InvokeCheckHealthAsync(null, null);

                // Assert
                // A crash loop keeps its history: the counter is left alone below the threshold
                Assert.Equal("3", File.ReadAllText(attemptsFile).Trim());

                _ctx.Logger.Verify(l => l.Info(
                    It.Is<string>(s => s.Contains("Resetting restart attempts counter")), It.IsAny<Exception>()),
                    Times.Never);
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
        public async Task CheckHealth_PreLaunchEnabled_AddsPreLaunchTimeoutToResetThreshold()
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "3");

            try
            {
                // 45s clears the 31s base threshold, and must not clear 31s + 300s of pre-launch budget
                File.SetLastWriteTimeUtc(attemptsFile, DateTime.UtcNow.AddSeconds(-45));

                TestReflection.SetField(service, "_options",
                    CreateStabilityOptions(heartbeatIntervalInSeconds: 1, maxFailedChecks: 1, preLaunchTimeoutInSeconds: 300));
                TestReflection.SetField(service, "_preLaunchEnabled", true);

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(false);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
                service.SetFailedChecks(0);

                // Act
                await service.InvokeCheckHealthAsync(null, null);

                // Assert
                Assert.Equal("3", File.ReadAllText(attemptsFile).Trim());

                _ctx.Logger.Verify(l => l.Info(
                    It.Is<string>(s => s.Contains("Resetting restart attempts counter")), It.IsAny<Exception>()),
                    Times.Never);
            }
            finally
            {
                if (File.Exists(attemptsFile))
                {
                    try { File.Delete(attemptsFile); } catch { /* teardown is best-effort */ }
                }
            }
        }

        [Theory]
        [InlineData(3600, 2, 7200)]                                 // window over the cap
        [InlineData(int.MaxValue, 2, int.MaxValue)]                  // product over int.MaxValue, clamped
        public async Task CheckHealth_DetectionWindowExceedsCap_WarnsAndIgnoresTheCap(
            int heartbeatIntervalInSeconds, int maxFailedChecks, int expectedDetectionWindowSeconds)
        {
            // Arrange
            var service = _ctx.Build();
            var attemptsFile = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.dat");
            File.WriteAllText(attemptsFile, "3");

            try
            {
                File.SetLastWriteTimeUtc(attemptsFile, DateTime.UtcNow.AddSeconds(-45));

                TestReflection.SetField(service, "_options",
                    CreateStabilityOptions(heartbeatIntervalInSeconds, maxFailedChecks));

                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(false);

                service.SetChildProcess(mockProcess.Object);
                service.SetRestartAttemptsFile(attemptsFile);
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
                Assert.Equal("3", File.ReadAllText(attemptsFile).Trim());
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
            timer.Raise(t => t.Elapsed += null, service, (ElapsedEventArgs)null);

            // Assert
            // CheckHealth is an async void timer handler, so the effect lands off the raising thread
            var deadline = DateTime.UtcNow.AddMilliseconds(TestTimeouts.CiGenerousMs);
            while (service.GetFailedChecks() == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25, CancellationToken.None);
            }

            Assert.Equal(1, service.GetFailedChecks());
        }

        public void Dispose() => _ctx.Dispose();
    }
}
