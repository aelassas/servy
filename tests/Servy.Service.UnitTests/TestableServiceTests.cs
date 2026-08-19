using Moq;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Testing;
using System.Timers;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class TestableServiceTests : IDisposable
    {
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        [Fact]
        public void OnStart_Workflow_ParsesPromotesAndValidates()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            var expectedOptions = new StartOptions { ServiceName = "TestService" };

            var mockScopedLogger = new Mock<IServyLogger>();

            // 1. Setup the ServiceHelper sequence
            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _ctx.Helper.Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, fullArgs))
                       .Returns(expectedOptions);

            // 2. Setup Logger Promotion (Root -> Scoped)
            _ctx.Logger.Setup(l => l.CreateScoped(expectedOptions.ServiceName))
                       .Returns(mockScopedLogger.Object);

            // 3. Setup Validation and Working Directory check (using the SCOPED logger)
            _ctx.Helper.Setup(h => h.ValidateAndLog(expectedOptions, mockScopedLogger.Object))
                       .Returns(true);
            _ctx.Helper.Setup(h => h.EnsureValidStartupDirectory(expectedOptions, mockScopedLogger.Object));

            var service = _ctx.Build();

            // Act
            service.StartForTest();

            // Assert
            // Verify the sequence of orchestration
            _ctx.Helper.Verify(h => h.GetArgs(), Times.Once);
            _ctx.Helper.Verify(h => h.ParseOptions(_ctx.ServiceRepository.Object, fullArgs), Times.Once);

            // Verify logger promotion
            _ctx.Logger.Verify(l => l.CreateScoped(expectedOptions.ServiceName), Times.Once);

            // The root logger must NOT be disposed because the scoped logger
            // delegates its underlying EventLog/File operations to it.
            _ctx.Logger.Verify(l => l.Dispose(), Times.Never);

            // Verify validation and working directory check used the NEW scoped logger
            _ctx.Helper.Verify(h => h.ValidateAndLog(expectedOptions, mockScopedLogger.Object), Times.Once);
            _ctx.Helper.Verify(h => h.EnsureValidStartupDirectory(expectedOptions, mockScopedLogger.Object), Times.Once);
        }

        [Fact]
        public void OnStart_WhenParseOptionsReturnsNull_DoesNotCallEnsureValidStartupDirectory()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };

            // 1. Mock GetArgs to return a valid array
            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);

            // 2. Mock ParseOptions to return null
            _ctx.Helper
                .Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, fullArgs))
                .Returns((StartOptions?)null);

            var service = _ctx.Build();

            // Act
            service.TestOnStart(fullArgs);

            // Assert
            // Verify we attempted to parse but stopped there
            _ctx.Helper.Verify(h => h.ParseOptions(_ctx.ServiceRepository.Object, fullArgs), Times.Once);

            // Verify that subsequent steps (Promotion/Validation/WorkingDir) were NEVER reached
            _ctx.Logger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
            _ctx.Helper.Verify(h => h.ValidateAndLog(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
            _ctx.Helper.Verify(h => h.EnsureValidStartupDirectory(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
        }

        [Fact]
        public void OnStart_WhenExceptionThrown_LogsError()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Simulate the exception at the first entry point of OnStart
            _ctx.Helper
                .Setup(h => h.GetArgs())
                .Throws(exception);

            var service = _ctx.Build();

            // Act
            service.TestOnStart(new string[] { });

            // Assert
            // Since the crash happens before promotion, mockLogger is still the active logger
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Exception in OnStart")),
                exception
                ), Times.Once);

            // Verify that promotion was never attempted due to the early failure
            _ctx.Logger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false, 5, 3, RecoveryAction.RestartService, false)] // health monitoring off => disabled
        [InlineData(true, 0, 3, RecoveryAction.RestartService, false)]
        [InlineData(true, 5, 0, RecoveryAction.RestartService, false)]
        [InlineData(true, 5, 3, RecoveryAction.None, false)]
        [InlineData(true, 0, 0, RecoveryAction.None, false)]
        [InlineData(true, 5, 3, RecoveryAction.RestartService, true)]
        public void OnStart_ComputesRecoveryActionEnabledFromOptions(
            bool enableHealthMonitoring, int heartbeat, int maxFailedChecks, RecoveryAction recovery, bool expected)
        {
            // Arrange
            var fullArgs = new[] { "TestService" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                EnableHealthMonitoring = enableHealthMonitoring,
                HeartbeatIntervalInSeconds = heartbeat,
                MaxFailedChecks = maxFailedChecks,
                RecoveryAction = recovery
            };

            var mockScopedLogger = new Mock<IServyLogger>();

            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _ctx.Helper.Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, It.IsAny<string[]>())).Returns(options);
            _ctx.Logger.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockScopedLogger.Object);
            _ctx.Helper.Setup(h => h.ValidateAndLog(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>())).Returns(true);
            _ctx.Helper.Setup(h => h.EnsureValidStartupDirectory(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()));

            var service = _ctx.Build();

            // Act
            service.TestOnStart(fullArgs);

            // Assert
            Assert.Equal(expected, TestReflection.GetField<bool>(service, "_recoveryActionEnabled"));
        }

        [Fact]
        public void SetupHealthMonitoring_ValidParameters_CreatesAndStartsTimer_AndLogs()
        {
            // Arrange
            var mockTimer = new Mock<ITimer>();

            _ctx.TimerFactory
                .Setup(f => f.Create(It.IsAny<double>()))
                .Returns(mockTimer.Object);

            var service = _ctx.Build();

            service.SetRecoveryActionEnabled(true);

            var options = new StartOptions
            {
                HeartbeatIntervalInSeconds = 5,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartService,
                EnableHealthMonitoring = true,
            };

            // Act
            service.InvokeSetupHealthMonitoring(options);

            // Assert
            _ctx.TimerFactory.Verify(f => f.Create(options.HeartbeatIntervalInSeconds * 1000.0), Times.Once);

            mockTimer.VerifyAdd(t => t.Elapsed += It.IsAny<ElapsedEventHandler>(), Times.Once);
            mockTimer.VerifySet(t => t.AutoReset = true, Times.Once);
            mockTimer.Verify(t => t.Start(), Times.Once);

            _ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Health monitoring started")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SetupHealthMonitoring_WhenRecoveryDisabled_DoesNotCreateTimer()
        {
            // Arrange
            var service = _ctx.Build();

            service.SetRecoveryActionEnabled(false);

            var options = new StartOptions
            {
                EnableHealthMonitoring = true,
                HeartbeatIntervalInSeconds = 5,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartService
            };

            // Act
            service.InvokeSetupHealthMonitoring(options);

            // Assert
            _ctx.TimerFactory.Verify(f => f.Create(It.IsAny<double>()), Times.Never);
            _ctx.Logger.Verify(l => l.Info(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        public void Dispose() => _ctx.Dispose();
    }
}
