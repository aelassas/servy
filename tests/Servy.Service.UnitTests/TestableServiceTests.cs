using Moq;
using Servy.Core;
using Servy.Core.Enums;
using Servy.Service.Logging;
using Servy.Service.ProcessManagement;
using Servy.Service.ServiceHelpers;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Timers;
using Xunit;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class TestableServiceTests
    {
        [Fact]
        public void OnStart_CallsInitializeStartup_AndEnsureValidWorkingDirectory()
        {
            // Arrange
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<ILogger>();
            var expectedOptions = new StartOptions();
            var streamWriterFactory = new Mock<IStreamWriterFactory>();
            var timerFactory = new Mock<ITimerFactory>();
            var processFactory = new Mock<IProcessFactory>();
            var pathValidator = new Mock<IPathValidator>();

            mockHelper
                .Setup(h => h.InitializeStartup(mockLogger.Object))
                .Returns(expectedOptions);

            var service = new TestableService(mockHelper.Object, mockLogger.Object, streamWriterFactory.Object, timerFactory.Object, processFactory.Object, pathValidator.Object);

            // Act
            service.TestOnStart(new string[0]);

            // Assert
            mockHelper.Verify(h => h.InitializeStartup(mockLogger.Object), Times.Once);
            mockHelper.Verify(h => h.EnsureValidWorkingDirectory(expectedOptions, mockLogger.Object), Times.Once);
        }

        [Fact]
        public void OnStart_WhenInitializeStartupReturnsNull_DoesNotCallEnsureValidWorkingDirectory()
        {
            // Arrange
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<ILogger>();
            var streamWriterFactory = new Mock<IStreamWriterFactory>();
            var timerFactory = new Mock<ITimerFactory>();
            var processFactory = new Mock<IProcessFactory>();
            var pathValidator = new Mock<IPathValidator>();

            mockHelper
                .Setup(h => h.InitializeStartup(mockLogger.Object))
                .Returns((StartOptions?)null);

            var service = new TestableService(mockHelper.Object, mockLogger.Object, streamWriterFactory.Object, timerFactory.Object, processFactory.Object, pathValidator.Object);

            // Act
            service.TestOnStart(new string[0]);

            // Assert
            mockHelper.Verify(h => h.InitializeStartup(mockLogger.Object), Times.Once);
            mockHelper.Verify(h => h.EnsureValidWorkingDirectory(It.IsAny<StartOptions>(), mockLogger.Object), Times.Never);
        }

        [Fact]
        public void OnStart_WhenExceptionThrown_LogsError()
        {
            // Arrange
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<ILogger>();
            var streamWriterFactory = new Mock<IStreamWriterFactory>();
            var timerFactory = new Mock<ITimerFactory>();
            var processFactory = new Mock<IProcessFactory>();
            var pathValidator = new Mock<IPathValidator>();

            var exception = new InvalidOperationException("Test exception");

            mockHelper
                .Setup(h => h.InitializeStartup(mockLogger.Object))
                .Throws(exception);

            var service = new TestableService(mockHelper.Object, mockLogger.Object, streamWriterFactory.Object, timerFactory.Object, processFactory.Object, pathValidator.Object);

            // Act
            service.TestOnStart(new string[0]);

            // Assert
            mockLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Exception in OnStart")),
                It.IsAny<Exception>()
                ), Times.Once);
        }

        [Fact]
        public void SetupHealthMonitoring_ValidParameters_CreatesAndStartsTimer_AndLogs()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockHelper = new Mock<IServiceHelper>();
            var mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            var mockTimerFactory = new Mock<ITimerFactory>();
            var mockProcessFactory = new Mock<IProcessFactory>();
            var mockPathValidator = new Mock<IPathValidator>();

            var mockTimer = new Mock<ITimer>();

            mockTimerFactory
                .Setup(f => f.Create(It.IsAny<double>()))
                .Returns(mockTimer.Object);

            var service = new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                mockStreamWriterFactory.Object,
                mockTimerFactory.Object,
                mockProcessFactory.Object,
                mockPathValidator.Object
            );

            var options = new StartOptions
            {
                HeartbeatInterval = 5,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartService
            };

            // Act
            service.InvokeSetupHealthMonitoring(options);

            // Assert
            mockTimerFactory.Verify(f => f.Create(options.HeartbeatInterval * 1000), Times.Once);

            mockTimer.VerifyAdd(t => t.Elapsed += It.IsAny<ElapsedEventHandler>(), Times.Once);
            mockTimer.VerifySet(t => t.AutoReset = true, Times.Once);
            mockTimer.Verify(t => t.Start(), Times.Once);

            mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Health monitoring started"))), Times.Once);
        }

        [Theory]
        [InlineData(0, 3, RecoveryAction.RestartService)]
        [InlineData(5, 0, RecoveryAction.RestartService)]
        [InlineData(5, 3, RecoveryAction.None)]
        [InlineData(0, 0, RecoveryAction.None)]
        public void SetupHealthMonitoring_InvalidParameters_DoesNotCreateTimer(int heartbeat, int maxFailedChecks, RecoveryAction recovery)
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockHelper = new Mock<IServiceHelper>();
            var mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            var mockTimerFactory = new Mock<ITimerFactory>();
            var mockProcessFactory = new Mock<IProcessFactory>();
            var mockPathValidator = new Mock<IPathValidator>();

            var service = new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                mockStreamWriterFactory.Object,
                mockTimerFactory.Object,
                mockProcessFactory.Object,
                mockPathValidator.Object
            );

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
            mockLogger.Verify(l => l.Info(It.IsAny<string>()), Times.Never);
        }

    }

}
