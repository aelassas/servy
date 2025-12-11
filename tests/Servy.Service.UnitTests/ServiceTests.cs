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
using System;
using System.Diagnostics;
using Xunit;
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

            _mockStreamWriterFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>()))
                .Returns((string path, bool enableSizeRotation, long size, bool enableDateRotation, DateRotationType dateRotationType, int maxRotations) =>
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
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                ExecutableArgs = "",
                WorkingDirectory = "C:\\Windows",
                Priority = ProcessPriorityClass.Normal,
                StdOutPath = "C:\\Logs\\stdout.log",
                StdErrPath = "C:\\Logs\\stderr.log",
                RotationSizeInBytes = 1024,
                HeartbeatInterval = 10,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartProcess,
                MaxRestartAttempts = 2
            };

            _mockServiceHelper.Setup(h => h.InitializeStartup(_mockLogger.Object))
                .Returns(options);

            _mockServiceHelper.Setup(h => h.EnsureValidWorkingDirectory(options, _mockLogger.Object));
            _mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            // Act
            _service.StartForTest(new string[] { });

            // Assert
            _mockStreamWriterFactory.Verify(f => f.Create(options.StdOutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations), Times.Once);
            _mockStreamWriterFactory.Verify(f => f.Create(options.StdErrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations), Times.Once);

            _mockServiceHelper.Verify(h => h.EnsureValidWorkingDirectory(options, _mockLogger.Object), Times.Once);

            _mockTimerFactory.Verify(f => f.Create(10 * 1000), Times.Once);
            _mockTimer.Verify(t => t.Start(), Times.Once);

            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Health monitoring started."))), Times.Once);
        }

        [Fact]
        public void OnStart_InvalidStdOutPath_LogsError()
        {
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                StdOutPath = "InvalidPath???",
                StdErrPath = string.Empty,
                RotationSizeInBytes = 1024,
                HeartbeatInterval = 0,
                MaxFailedChecks = 0,
                RecoveryAction = RecoveryAction.None,
                MaxRestartAttempts = 1
            };

            _mockServiceHelper.Setup(h => h.InitializeStartup(_mockLogger.Object)).Returns(options);
            _mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(false);

            // Act
            _service.StartForTest(new string[] { });

            // Assert
            _mockLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Invalid log file path")),
                It.IsAny<Exception>()
                ), Times.Once);
        }

        [Fact]
        public void OnStart_NullOptions_StopsService()
        {
            bool stopped = false;
            _service.OnStoppedForTest += () => stopped = true;

            _mockServiceHelper.Setup(h => h.InitializeStartup(_mockLogger.Object)).Returns((StartOptions)null);

            _service.StartForTest(new string[] { });

            Assert.True(stopped);
        }

        [Fact]
        public void OnStart_ExceptionInInitialize_StopsServiceAndLogsError()
        {
            bool stopped = false;
            _service.OnStoppedForTest += () => stopped = true;

            _mockServiceHelper.Setup(h => h.InitializeStartup(_mockLogger.Object)).Throws(new Exception("Boom"));

            _service.StartForTest(new string[] { });

            Assert.True(stopped);
            _mockLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Exception in OnStart")),
                It.IsAny<Exception>()
            ), Times.Once);
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
            mockLogger.Verify(l => l.Warning(It.Is<string>(msg => msg.Contains("Failed to set priority") && msg.Contains("Priority error"))), Times.Once);
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
                RotationSizeInBytes = 12345
            };

            // Simulate Helper.IsValidPath always true for testing
            HelperOverride.IsValidPathOverride = path => true;

            var mockStdOutWriter = new Mock<IStreamWriter>();
            var mockStdErrWriter = new Mock<IStreamWriter>();

            mockStreamWriterFactory.Setup(f => f.Create(options.StdOutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations))
                .Returns(mockStdOutWriter.Object);

            mockStreamWriterFactory.Setup(f => f.Create(options.StdErrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations))
                .Returns(mockStdErrWriter.Object);

            mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert
            mockStreamWriterFactory.Verify(f => f.Create(options.StdOutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations), Times.Once);
            mockStreamWriterFactory.Verify(f => f.Create(options.StdErrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations), Times.Once);

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
            mockStreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>()), Times.Never);

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
            mockStreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>()), Times.Never);
            mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }


    }
}
