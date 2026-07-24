using Moq;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.UnitTests.Utilities;
using System;
using System.Timers;
using Xunit;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class TestableServiceTests
    {
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public TestableServiceTests()
        {
            _mockProcessKiller = new Mock<IProcessKiller>();
        }

        [Fact]
        public void OnStart_Workflow_ParsesPromotesAndValidates()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var fullArgs = new[] { "servy.exe" };
            var expectedOptions = new StartOptions { ServiceName = "TestService" };

            var mockScopedLogger = new Mock<IServyLogger>();

            // 1. Setup the ServiceHelper sequence
            ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);
            ctx.Helper.Setup(h => h.ParseOptions(ctx.ServiceRepository.Object, fullArgs))
                      .Returns(expectedOptions);

            // 2. Setup Logger Promotion (Root -> Scoped)
            ctx.Logger.Setup(l => l.CreateScoped(expectedOptions.ServiceName))
                       .Returns(mockScopedLogger.Object);

            // 3. Setup Validation and Working Directory check (using the SCOPED logger)
            ctx.Helper.Setup(h => h.ValidateAndLog(expectedOptions, mockScopedLogger.Object))
                      .Returns(true);
            ctx.Helper.Setup(h => h.EnsureValidWorkingDirectory(expectedOptions, mockScopedLogger.Object));

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                // Act
                service.TestOnStart();

                // Assert
                // Verify the sequence of orchestration
                ctx.Helper.Verify(h => h.GetArgs(), Times.Once);
                ctx.Helper.Verify(h => h.ParseOptions(ctx.ServiceRepository.Object, fullArgs), Times.Once);

                // Verify logger promotion
                ctx.Logger.Verify(l => l.CreateScoped(expectedOptions.ServiceName), Times.Once);

                // The root logger must NOT be disposed because the scoped logger
                // delegates its underlying EventLog/File operations to it.
                ctx.Logger.Verify(l => l.Dispose(), Times.Never);

                // Verify validation and working directory check used the NEW scoped logger
                ctx.Helper.Verify(h => h.ValidateAndLog(expectedOptions, mockScopedLogger.Object), Times.Once);
                ctx.Helper.Verify(h => h.EnsureValidWorkingDirectory(expectedOptions, mockScopedLogger.Object), Times.Once);
            }
        }

        [Fact]
        public void OnStart_WhenParseOptionsReturnsNull_DoesNotCallEnsureValidWorkingDirectory()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var fullArgs = new[] { "servy.exe" };

            // 1. Mock GetArgs to return a valid array
            ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);

            // 2. Mock ParseOptions to return null
            ctx.Helper
                .Setup(h => h.ParseOptions(ctx.ServiceRepository.Object, fullArgs))
                .Returns((StartOptions)null);

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                // Act
                service.TestOnStart(fullArgs);

                // Assert
                // Verify we attempted to parse but stopped there
                ctx.Helper.Verify(h => h.ParseOptions(ctx.ServiceRepository.Object, fullArgs), Times.Once);

                // Verify that subsequent steps (Promotion/Validation/WorkingDir) were NEVER reached
                ctx.Logger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
                ctx.Helper.Verify(h => h.ValidateAndLog(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
                ctx.Helper.Verify(h => h.EnsureValidWorkingDirectory(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
            }
        }

        [Fact]
        public void OnStart_WhenExceptionThrown_LogsError()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var exception = new InvalidOperationException("Test exception");

            // Simulate the exception at the first entry point of OnStart
            ctx.Helper
                .Setup(h => h.GetArgs())
                .Throws(exception);

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                // Act
                service.TestOnStart(new string[] { });

                // Assert
                // Since the crash happens before promotion, mockLogger is still the active logger
                ctx.Logger.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Exception in OnStart")),
                    exception
                    ), Times.Once);

                // Verify that promotion was never attempted due to the early failure
                ctx.Logger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
            }
        }

        [Fact]
        public void SetupHealthMonitoring_ValidParameters_CreatesAndStartsTimer_AndLogs()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var mockTimer = new Mock<ITimer>();

            ctx.TimerFactory
                .Setup(f => f.Create(It.IsAny<double>()))
                .Returns(mockTimer.Object);

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                service.SetRecoveryActionEnabled(true);

                var options = new StartOptions
                {
                    HeartbeatInterval = 5,
                    MaxFailedChecks = 3,
                    RecoveryAction = RecoveryAction.RestartService,
                    EnableHealthMonitoring = true,
                };

                // Act
                service.InvokeSetupHealthMonitoring(options);

                // Assert
                ctx.TimerFactory.Verify(f => f.Create(options.HeartbeatInterval * 1000.0), Times.Once);

                mockTimer.VerifyAdd(t => t.Elapsed += It.IsAny<ElapsedEventHandler>(), Times.Once);
                mockTimer.VerifySet(t => t.AutoReset = true, Times.Once);
                mockTimer.Verify(t => t.Start(), Times.Once);

                ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Health monitoring started")), It.IsAny<Exception>()), Times.Once);
            }
        }

        [Theory]
        [InlineData(0, 3, RecoveryAction.RestartService)]
        [InlineData(5, 0, RecoveryAction.RestartService)]
        [InlineData(5, 3, RecoveryAction.None)]
        [InlineData(0, 0, RecoveryAction.None)]
        public void SetupHealthMonitoring_InvalidParameters_DoesNotCreateTimer(int heartbeat, int maxFailedChecks, RecoveryAction recovery)
        {
            // Arrange
            var ctx = new ServiceTestContext();

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                var options = new StartOptions
                {
                    EnableHealthMonitoring = true, // Attempt to request monitoring
                    HeartbeatInterval = heartbeat,
                    MaxFailedChecks = maxFailedChecks,
                    RecoveryAction = recovery
                };

                // Actively calculate the true production gating state based on configuration validation rules
                bool calculatedRecoveryActionEnabled = options.EnableHealthMonitoring
                    && options.HeartbeatInterval > 0
                    && options.MaxFailedChecks > 0
                    && options.RecoveryAction != RecoveryAction.None;

                service.SetRecoveryActionEnabled(calculatedRecoveryActionEnabled);

                // Act
                service.InvokeSetupHealthMonitoring(options);

                // Assert
                ctx.TimerFactory.Verify(f => f.Create(It.IsAny<double>()), Times.Never);
                ctx.Logger.Verify(l => l.Info(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }
    }
}