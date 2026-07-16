using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.UnitTests.Helpers;
using Servy.Service.Validation;
using Servy.Testing;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using IServiceHelper = Servy.Service.Helpers.IServiceHelper;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class ServiceTests : IDisposable
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

            // Crucial setup: Stub the StartInfo property so it never returns null during tests
            _mockProcess.Setup(p => p.StartInfo).Returns(new ProcessStartInfo());

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
                .Returns((StartOptions)null);

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
            using (var service = CreateTestableService())
            {
                service.SetChildProcess(_mockProcess.Object);
                _mockProcess.SetupProperty(p => p.PriorityClass);

                // Act
                service.InvokeSetProcessPriority(ProcessPriorityClass.High);

                // Assert
                _mockProcess.VerifySet(p => p.PriorityClass = ProcessPriorityClass.High, Times.Once);
                _mockLogger.Verify(l => l.Info(It.Is<string>(msg => msg.Contains("Set process priority to High")), It.IsAny<Exception>()), Times.Once);
            }
        }

        [Fact]
        public void SetProcessPriority_ExceptionThrown_LogsWarning()
        {
            // Arrange
            using (var service = CreateTestableService())
            {
                service.SetChildProcess(_mockProcess.Object);
                _mockProcess.SetupSet(p => p.PriorityClass = It.IsAny<ProcessPriorityClass>())
                           .Throws(new Exception("Priority error"));

                // Act
                service.InvokeSetProcessPriority(ProcessPriorityClass.High);

                // Assert
                _mockLogger.Verify(l => l.Warn(It.Is<string>(msg => msg.Contains("Failed to set priority") && msg.Contains("Priority error")), It.IsAny<Exception>()), Times.Once);
            }
        }

        [Fact]
        public void HandleLogWriters_ValidPaths_CreatesStreamWriters()
        {
            // Arrange
            using (var service = CreateTestableService())
            {
                var options = new StartOptions
                {
                    StdoutPath = "valid_stdout.log",
                    StderrPath = "valid_stderr.log",
                    RotationSizeInBytes = 12345,
                    UseLocalTimeForRotation = true,
                };

                var mockStdOutWriter = new Mock<IStreamWriter>();
                var mockStdErrWriter = new Mock<IStreamWriter>();

                _mockStreamWriterFactory.Setup(f => f.Create(options.StdoutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation))
                    .Returns(mockStdOutWriter.Object);

                _mockStreamWriterFactory.Setup(f => f.Create(options.StderrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation))
                    .Returns(mockStdErrWriter.Object);

                _mockPathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

                // Act
                service.InvokeHandleLogWriters(options);

                // Assert
                _mockStreamWriterFactory.Verify(f => f.Create(options.StdoutPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation), Times.Once);
                _mockStreamWriterFactory.Verify(f => f.Create(options.StderrPath, options.EnableSizeRotation, options.RotationSizeInBytes, options.EnableDateRotation, options.DateRotationType, options.MaxRotations, options.UseLocalTimeForRotation), Times.Once);

                // Check no errors logged
                _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }

        [Fact]
        public void HandleLogWriters_InvalidPaths_LogsErrors()
        {
            // Arrange
            using (var service = CreateTestableService())
            {
                var options = new StartOptions
                {
                    StdoutPath = "invalid_stdout.log",
                    StderrPath = "invalid_stderr.log",
                    RotationSizeInBytes = 12345
                };

                // Act
                service.InvokeHandleLogWriters(options);

                // Assert
                _mockStreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
                _mockLogger.Verify(l => l.Error(It.Is<string>(msg => msg.Contains("Invalid log file path")), null), Times.Exactly(2));
            }
        }

        [Fact]
        public void HandleLogWriters_EmptyPaths_DoesNotCreateWritersOrLog()
        {
            // Arrange
            using (var service = CreateTestableService())
            {
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
                _mockStreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
                _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }

        #region Null, Empty, and Standard Sanitization Tests

        [Theory]
        [InlineData(null, "_")]
        [InlineData("", "_")]
        public void MakeFilenameSafe_NullOrEmptyInput_ReturnsSafeFallback(string input, string expectedBase)
        {
            // Arrange & Act
            string result = Service.MakeFilenameSafe(input);

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
            // Arrange & Act
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
            // Arrange & Act
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
            // Arrange & Act
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
            // Arrange & Act
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
            // Arrange & Act
            string result = Service.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Fact]
        public void MakeFilenameSafe_TrailingVariationsProduceUniqueOutputs()
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

            var allOutputs = new[] { outBase, outSpace, outDot, outSpaces };

            // Assert: Verify that despite trimming, appending original hashes isolates filenames completely.
            // Grouping the collection ensures all 6 pairwise distinctness paths are fully checked.
            Assert.Equal(allOutputs.Length, allOutputs.Distinct(StringComparer.Ordinal).Count());

            // All must preserve base readability prefixing
            Assert.All(allOutputs, output => Assert.StartsWith("MyService_", output));
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("...")]
        [InlineData(" \t. ")]
        public void MakeFilenameSafe_PathTraversalAndEmptyTrimsAreNeutralized(string input)
        {
            // Arrange & Act
            string result = Service.MakeFilenameSafe(input);

            // Assert: Directory traversal markers or blank nodes reduce to safe baseline anchors plus hash codes
            Assert.StartsWith("__", result);
            Assert.False(result.Contains(".."), "Output must not contain directory traversal paths.");
        }

        #endregion

        #region Private Test Helpers

        /// <summary>
        /// Direct central factory targeting the creation of TestableService instances.
        /// Consolidates individual field setups consistently, ensuring signature immunity across call sites.
        /// </summary>
        private TestableService CreateTestableService()
        {
            return new TestableService(
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

            return mockScopedLogger;
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

            var eventArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("Test Output Line");

            // Act
            TestReflection.InvokeNonPublic(_service, "OnOutputDataReceived", this, eventArgs);

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

            var eventArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("Test Error Line");

            // Act
            TestReflection.InvokeNonPublic(_service, "OnErrorDataReceived", this, eventArgs);

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

            var eventArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(null);

            // Act
            TestReflection.InvokeNonPublic(_service, "OnOutputDataReceived", this, eventArgs);

            // Assert
            _mockStdoutWriter.Verify(w => w.WriteLine(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Process Exit & Health Monitoring Tests

        [Fact]
        public async Task OnProcessExited_CleanExit_RecoveryDisabled_StopsService()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "CleanExitTest",
                ExecutablePath = "test.exe",
                RecoveryOnCleanExit = false,
                RecoveryAction = RecoveryAction.None, // Recovery disabled
                EnableHealthMonitoring = false
            };
            var scopedLogger = SetupStandardServiceStart(options);

            bool stopped = false;
            _service.OnStoppedForTest += () => stopped = true;
            _service.StartForTest();

            _mockProcess.Setup(p => p.HasExited).Returns(true);
            _mockProcess.Setup(p => p.ExitCode).Returns(0); // Clean exit

            // Wire up a completion signal via Moq's Callback to handle the async void continuation cleanly
            var stopLoggedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            scopedLogger
                .Setup(l => l.Info(It.Is<string>(s => s.Contains("Service will stop.")), It.IsAny<Exception>()))
                .Callback(() => stopLoggedSignal.TrySetResult(true));

            // Act
            // Invoke the non-public async void event handler via reflection
            TestReflection.InvokeNonPublic(_service, "OnProcessExited", _mockProcess.Object, EventArgs.Empty);

            // Assert
            // Await either the log confirmation or the state callback completion block
            await Task.WhenAny(stopLoggedSignal.Task, Task.Delay(2000, CancellationToken.None));

            Assert.True(stopped, "The background clean exit stop sequence failed to invoke the OnStoppedForTest event callback.");
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

            // Populate the internal volatile state switches so recovery evaluations pass
            TestReflection.SetField(_service, "_maxFailedChecks", 1);
            TestReflection.SetField(_service, "_recoveryActionEnabled", true);

            _mockProcess.Setup(p => p.HasExited).Returns(true);
            _mockProcess.Setup(p => p.ExitCode).Returns(1);

            // Wire up a TaskCompletionSource via Moq's Callback mechanism to signal completion 
            // without using exceptions as control flow.
            var recoveryLoggedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            scopedLogger
                .Setup(l => l.Warn(It.Is<string>(s => s.Contains("Initiating recovery")), It.IsAny<Exception>()))
                .Callback(() => recoveryLoggedSignal.TrySetResult(true));

            // Act
            // This invokes the asynchronous background loop state machine
            TestReflection.InvokeNonPublic(_service, "OnProcessExited", _mockProcess.Object, EventArgs.Empty);

            // Assert
            // Await the completion signal deterministically up to a generous 2-second timeout to accommodate slow CI runners.
            await Task.WhenAny(recoveryLoggedSignal.Task, Task.Delay(2000, CancellationToken.None));

            Assert.True(recoveryLoggedSignal.Task.IsCompleted,
                "The internal recovery sequence was not scheduled or executed within the time limit.");

            // Verify exactly once at the tail end. If a regression occurs, Moq will now surface its precise mismatch diagnostics.
            scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Initiating recovery")), null), Times.Once());
        }

        #endregion

        #region Teardown & Custom Command Tests

        [Fact]
        public void OnStop_ExecutesTeardown()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                StdoutPath = "C:\\Logs\\stdout.log",
                StderrPath = "C:\\Logs\\stderr.log",
                EnableHealthMonitoring = true,
                HeartbeatInterval = 10,
                MaxFailedChecks = 3
            };

            // Setup standard dependencies
            SetupStandardServiceStart(options);

            // Stub the process wrapper so its Stop method returns success
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Start the service to initialize the writers, timer, and child process
            _service.StartForTest();

            bool stopped = false;
            _service.OnStoppedForTest += () => stopped = true;

            // Act
            _service.Stop();

            // Assert
            // 1. Verify the stopping event fired successfully
            Assert.True(stopped);

            // 2. Verify process stop was requested with its stop timeout limit
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);

            // 3. Verify that the health-monitoring timer was stopped
            _mockTimer.Verify(t => t.Stop(), Times.Once);

            // 4. Verify stdout and stderr writers were flushed and disposed cleanly
            _mockStdoutWriter.Verify(w => w.Dispose(), Times.Once);
            _mockStderrWriter.Verify(w => w.Dispose(), Times.Once);
        }

        [Fact]
        public void SafeKillProcess_GracefulStop_LogsCorrectly()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _service.StartForTest();

            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true); // Graceful stop succeeds
            _mockProcess.Setup(p => p.HasExited).Returns(false);

            // Act
            TestReflection.InvokeNonPublic(_service, "SafeKillProcess", _mockProcess.Object, 1000);

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

            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(false); // Graceful stop fails
            _mockProcess.Setup(p => p.HasExited).Returns(false);

            // Act
            TestReflection.InvokeNonPublic(_service, "SafeKillProcess", _mockProcess.Object, 1000);

            // Assert
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("was forcefully terminated")), It.IsAny<Exception>()), Times.Once);
        }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Explicit teardown hook called by the xUnit test runner framework execution loop after each test finishes.
        /// Flushes background handles to prevent WaitHandle/Semaphore leaks between individual testing suites.
        /// </summary>
        public void Dispose()
        {
            _service?.Dispose();
        }

        #endregion
    }
}