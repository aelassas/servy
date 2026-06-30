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
using System.Diagnostics;
using System.Reflection;
using IServiceHelper = Servy.Service.Helpers.IServiceHelper;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class ServiceTests
    {
        private readonly Mock<IServiceHelper> _mockServiceHelper;
        private readonly Mock<IServyLogger> _mockLogger;
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
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public ServiceTests()
        {
            _mockServiceHelper = new Mock<IServiceHelper>();
            _mockLogger = new Mock<IServyLogger>();
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

            _mockProcessFactory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(_mockProcess.Object);

            _mockServiceRepository = new Mock<IServiceRepository>();
            _mockProcessKiller = new Mock<IProcessKiller>();

            _service = new Service(
                _mockServiceHelper.Object,
                _mockLogger.Object,
                _mockStreamWriterFactory.Object,
                _mockTimerFactory.Object,
                _mockProcessFactory.Object,
                _mockPathValidator.Object,
                _mockServiceRepository.Object,
                _mockProcessKiller.Object
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
                StdoutPath = "C:\\Logs\\stdout.log",
                StderrPath = "C:\\Logs\\stderr.log"
            };

            var mockScopedLogger = new Mock<IServyLogger>();

            // 1. ServiceHelper flow
            _mockServiceHelper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _mockServiceHelper.Setup(h => h.ParseOptions(_mockServiceRepository.Object, It.IsAny<string[]>()))
                .Returns(options);
            _mockProcess.Setup(p => p.Start()).Returns(true);

            // 2. Logger Promotion setup
            // This is critical: the service will now use mockScopedLogger.Object for everything else
            _mockLogger.Setup(l => l.CreateScoped(options.ServiceName)).Returns(mockScopedLogger.Object);

            // 3. Validation setup (Must use the scoped logger)
            _mockServiceHelper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object))
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
            mockScopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Health monitoring started.")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void OnStart_InvalidStdoutPath_LogsError()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                StdoutPath = "InvalidPath???",
                StderrPath = string.Empty,
                RecoveryAction = RecoveryAction.None
            };

            var mockScopedLogger = new Mock<IServyLogger>();

            // 1. Setup the ServiceHelper flow
            _mockServiceHelper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _mockServiceHelper.Setup(h => h.ParseOptions(_mockServiceRepository.Object, fullArgs))
                .Returns(options);

            // 2. Setup Logger Promotion: Root returns Scoped
            _mockLogger.Setup(l => l.CreateScoped(options.ServiceName))
                .Returns(mockScopedLogger.Object);

            // 3. Setup Validation: Must return true for the method to proceed to HandleLogWriters
            _mockServiceHelper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object))
                .Returns(true);

            // 4. Force the path validation to fail
            _mockPathValidator.Setup(v => v.IsValidPath(options.StdoutPath)).Returns(false);

            // Act
            _service.StartForTest();

            // Assert
            // Verify the error was logged to the SCOPED logger, not the root _mockLogger
            mockScopedLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Invalid log file path")),
                It.IsAny<Exception>()
                ), Times.AtLeastOnce);

            // The root logger must NOT be disposed because the scoped logger
            // delegates its underlying EventLog/File operations to it.
            _mockLogger.Verify(l => l.Dispose(), Times.Never);
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
            _mockServiceHelper.Verify(h => h.ValidateAndLog(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
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
            var mockLogger = new Mock<IServyLogger>();
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
                _mockServiceRepository.Object,
                _mockProcessKiller.Object
            );
            service.SetChildProcess(mockProcess.Object);

            mockProcess.SetupProperty(p => p.PriorityClass);

            // Act
            service.InvokeSetProcessPriority(ProcessPriorityClass.High);

            // Assert
            mockProcess.VerifySet(p => p.PriorityClass = ProcessPriorityClass.High, Times.Once);
            mockLogger.Verify(l => l.Info(It.Is<string>(msg => msg.Contains("Set process priority to High")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SetProcessPriority_ExceptionThrown_LogsWarning()
        {
            // Arrange
            var mockProcess = new Mock<IProcessWrapper>();
            var mockLogger = new Mock<IServyLogger>();
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
                _mockServiceRepository.Object,
                _mockProcessKiller.Object
            );
            service.SetChildProcess(mockProcess.Object);

            mockProcess.SetupSet(p => p.PriorityClass = It.IsAny<ProcessPriorityClass>())
                       .Throws(new Exception("Priority error"));

            // Act
            service.InvokeSetProcessPriority(ProcessPriorityClass.High);

            // Assert
            mockLogger.Verify(l => l.Warn(It.Is<string>(msg => msg.Contains("Failed to set priority") && msg.Contains("Priority error")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void HandleLogWriters_ValidPaths_CreatesStreamWriters()
        {
            // Arrange
            var mockLogger = new Mock<IServyLogger>();
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
                _mockServiceRepository.Object,
                _mockProcessKiller.Object
            );

            var options = new StartOptions
            {
                StdoutPath = "valid_stdout.log",
                StderrPath = "valid_stderr.log",
                RotationSizeInBytes = 12345,
                UseLocalTimeForRotation = true,
            };

            // Simulate Helper.IsValidPath always true for testing
            HelperOverride.IsValidPathOverride = path => true;

            var mockStdOutWriter = new Mock<IStreamWriter>();
            var mockStdErrWriter = new Mock<IStreamWriter>();

            mockStreamWriterFactory.Setup(f => f.Create(options.StdoutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation))
                .Returns(mockStdOutWriter.Object);

            mockStreamWriterFactory.Setup(f => f.Create(options.StderrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation))
                .Returns(mockStdErrWriter.Object);

            mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert
            mockStreamWriterFactory.Verify(f => f.Create(options.StdoutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation), Times.Once);
            mockStreamWriterFactory.Verify(f => f.Create(options.StderrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation), Times.Once);

            // Check no errors logged
            mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

            // Cleanup helper override
            HelperOverride.IsValidPathOverride = null;
        }

        [Fact]
        public void HandleLogWriters_InvalidPaths_LogsErrors()
        {
            // Arrange
            var mockLogger = new Mock<IServyLogger>();
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
                _mockServiceRepository.Object,
                _mockProcessKiller.Object
            );

            var options = new StartOptions
            {
                StdoutPath = "invalid_stdout.log",
                StderrPath = "invalid_stderr.log",
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
            var mockLogger = new Mock<IServyLogger>();
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
                _mockServiceRepository.Object,
                _mockProcessKiller.Object
            );

            var options = new StartOptions
            {
                StdoutPath = "",
                StderrPath = string.Empty,
                RotationSizeInBytes = 12345,
                MaxRotations = 5,
            };

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert
            mockStreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
            mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        #region Null, Empty, and Standard Sanitization Tests

        [Theory]
        [InlineData(null, "_")]
        [InlineData("", "_")]
        public void MakeFilenameSafe_NullOrEmptyInput_ReturnsSafeFallback(string? input, string expectedBase)
        {
            // Act
            string result = Service.MakeFilenameSafe(input!);

            // Assert
            Assert.StartsWith(expectedBase, result);

            // The result length minus the expected base prefix length must equal 
            // exactly 6 characters (the length of our deterministic hex short hash).
            Assert.Equal(6, result.Length - expectedBase.Length);
        }

        [Fact]
        public void MakeFilenameSafe_ValidStandardName_AppendsHashSuffix()
        {
            // Arrange
            string input = "service_runtime_log.txt";

            // Act
            string result = Service.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith("service_runtime_log.txt_", result);
            // Verify hash part length is exactly 6 hex characters
            string hashPart = result.Substring("service_runtime_log.txt_".Length);
            Assert.Equal(6, hashPart.Length);
        }

        [Fact]
        public void MakeFilenameSafe_WithInvalidCharacters_ReplacesThemAndAppendsHash()
        {
            // Arrange
            string input = "log:service/v1*production?.txt";
            string expectedPrefix = "log_service_v1_production_.txt_";

            // Act
            string result = Service.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        #endregion

        #region DOS Reserved Device Names & Multi-Extension Edge Cases (Issue #2080)

        [Theory]
        [InlineData("CON")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("COM1")]
        [InlineData("LPT5")]
        public void MakeFilenameSafe_ExactReservedDeviceName_PrependsUnderscore(string reservedName)
        {
            // Arrange
            string expectedPrefix = "_" + reservedName + "_";

            // Act
            string result = Service.MakeFilenameSafe(reservedName);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CON.log", "_CON.log_")]
        [InlineData("NUL.txt", "_NUL.txt_")]
        [InlineData("LPT1.dat", "_LPT1.dat_")]
        public void MakeFilenameSafe_SingleExtensionReservedDeviceName_PrependsUnderscore(string input, string expectedPrefix)
        {
            // Act
            string result = Service.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CON.log.gz", "_CON.log.gz_")]
        [InlineData("NUL.bak.tmp", "_NUL.bak.tmp_")]
        [InlineData("LPT1.foo.bar", "_LPT1.foo.bar_")]
        [InlineData("AUX.spec.json.zip", "_AUX.spec.json.zip_")]
        public void MakeFilenameSafe_MultiExtensionReservedDeviceName_SuccessfullyCatchesAndPrependsUnderscore(string input, string expectedPrefix)
        {
            // Act
            string result = Service.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CONSTANT.log", "CONSTANT.log_")]
        [InlineData("NULLED.bak", "NULLED.bak_")]
        [InlineData("COMPASS.json", "COMPASS.json_")]
        [InlineData("A.CON.log", "A.CON.log_")]
        public void MakeFilenameSafe_NamesContainingReservedWordsAsSubstrings(string safeName, string expectedPrefix)
        {
            // Act
            string result = Service.MakeFilenameSafe(safeName);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        #endregion

        #region Disambiguation & Namespace Collision Resolution (Issue #2118 & #2069)

        [Theory]
        [InlineData("CON", "_CON_")]
        [InlineData("_CON", "__CON_")]
        [InlineData("__CON", "___CON_")]
        [InlineData("CON.log.gz", "_CON.log.gz_")]
        [InlineData("_CON.log.gz", "__CON.log.gz_")]
        public void MakeFilenameSafe_CollidingNamespaceInputs_ResolvesToUniqueFilenames(string input, string expectedPrefix)
        {
            // Act
            string result = Service.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CON  ", "_CON_")]
        [InlineData("CON...", "_CON_")]
        [InlineData("CON.log.gz  ", "_CON.log.gz_")]
        [InlineData("正常_service_name.log.  ", "正常_service_name.log_")]
        public void MakeFilenameSafe_WithTrailingSpacesOrPeriods_NormalizesAndEscapesCorrectly(string input, string expectedPrefix)
        {
            // Act
            string result = Service.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Fact]
        public void MakeFilenameSafe_Issue2069_TrailingVariationsProduceUniqueOutputs()
        {
            // Arrange: Inputs that would natively collide on Win32 filesystems due to trailing strip behaviors
            string nameBase = "MyService";
            string nameWithSpace = "MyService ";
            string nameWithDot = "MyService.";
            string nameWithSpaces = "MyService   ";

            // Act
            string outBase = Service.MakeFilenameSafe(nameBase);
            string outSpace = Service.MakeFilenameSafe(nameWithSpace);
            string outDot = Service.MakeFilenameSafe(nameWithDot);
            string outSpaces = Service.MakeFilenameSafe(nameWithSpaces);

            // Assert: Verify that despite trimming, appending original hashes isolates filenames completely
            Assert.NotEqual(outBase, outSpace);
            Assert.NotEqual(outBase, outDot);
            Assert.NotEqual(outSpace, outDot);
            Assert.NotEqual(outSpace, outSpaces);

            // All must preserve base readability prefixing
            Assert.StartsWith("MyService_", outBase);
            Assert.StartsWith("MyService_", outSpace);
            Assert.StartsWith("MyService_", outDot);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("...")]
        [InlineData(" \t. ")]
        public void MakeFilenameSafe_Issue2069_PathTraversalAndEmptyTrimsAreNeutralized(string input)
        {
            // Act
            string result = Service.MakeFilenameSafe(input);

            // Assert: Directory traversal markers or blank nodes reduce to safe baseline anchors plus hash codes
            Assert.StartsWith("__", result);
            Assert.False(result.Contains(".."), "Output must not contain directory traversal paths.");
        }

        #endregion

        #region Private Test Helpers

        /// <summary>
        /// DRY helper to initialize the base StartOptions and mocks required to get the 
        /// Service through its initial ValidateAndLog and HandleLogWriters phases cleanly.
        /// </summary>
        private Mock<IServyLogger> SetupStandardServiceStart(StartOptions options)
        {
            var fullArgs = new[] { "servy.exe" };
            var mockScopedLogger = new Mock<IServyLogger>();

            _mockServiceHelper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _mockServiceHelper.Setup(h => h.ParseOptions(It.IsAny<IServiceRepository>(), It.IsAny<string[]>())).Returns(options);
            _mockLogger.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockScopedLogger.Object);
            _mockServiceHelper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object)).Returns(true);
            _mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);
            _mockProcess.Setup(p => p.Start()).Returns(true);

            // Default ProcessFactory mock for the main process
            _mockProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == options.ExecutablePath), It.IsAny<IServyLogger>()))
                .Returns(_mockProcess.Object);

            return mockScopedLogger;
        }

        /// <summary>
        /// Uses reflection to instantiate the internal DataReceivedEventArgs class for testing streams.
        /// </summary>
        private DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
        {
            var constructor = typeof(DataReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
            return (DataReceivedEventArgs)constructor?.Invoke(new object[] { data })!;
        }

        #endregion

        #region Pre-Launch Orchestration Tests

        [Fact]
        public void OnStart_PreLaunchFireAndForget_RunsAndContinues()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "test.exe",
                PreLaunchExecutablePath = "prelaunch.exe",
                PreLaunchTimeoutInSeconds = 0 // 0 means Fire and Forget
            };
            var scopedLogger = SetupStandardServiceStart(options);

            var mockPreLaunchProcess = new Mock<IProcessWrapper>();
            mockPreLaunchProcess.Setup(p => p.Start()).Returns(true);
            _mockProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Returns(mockPreLaunchProcess.Object);

            // Act
            _service.StartForTest();

            // Assert
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("fire-and-forget")), It.IsAny<Exception>()), Times.AtLeastOnce);
            _mockProcess.Verify(p => p.Start(), Times.Once); // Main process still starts
        }

        [Fact]
        public void OnStart_PreLaunchSynchronous_Failure_StopsService()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "test.exe",
                PreLaunchExecutablePath = "prelaunch.exe",
                PreLaunchTimeoutInSeconds = 10,
                PreLaunchIgnoreFailure = false, // Critical: don't ignore
                PreLaunchRetryAttempts = 0
            };
            var scopedLogger = SetupStandardServiceStart(options);

            bool stopped = false;
            _service.OnStoppedForTest += () => stopped = true;

            var mockPreLaunchProcess = new Mock<IProcessWrapper>();
            mockPreLaunchProcess.Setup(p => p.Start()).Returns(true);
            mockPreLaunchProcess.Setup(p => p.ExitCode).Returns(1); // Failed exit

            _mockProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Returns(mockPreLaunchProcess.Object);

            // Act
            _service.StartForTest();

            // Assert
            Assert.True(stopped);
            scopedLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("failed after all retry attempts")), null), Times.Once);
            _mockProcess.Verify(p => p.Start(), Times.Never); // Main process should NOT start
        }

        [Fact]
        public void OnStart_PreLaunchSynchronous_IgnoreFailure_Continues()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "test.exe",
                PreLaunchExecutablePath = "prelaunch.exe",
                PreLaunchTimeoutInSeconds = 10,
                PreLaunchIgnoreFailure = true, // Critical: ignore
                PreLaunchRetryAttempts = 0
            };
            var scopedLogger = SetupStandardServiceStart(options);

            var mockPreLaunchProcess = new Mock<IProcessWrapper>();
            mockPreLaunchProcess.Setup(p => p.Start()).Returns(true);
            mockPreLaunchProcess.Setup(p => p.ExitCode).Returns(1); // Failed

            _mockProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Returns(mockPreLaunchProcess.Object);

            // Act
            _service.StartForTest();

            // Assert
            scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Ignoring pre-launch failure")), null), Times.Once);
            _mockProcess.Verify(p => p.Start(), Times.Once); // Main process starts anyway
        }

        #endregion

        #region Stream Redirection Tests

        [Fact]
        public void OnOutputDataReceived_ValidData_WritesToStdoutWriter()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe", StdoutPath = "stdout.log" };
            SetupStandardServiceStart(options);
            _service.StartForTest();

            var onOutputMethod = typeof(Service).GetMethod("OnOutputDataReceived", BindingFlags.NonPublic | BindingFlags.Instance);
            var eventArgs = CreateDataReceivedEventArgs("Test Output Line");

            // Act
            onOutputMethod!.Invoke(_service, new object[] { this, eventArgs });

            // Assert
            _mockStdoutWriter.Verify(w => w.WriteLine("Test Output Line"), Times.Once);
        }

        [Fact]
        public void OnErrorDataReceived_ValidData_WritesToStderrWriter()
        {
            // Arrange
            // StdErrPath must contain "stderr": the class-level IStreamWriterFactory mock
            // routes on that substring and returns null for anything else.
            var options = new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                StderrPath = "test_stderr.log"
            };
            SetupStandardServiceStart(options);
            _service.StartForTest();

            var onErrorMethod = typeof(Service).GetMethod("OnErrorDataReceived", BindingFlags.NonPublic | BindingFlags.Instance);
            var eventArgs = CreateDataReceivedEventArgs("Test Error Line");

            // Act
            onErrorMethod!.Invoke(_service, new object[] { this, eventArgs });

            // Assert
            _mockStderrWriter.Verify(w => w.WriteLine("Test Error Line"), Times.Once);
        }

        [Fact]
        public void OnOutputDataReceived_NullData_DoesNothing()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe", StdoutPath = "out.log" };
            SetupStandardServiceStart(options);
            _service.StartForTest();

            var onOutputMethod = typeof(Service).GetMethod("OnOutputDataReceived", BindingFlags.NonPublic | BindingFlags.Instance);
            var eventArgs = CreateDataReceivedEventArgs(null!);

            // Act
            onOutputMethod!.Invoke(_service, new object[] { this, eventArgs });

            // Assert
            _mockStdoutWriter.Verify(w => w.WriteLine(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Process Exit & Health Monitoring Tests

        [Fact]
        public async Task OnProcessExited_CleanExit_RecoveryDisabled_StopsService()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe", RecoveryOnCleanExit = false, RecoveryAction = RecoveryAction.None };
            var scopedLogger = SetupStandardServiceStart(options);

            bool stopped = false;
            _service.OnStoppedForTest += () => stopped = true;
            _service.StartForTest();

            _mockProcess.Setup(p => p.HasExited).Returns(true);
            _mockProcess.Setup(p => p.ExitCode).Returns(0);

            // Act
            var onProcessExitedMethod = typeof(Service).GetMethod("OnProcessExited", BindingFlags.NonPublic | BindingFlags.Instance);
            onProcessExitedMethod!.Invoke(_service, new object[] { _mockProcess.Object, EventArgs.Empty });

            await Task.Delay(50, TestContext.Current.CancellationToken); // Allow async void to complete inner logic

            // Assert
            Assert.True(stopped);
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Service will stop.")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task OnProcessExited_NonZeroExit_RecoveryEnabled_InitiatesRecovery()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                RecoveryAction = RecoveryAction.RestartProcess,
                EnableHealthMonitoring = true,
                MaxFailedChecks = 1,
                HeartbeatInterval = 10
            };
            var scopedLogger = SetupStandardServiceStart(options);
            _service.StartForTest();

            // CRITICAL 1: Populate the internal volatile state switches so recovery evaluations pass
            var maxFailedField = typeof(Service).GetField("_maxFailedChecks", BindingFlags.NonPublic | BindingFlags.Instance);
            maxFailedField!.SetValue(_service, 1);

            var recoveryActionEnabledField = typeof(Service).GetField("_recoveryActionEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
            recoveryActionEnabledField!.SetValue(_service, true);

            _mockProcess.Setup(p => p.HasExited).Returns(true);
            _mockProcess.Setup(p => p.ExitCode).Returns(1);

            // Act
            var onProcessExitedMethod = typeof(Service).GetMethod("OnProcessExited", BindingFlags.NonPublic | BindingFlags.Instance);

            // This executes up to the first internal await frame
            onProcessExitedMethod!.Invoke(_service, new object[] { _mockProcess.Object, EventArgs.Empty });

            // CRITICAL 2: Force the test context thread pool to yield execution control.
            // This allows the async void state machine to complete its work behind the scenes.
            int retries = 0;
            bool foundLog = false;
            while (retries < 10 && !foundLog)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
                try
                {
                    // Verify if the background loop successfully hit our warning marker block
                    scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Initiating recovery")), null), Times.Once());
                    foundLog = true;
                }
                catch (MockException)
                {
                    retries++;
                }
            }

            // Assert
            Assert.True(foundLog, "The internal recovery sequence was not scheduled or executed within the time limit.");
        }

        [Fact]
        public void CheckHealth_ProcessHealthy_ResetsFailedChecks()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                RecoveryAction = RecoveryAction.RestartProcess,
                EnableHealthMonitoring = true,
                MaxFailedChecks = 3
            };
            var scopedLogger = SetupStandardServiceStart(options);
            _service.StartForTest();

            // Force a transient failure into the field
            var failedChecksField = typeof(Service).GetField("_failedChecks", BindingFlags.NonPublic | BindingFlags.Instance);
            failedChecksField!.SetValue(_service, 1);

            _mockProcess.Setup(p => p.HasExited).Returns(false); // Process is alive

            // Act
            var checkHealthMethod = typeof(Service).GetMethod("CheckHealth", BindingFlags.NonPublic | BindingFlags.Instance);
            checkHealthMethod!.Invoke(_service, new object[] { this, null! });

            // Assert
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("healthy again. Resetting transient failure")), It.IsAny<Exception>()), Times.Once);
            Assert.Equal(0, failedChecksField.GetValue(_service));
        }

        #endregion

        #region Teardown & Custom Command Tests

        [Fact]
        public void OnStop_ExecutesTeardown()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            SetupStandardServiceStart(options);
            _service.StartForTest();

            bool stopped = false;
            _service.OnStoppedForTest += () => stopped = true;

            // Act
            _service.Stop();

            // Assert
            Assert.True(stopped);
        }

        [Fact]
        public void SafeKillProcess_GracefulStop_LogsCorrectly()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _service.StartForTest();

            var safeKillMethod = typeof(Service).GetMethod("SafeKillProcess", BindingFlags.NonPublic | BindingFlags.Instance);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true); // Graceful stop succeeds
            _mockProcess.Setup(p => p.HasExited).Returns(false);

            // Act
            safeKillMethod!.Invoke(_service, new object[] { _mockProcess.Object, 1000 });

            // Assert
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("stopped gracefully")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SafeKillProcess_ForceKill_LogsCorrectly()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _service.StartForTest();

            var safeKillMethod = typeof(Service).GetMethod("SafeKillProcess", BindingFlags.NonPublic | BindingFlags.Instance);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(false); // Graceful stop fails
            _mockProcess.Setup(p => p.HasExited).Returns(false);

            // Act
            safeKillMethod!.Invoke(_service, new object[] { _mockProcess.Object, 1000 });

            // Assert
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("was forcefully terminated")), It.IsAny<Exception>()), Times.Once);
        }

        #endregion
    }
}
