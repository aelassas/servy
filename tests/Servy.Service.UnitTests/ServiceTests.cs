using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System.Diagnostics;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class ServiceTests
    {
        private readonly Mock<IServiceHelper> _mockServiceHelper;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IStreamWriterFactory> _mockStreamWriterFactory;
        private readonly Mock<ITimerFactory> _mockTimerFactory;
        private readonly Mock<IProcessFactory> _mockProcessFactory;
        private readonly Mock<IPathValidator> _mockPathValidator;
        private readonly Service _service;

        private readonly Mock<IStreamWriter> _mockStdoutWriter;
        private readonly Mock<IStreamWriter> _mockStderrWriter;
        private readonly Mock<ITimer> _mockTimer;
        private readonly Mock<IProcessWrapper> _mockProcess;
        private readonly Mock<IServiceRepository> _mockServiceRepository;

        public ServiceTests()
        {
            _mockServiceHelper = new Mock<IServiceHelper>();
            _mockLogger = new Mock<ILogger>();
            _mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            _mockTimerFactory = new Mock<ITimerFactory>();
            _mockProcessFactory = new Mock<IProcessFactory>();
            _mockPathValidator = new Mock<IPathValidator>();

            _mockStdoutWriter = new Mock<IStreamWriter>();
            _mockStderrWriter = new Mock<IStreamWriter>();
            _mockTimer = new Mock<ITimer>();
            _mockProcess = new Mock<IProcessWrapper>();

            _mockStreamWriterFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns((string path, bool enableSizeRotation, long size, bool enableDateRotation, DateRotationType dateRotationType, int maxRotations, bool useLocalTimeForRotation) =>
                {
                    if (path.Contains("stdout"))
                        return _mockStdoutWriter.Object;
                    else if (path.Contains("stderr"))
                        return _mockStderrWriter.Object;
                    return null;
                });

            _mockTimerFactory.Setup(f => f.Create(It.IsAny<double>()))
                .Returns(_mockTimer.Object);

            _mockProcessFactory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<ILogger>()))
                .Returns(_mockProcess.Object);

            _mockServiceRepository = new Mock<IServiceRepository>();

            _service = new Service(
                _mockServiceHelper.Object,
                _mockLogger.Object,
                _mockStreamWriterFactory.Object,
                _mockTimerFactory.Object,
                _mockProcessFactory.Object,
                _mockPathValidator.Object,
                _mockServiceRepository.Object
            );
        }

        [Fact]
        public void OnStart_ValidOptions_InitializesCorrectly()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" }; // Arguments returned by Helper
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                WorkingDirectory = "C:\\Windows",
                EnableHealthMonitoring = true,
                HeartbeatInterval = 10,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartProcess,
                StdOutPath = "C:\\Logs\\stdout.log",
                StdErrPath = "C:\\Logs\\stderr.log"
            };

            var mockScopedLogger = new Mock<ILogger>();

            // 1. ServiceHelper flow
            _mockServiceHelper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _mockServiceHelper.Setup(h => h.ParseOptions(_mockServiceRepository.Object, It.IsAny<string[]>()))
                .Returns(options);

            // 2. Logger Promotion setup
            // This is critical: the service will now use mockScopedLogger.Object for everything else
            _mockLogger.Setup(l => l.CreateScoped(options.ServiceName)).Returns(mockScopedLogger.Object);

            // 3. Validation setup (Must use the scoped logger)
            _mockServiceHelper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object, fullArgs))
                .Returns(true);

            // 4. Path Validator setup (Used inside HandleLogWriters)
            _mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            // Act
            _service.StartForTest();

            // Assert
            // Verify Health Monitoring started
            // 10 seconds * 1000ms = 10000
            _mockTimerFactory.Verify(f => f.Create(10000), Times.Once);
            _mockTimer.Verify(t => t.Start(), Times.Once);

            // Verify the scoped logger received the success message
            mockScopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Health monitoring started."))), Times.Once);
        }

        [Fact]
        public void OnStart_InvalidStdOutPath_LogsError()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                StdOutPath = "InvalidPath???",
                StdErrPath = string.Empty,
                RecoveryAction = RecoveryAction.None
            };

            var mockScopedLogger = new Mock<ILogger>();

            // 1. Setup the ServiceHelper flow
            _mockServiceHelper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _mockServiceHelper.Setup(h => h.ParseOptions(_mockServiceRepository.Object, fullArgs))
                .Returns(options);

            // 2. Setup Logger Promotion: Root returns Scoped
            _mockLogger.Setup(l => l.CreateScoped(options.ServiceName))
                .Returns(mockScopedLogger.Object);

            // 3. Setup Validation: Must return true for the method to proceed to HandleLogWriters
            _mockServiceHelper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object, fullArgs))
                .Returns(true);

            // 4. Force the path validation to fail
            _mockPathValidator.Setup(v => v.IsValidPath(options.StdOutPath)).Returns(false);

            // Act
            _service.StartForTest();

            // Assert
            // Verify the error was logged to the SCOPED logger, not the root _mockLogger
            mockScopedLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Invalid log file path")),
                It.IsAny<Exception>()
                ), Times.AtLeastOnce);

            // Verify root logger was disposed after promotion
            _mockLogger.Verify(l => l.Dispose(), Times.Once);
        }

        [Fact]
        public void OnStart_NullOptions_StopsService()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            bool stopped = false;

            // Subscribe to the test event to verify the service actually stops
            _service.OnStoppedForTest += () => stopped = true;

            // 1. Mock GetArgs to return a valid array
            _mockServiceHelper.Setup(h => h.GetArgs()).Returns(fullArgs);

            // 2. Mock ParseOptions to return null (simulating invalid or missing configuration)
            _mockServiceHelper.Setup(h => h.ParseOptions(_mockServiceRepository.Object, fullArgs))
                .Returns((StartOptions?)null);

            // Act
            _service.StartForTest();

            // Assert
            // Verify the service stopped because options were null
            Assert.True(stopped);

            // Verify that ValidateAndLog was NEVER called because we exited early
            _mockServiceHelper.Verify(h => h.ValidateAndLog(It.IsAny<StartOptions>(), It.IsAny<ILogger>(), It.IsAny<string[]>()), Times.Never);
        }

        [Fact]
        public void OnStart_ExceptionInGetArgs_StopsServiceAndLogsError()
        {
            // Arrange
            bool stopped = false;
            var testException = new Exception("Boom");

            // Subscribe to the test event to verify the service actually stops
            _service.OnStoppedForTest += () => stopped = true;

            // Simulate an exception at the very beginning of the OnStart sequence
            _mockServiceHelper.Setup(h => h.GetArgs()).Throws(testException);

            // Act
            _service.StartForTest();

            // Assert
            // 1. Verify the service triggered a stop
            Assert.True(stopped);

            // 2. Verify the error was logged to the root logger
            // (Promotion hasn't happened yet, so _mockLogger is still the active logger)
            _mockLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Exception in OnStart")),
                testException
            ), Times.Once);

            // 3. Verify that the logger was never promoted/disposed due to early failure
            _mockLogger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SetProcessPriority_ValidPriority_SetsPriorityAndLogsInfo()
        {
            // Arrange
            var mockProcess = new Mock<IProcessWrapper>();
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
                mockPathValidator.Object,
                _mockServiceRepository.Object
            );
            service.SetChildProcess(mockProcess.Object);

            mockProcess.SetupProperty(p => p.PriorityClass);

            // Act
            service.InvokeSetProcessPriority(ProcessPriorityClass.High);

            // Assert
            mockProcess.VerifySet(p => p.PriorityClass = ProcessPriorityClass.High, Times.Once);
            mockLogger.Verify(l => l.Info(It.Is<string>(msg => msg.Contains("Set process priority to High"))), Times.Once);
        }

        [Fact]
        public void SetProcessPriority_ExceptionThrown_LogsWarning()
        {
            // Arrange
            var mockProcess = new Mock<IProcessWrapper>();
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
                mockPathValidator.Object,
                _mockServiceRepository.Object
            );
            service.SetChildProcess(mockProcess.Object);

            mockProcess.SetupSet(p => p.PriorityClass = It.IsAny<ProcessPriorityClass>())
                       .Throws(new Exception("Priority error"));

            // Act
            service.InvokeSetProcessPriority(ProcessPriorityClass.High);

            // Assert
            mockLogger.Verify(l => l.Warn(It.Is<string>(msg => msg.Contains("Failed to set priority") && msg.Contains("Priority error"))), Times.Once);
        }

        [Fact]
        public void HandleLogWriters_ValidPaths_CreatesStreamWriters()
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
                mockPathValidator.Object,
                _mockServiceRepository.Object
            );

            var options = new StartOptions
            {
                StdOutPath = "valid_stdout.log",
                StdErrPath = "valid_stderr.log",
                RotationSizeInBytes = 12345,
                UseLocalTimeForRotation = true,
            };

            // Simulate Helper.IsValidPath always true for testing
            HelperOverride.IsValidPathOverride = path => true;

            var mockStdOutWriter = new Mock<IStreamWriter>();
            var mockStdErrWriter = new Mock<IStreamWriter>();

            mockStreamWriterFactory.Setup(f => f.Create(options.StdOutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation))
                .Returns(mockStdOutWriter.Object);

            mockStreamWriterFactory.Setup(f => f.Create(options.StdErrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation))
                .Returns(mockStdErrWriter.Object);

            mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert
            mockStreamWriterFactory.Verify(f => f.Create(options.StdOutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation), Times.Once);
            mockStreamWriterFactory.Verify(f => f.Create(options.StdErrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation), Times.Once);

            // Check no errors logged
            mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

            // Cleanup helper override
            HelperOverride.IsValidPathOverride = null;
        }

        [Fact]
        public void HandleLogWriters_InvalidPaths_LogsErrors()
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
                mockPathValidator.Object,
                _mockServiceRepository.Object
            );

            var options = new StartOptions
            {
                StdOutPath = "invalid_stdout.log",
                StdErrPath = "invalid_stderr.log",
                RotationSizeInBytes = 12345
            };

            // Simulate Helper.IsValidPath always false for testing invalid paths
            HelperOverride.IsValidPathOverride = path => false;

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert
            mockStreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);

            mockLogger.Verify(l => l.Error(It.Is<string>(msg => msg.Contains("Invalid log file path")), null), Times.Exactly(2));

            // Cleanup helper override
            HelperOverride.IsValidPathOverride = null;
        }

        [Fact]
        public void HandleLogWriters_EmptyPaths_DoesNotCreateWritersOrLog()
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
                mockPathValidator.Object,
                _mockServiceRepository.Object
            );

            var options = new StartOptions
            {
                StdOutPath = "",
                StdErrPath = string.Empty,
                RotationSizeInBytes = 12345,
                MaxRotations = 5,
            };

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert
            mockStreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
            mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }


    }
}
