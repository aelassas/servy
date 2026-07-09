using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Timers;
using Xunit;
using IServiceHelper = Servy.Service.Helpers.IServiceHelper;
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
            var fullArgs = new[] { "servy.exe" };
            var expectedOptions = new StartOptions { ServiceName = "TestService" };

            var mockHelper = new Mock<IServiceHelper>();
            var mockRootLogger = new Mock<IServyLogger>();
            var mockScopedLogger = new Mock<IServyLogger>();

            var streamWriterFactory = new Mock<IStreamWriterFactory>();
            var timerFactory = new Mock<ITimerFactory>();
            var processFactory = new Mock<IProcessFactory>();
            var pathValidator = new Mock<IPathValidator>();
            var serviceRepository = new Mock<IServiceRepository>();

            // 1. Setup the ServiceHelper sequence
            mockHelper.Setup(h => h.GetArgs()).Returns(fullArgs);
            mockHelper.Setup(h => h.ParseOptions(serviceRepository.Object, fullArgs))
                      .Returns(expectedOptions);

            // 2. Setup Logger Promotion (Root -> Scoped)
            mockRootLogger.Setup(l => l.CreateScoped(expectedOptions.ServiceName))
                          .Returns(mockScopedLogger.Object);

            // 3. Setup Validation and Working Directory check (using the SCOPED logger)
            mockHelper.Setup(h => h.ValidateAndLog(expectedOptions, mockScopedLogger.Object))
                      .Returns(true);
            mockHelper.Setup(h => h.EnsureValidWorkingDirectory(expectedOptions, mockScopedLogger.Object));

            using (var service = new TestableService(
                mockHelper.Object,
                mockRootLogger.Object,
                streamWriterFactory.Object,
                timerFactory.Object,
                processFactory.Object,
                pathValidator.Object,
                serviceRepository.Object,
                _mockProcessKiller.Object
                ))
            {
                // Act
                service.TestOnStart();

                // Assert
                // Verify the sequence of orchestration
                mockHelper.Verify(h => h.GetArgs(), Times.Once);
                mockHelper.Verify(h => h.ParseOptions(serviceRepository.Object, fullArgs), Times.Once);

                // Verify logger promotion
                mockRootLogger.Verify(l => l.CreateScoped(expectedOptions.ServiceName), Times.Once);

                // The root logger must NOT be disposed because the scoped logger
                // delegates its underlying EventLog/File operations to it.
                mockRootLogger.Verify(l => l.Dispose(), Times.Never);

                // Verify validation and working directory check used the NEW scoped logger
                mockHelper.Verify(h => h.ValidateAndLog(expectedOptions, mockScopedLogger.Object), Times.Once);
                mockHelper.Verify(h => h.EnsureValidWorkingDirectory(expectedOptions, mockScopedLogger.Object), Times.Once);
            }
        }

        [Fact]
        public void OnStart_WhenParseOptionsReturnsNull_DoesNotCallEnsureValidWorkingDirectory()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<IServyLogger>();
            var streamWriterFactory = new Mock<IStreamWriterFactory>();
            var timerFactory = new Mock<ITimerFactory>();
            var processFactory = new Mock<IProcessFactory>();
            var pathValidator = new Mock<IPathValidator>();
            var serviceRepository = new Mock<IServiceRepository>();

            // 1. Mock GetArgs to return a valid array
            mockHelper.Setup(h => h.GetArgs()).Returns(fullArgs);

            // 2. Mock ParseOptions to return null
            mockHelper
                .Setup(h => h.ParseOptions(serviceRepository.Object, fullArgs))
                .Returns((StartOptions)null);

            using (var service = new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                streamWriterFactory.Object,
                timerFactory.Object,
                processFactory.Object,
                pathValidator.Object,
                serviceRepository.Object,
                _mockProcessKiller.Object
                ))
            {
                // Act
                service.TestOnStart(fullArgs);

                // Assert
                // Verify we attempted to parse but stopped there
                mockHelper.Verify(h => h.ParseOptions(serviceRepository.Object, fullArgs), Times.Once);

                // Verify that subsequent steps (Promotion/Validation/WorkingDir) were NEVER reached
                mockLogger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
                mockHelper.Verify(h => h.ValidateAndLog(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
                mockHelper.Verify(h => h.EnsureValidWorkingDirectory(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
            }
        }

        [Fact]
        public void OnStart_WhenExceptionThrown_LogsError()
        {
            // Arrange
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<IServyLogger>();
            var streamWriterFactory = new Mock<IStreamWriterFactory>();
            var timerFactory = new Mock<ITimerFactory>();
            var processFactory = new Mock<IProcessFactory>();
            var pathValidator = new Mock<IPathValidator>();
            var serviceRepository = new Mock<IServiceRepository>();

            var exception = new InvalidOperationException("Test exception");

            // Simulate the exception at the first entry point of OnStart
            mockHelper
                .Setup(h => h.GetArgs())
                .Throws(exception);

            using (var service = new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                streamWriterFactory.Object,
                timerFactory.Object,
                processFactory.Object,
                pathValidator.Object,
                serviceRepository.Object,
                _mockProcessKiller.Object
                ))
            {
                // Act
                service.TestOnStart(new string[] { });

                // Assert
                // Since the crash happens before promotion, mockLogger is still the active logger
                mockLogger.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Exception in OnStart")),
                    exception
                    ), Times.Once);

                // Verify that promotion was never attempted due to the early failure
                mockLogger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
            }
        }

        [Fact]
        public void SetupHealthMonitoring_ValidParameters_CreatesAndStartsTimer_AndLogs()
        {
            // Arrange
            var mockLogger = new Mock<IServyLogger>();
            var mockHelper = new Mock<IServiceHelper>();
            var mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            var mockTimerFactory = new Mock<ITimerFactory>();
            var mockProcessFactory = new Mock<IProcessFactory>();
            var mockPathValidator = new Mock<IPathValidator>();
            var serviceRepository = new Mock<IServiceRepository>();

            var mockTimer = new Mock<ITimer>();

            mockTimerFactory
                .Setup(f => f.Create(It.IsAny<double>()))
                .Returns(mockTimer.Object);

            using (var service = new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                mockStreamWriterFactory.Object,
                mockTimerFactory.Object,
                mockProcessFactory.Object,
                mockPathValidator.Object,
                serviceRepository.Object,
                _mockProcessKiller.Object
            ))
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
                mockTimerFactory.Verify(f => f.Create(options.HeartbeatInterval * 1000.0), Times.Once);

                mockTimer.VerifyAdd(t => t.Elapsed += It.IsAny<ElapsedEventHandler>(), Times.Once);
                mockTimer.VerifySet(t => t.AutoReset = true, Times.Once);
                mockTimer.Verify(t => t.Start(), Times.Once);

                mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Health monitoring started")), It.IsAny<Exception>()), Times.Once);
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
            var mockLogger = new Mock<IServyLogger>();
            var mockHelper = new Mock<IServiceHelper>();
            var mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            var mockTimerFactory = new Mock<ITimerFactory>();
            var mockProcessFactory = new Mock<IProcessFactory>();
            var mockPathValidator = new Mock<IPathValidator>();
            var serviceRepository = new Mock<IServiceRepository>();

            using (var service = new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                mockStreamWriterFactory.Object,
                mockTimerFactory.Object,
                mockProcessFactory.Object,
                mockPathValidator.Object,
                serviceRepository.Object,
                _mockProcessKiller.Object
            ))
            {
                var options = new StartOptions
                {
                    HeartbeatInterval = heartbeat,
                    MaxFailedChecks = maxFailedChecks,
                    RecoveryAction = recovery
                };

                // Act
                service.InvokeSetupHealthMonitoring(options);

                // Assert
                mockTimerFactory.Verify(f => f.Create(It.IsAny<double>()), Times.Never);
                mockLogger.Verify(l => l.Info(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }
    }
}