using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.UnitTests.Helpers;
using Servy.Service.Validation;
using Servy.Testing;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using IServiceHelper = Servy.Service.Helpers.IServiceHelper;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests
{
    public class ServiceTests : IDisposable
    {
        private readonly ServiceTestContext _ctx;
        private readonly Service _service;

        private readonly Mock<IStreamWriter> _mockStdoutWriter;
        private readonly Mock<IStreamWriter> _mockStderrWriter;
        private readonly Mock<ITimer> _mockTimer;
        private readonly Mock<IProcessWrapper> _mockProcess;

        public ServiceTests()
        {
            _ctx = new ServiceTestContext();

            _mockStdoutWriter = new Mock<IStreamWriter>();
            _mockStderrWriter = new Mock<IStreamWriter>();
            _mockTimer = new Mock<ITimer>();
            _mockProcess = new Mock<IProcessWrapper>();

            // Crucial setup: Stub the StartInfo property so it never returns null during tests
            _mockProcess.Setup(p => p.StartInfo).Returns(new ProcessStartInfo());

            _ctx.StreamWriterFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns((string path, bool enableSizeRotation, long size, bool enableDateRotation, DateRotationType dateRotationType, int maxRotations, bool useLocalTimeForRotation) =>
                {
                    if (path.Contains("stdout"))
                        return _mockStdoutWriter.Object;
                    else if (path.Contains("stderr"))
                        return _mockStderrWriter.Object;
                    return null;
                });

            _ctx.TimerFactory.Setup(f => f.Create(It.IsAny<double>()))
                .Returns(_mockTimer.Object);

            _ctx.ProcessFactory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(_mockProcess.Object);

            _service = _ctx.BuildService();
        }

        [Theory]
        [InlineData(false, true, true, true, true, true, true, "serviceHelper")]
        [InlineData(true, false, true, true, true, true, true, "logger")]
        [InlineData(true, true, false, true, true, true, true, "streamWriterFactory")]
        [InlineData(true, true, true, false, true, true, true, "timerFactory")]
        [InlineData(true, true, true, true, false, true, true, "processFactory")]
        [InlineData(true, true, true, true, true, false, true, "pathValidator")]
        [InlineData(true, true, true, true, true, true, false, "serviceRepository")]
        public void Constructor_WhenDependencyIsNull_ThrowsArgumentNullException(
            bool useServiceHelper,
            bool useLogger,
            bool useStreamWriterFactory,
            bool useTimerFactory,
            bool useProcessFactory,
            bool usePathValidator,
            bool useServiceRepository,
            string expectedParamName)
        {
            // Arrange
            var serviceHelper = useServiceHelper ? new Mock<IServiceHelper>().Object : null!;
            var logger = useLogger ? new Mock<IServyLogger>().Object : null!;
            var streamWriterFactory = useStreamWriterFactory ? new Mock<IStreamWriterFactory>().Object : null!;
            var timerFactory = useTimerFactory ? new Mock<ITimerFactory>().Object : null!;
            var processFactory = useProcessFactory ? new Mock<IProcessFactory>().Object : null!;
            var pathValidator = usePathValidator ? new Mock<IPathValidator>().Object : null!;
            var serviceRepository = useServiceRepository ? new Mock<IServiceRepository>().Object : null!;

            // Act
            var exception = Record.Exception(() => new Service(
                serviceHelper,
                logger,
                streamWriterFactory,
                timerFactory,
                processFactory,
                pathValidator,
                serviceRepository
            ));

            // Assert
            var argumentNullException = Assert.IsType<ArgumentNullException>(exception);
            Assert.Equal(expectedParamName, argumentNullException.ParamName);
        }

        #region Production Constructor Guard Clauses

        [Theory]
        [InlineData(false, true, true, true, true, true, "serviceHelper")]
        [InlineData(true, false, true, true, true, true, "logger")]
        [InlineData(true, true, false, true, true, true, "streamWriterFactory")]
        [InlineData(true, true, true, false, true, true, "timerFactory")]
        [InlineData(true, true, true, true, false, true, "processFactory")]
        [InlineData(true, true, true, true, true, false, "pathValidator")]
        public void ProductionConstructor_WhenDependencyIsNull_ThrowsArgumentNullException(
            bool useServiceHelper,
            bool useLogger,
            bool useStreamWriterFactory,
            bool useTimerFactory,
            bool useProcessFactory,
            bool usePathValidator,
            string expectedParamName)
        {
            // Arrange
            var serviceHelper = useServiceHelper ? new Mock<IServiceHelper>().Object : null!;
            var logger = useLogger ? new Mock<IServyLogger>().Object : null!;
            var streamWriterFactory = useStreamWriterFactory ? new Mock<IStreamWriterFactory>().Object : null!;
            var timerFactory = useTimerFactory ? new Mock<ITimerFactory>().Object : null!;
            var processFactory = useProcessFactory ? new Mock<IProcessFactory>().Object : null!;
            var pathValidator = usePathValidator ? new Mock<IPathValidator>().Object : null!;

            // Act
            var exception = Record.Exception(() => new Service(
                serviceHelper,
                logger,
                streamWriterFactory,
                timerFactory,
                processFactory,
                pathValidator
            ));

            // Assert
            var argumentNullException = Assert.IsType<ArgumentNullException>(exception);
            Assert.Equal(expectedParamName, argumentNullException.ParamName);
        }

        #endregion

        [Fact]
        public void OnStart_ValidOptions_InitializesCorrectly()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" }; // Arguments returned by Helper
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                StartupDirectory = "C:\\Windows",
                EnableHealthMonitoring = true,
                HeartbeatIntervalInSeconds = 10,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartProcess,
                StdoutPath = "C:\\Logs\\stdout.log",
                StderrPath = "C:\\Logs\\stderr.log"
            };

            var mockScopedLogger = new Mock<IServyLogger>();

            // 1. ServiceHelper flow
            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _ctx.Helper.Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, It.IsAny<string[]>()))
                .Returns(options);
            _mockProcess.Setup(p => p.Start()).Returns(true);

            // 2. Logger Promotion setup
            // This is critical: the service will now use mockScopedLogger.Object for everything else
            _ctx.Logger.Setup(l => l.CreateScoped(options.ServiceName)).Returns(mockScopedLogger.Object);

            // 3. Validation setup (Must use the scoped logger)
            _ctx.Helper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object))
                .Returns(true);

            // 4. Path Validator setup (Used inside HandleLogWriters)
            _ctx.PathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            // Act
            _service.StartForTest();

            // Assert
            // Verify Health Monitoring started
            // 10 seconds * 1000ms = 10000
            _ctx.TimerFactory.Verify(f => f.Create(10000), Times.Once);
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
            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _ctx.Helper.Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, fullArgs))
                .Returns(options);

            // 2. Setup Logger Promotion: Root returns Scoped
            _ctx.Logger.Setup(l => l.CreateScoped(options.ServiceName))
                .Returns(mockScopedLogger.Object);

            // 3. Setup Validation: Must return true for the method to proceed to HandleLogWriters
            _ctx.Helper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object))
                .Returns(true);

            // 4. Force the path validation to fail
            _ctx.PathValidator.Setup(v => v.IsValidPath(options.StdoutPath)).Returns(false);

            // Act
            _service.StartForTest();

            // Assert
            // Verify the error was logged to the SCOPED logger, not the root _ctx.Logger
            mockScopedLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Invalid log file path")),
                It.IsAny<Exception>()
                ), Times.AtLeastOnce);

            // The root logger must NOT be disposed because the scoped logger
            // delegates its underlying EventLog/File operations to it.
            _ctx.Logger.Verify(l => l.Dispose(), Times.Never);
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
            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);

            // 2. Mock ParseOptions to return null (simulating invalid or missing configuration)
            _ctx.Helper.Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, fullArgs))
                .Returns((StartOptions?)null);

            // Act
            _service.StartForTest();

            // Assert
            // Verify the service stopped because options were null
            Assert.True(stopped);

            // Verify that ValidateAndLog was NEVER called because we exited early
            _ctx.Helper.Verify(h => h.ValidateAndLog(It.IsAny<StartOptions>(), It.IsAny<IServyLogger>()), Times.Never);
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
            _ctx.Helper.Setup(h => h.GetArgs()).Throws(testException);

            // Act
            _service.StartForTest();

            // Assert
            // 1. Verify the service triggered a stop
            Assert.True(stopped);

            // 2. Verify the error was logged to the root logger
            // (Promotion hasn't happened yet, so _ctx.Logger is still the active logger)
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Exception in OnStart")),
                testException
            ), Times.Once);

            // 3. Verify that the logger was never promoted due to the early failure
            _ctx.Logger.Verify(l => l.CreateScoped(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void OnStart_ValidationFails_SetsServiceSpecificExitCodeAndStopsWithoutStartingProcess()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe"
            };
            var mockScopedLogger = new Mock<IServyLogger>();
            bool stopped = false;

            _service.OnStoppedForTest += () => stopped = true;

            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _ctx.Helper.Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, fullArgs)).Returns(options);
            _ctx.Logger.Setup(l => l.CreateScoped(options.ServiceName)).Returns(mockScopedLogger.Object);

            // The path under test: validation reports the options as unusable.
            _ctx.Helper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object)).Returns(false);

            // Act
            _service.StartForTest();

            // Assert
            Assert.True(stopped);
            Assert.Equal(AppConfig.ServiceSpecificErrorCode, _service.ExitCode);

            // The early return is what keeps an unvalidated executable from being launched.
            _ctx.ProcessFactory.Verify(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()), Times.Never);
        }

        [Theory]
        [InlineData(30, 40_000)] // above the 20 s threshold: (30 + 10 s buffer) * 1000 ms
        [InlineData(20, null)]   // at the threshold: no request, the comparison is strictly greater-than
        public void OnStart_StartTimeout_RequestsAdditionalScmTimeOnlyAboveThreshold(int startTimeoutSeconds, int? expectedMilliseconds)
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                StartTimeoutInSeconds = startTimeoutSeconds
            };
            var scopedLogger = SetupStandardServiceStart(options);

            // Act
            _service.StartForTest();

            // Assert
            // The startup request is the only RequestAdditionalTime call that carries a logger;
            // the heartbeat call sites all pass null, so the scoped logger identifies this one.
            if (expectedMilliseconds.HasValue)
            {
                _ctx.Helper.Verify(h => h.RequestAdditionalTime(_service, expectedMilliseconds.Value, scopedLogger.Object), Times.Once);
            }
            else
            {
                _ctx.Helper.Verify(h => h.RequestAdditionalTime(_service, It.IsAny<int>(), scopedLogger.Object), Times.Never);
            }
        }

        [Fact]
        public void SetProcessPriority_ValidPriority_SetsPriorityAndLogsInfo()
        {
            // Arrange
            var service = _ctx.Build();
            service.SetChildProcess(_mockProcess.Object);
            _mockProcess.SetupProperty(p => p.PriorityClass);

            // Act
            service.SetProcessPriority(ProcessPriorityClass.High);

            // Assert
            _mockProcess.VerifySet(p => p.PriorityClass = ProcessPriorityClass.High, Times.Once);
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(msg => msg.Contains("Set process priority to High")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SetProcessPriority_ExceptionThrown_LogsWarning()
        {
            // Arrange
            var service = _ctx.Build();
            service.SetChildProcess(_mockProcess.Object);
            _mockProcess.SetupSet(p => p.PriorityClass = It.IsAny<ProcessPriorityClass>())
                       .Throws(new Exception("Priority error"));

            // Act
            service.SetProcessPriority(ProcessPriorityClass.High);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(msg => msg.Contains("Failed to set priority") && msg.Contains("Priority error")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SetProcessCpuAffinity_ValidMask_SetsAffinityAndLogsInfo()
        {
            // Arrange
            var service = _ctx.Build();
            service.SetChildProcess(_mockProcess.Object);
            _mockProcess.SetupProperty(p => p.ProcessorAffinity);

            // Act
            // Core 0 alone, so the mask is valid on a single-core runner too
            service.SetProcessCpuAffinity("0");

            // Assert
            _mockProcess.VerifySet(p => p.ProcessorAffinity = new IntPtr(0x1), Times.Once);
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(msg => msg.Contains("Set process CPU affinity to 0x1 (0).")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SetProcessCpuAffinity_ExceptionThrown_LogsWarning()
        {
            // Arrange
            var service = _ctx.Build();
            service.SetChildProcess(_mockProcess.Object);
            _mockProcess.SetupSet(p => p.ProcessorAffinity = It.IsAny<IntPtr>())
                       .Throws(new Exception("Affinity error"));

            // Act
            service.SetProcessCpuAffinity("0");

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(msg => msg.Contains("Failed to set CPU affinity ('0')") && msg.Contains("Affinity error")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SetProcessCpuAffinity_BeforeChildProcessStarted_WarnsAndSetsNothing()
        {
            // Arrange
            // No SetChildProcess: the affinity call arrives before the child has been started
            var service = _ctx.Build();

            // Act
            service.SetProcessCpuAffinity("0");

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(msg => msg.Contains("SetProcessCpuAffinity called before child process was started")), It.IsAny<Exception>()), Times.Once);
            _mockProcess.VerifySet(p => p.ProcessorAffinity = It.IsAny<IntPtr>(), Times.Never);
        }

        [Fact]
        public void SetProcessPriority_BeforeChildProcessStarted_WarnsAndSetsNothing()
        {
            // Arrange
            // No SetChildProcess: the priority call arrives before the child has been started
            var service = _ctx.Build();

            // Act
            service.SetProcessPriority(ProcessPriorityClass.High);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(msg => msg.Contains("SetProcessPriority called before child process was started")), It.IsAny<Exception>()), Times.Once);
            _mockProcess.VerifySet(p => p.PriorityClass = It.IsAny<ProcessPriorityClass>(), Times.Never);
        }

        // One row per rotation flag, each turning exactly one of the three on, so every
        // transposition of the two bool arguments differs in at least one row. The previous
        // single-fixture form left EnableSizeRotation and EnableDateRotation at their shared
        // false default, which made swapping them at the call site undetectable.
        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void HandleLogWriters_ValidPaths_CreatesStreamWriters(bool enableSizeRotation, bool enableDateRotation, bool useLocalTime)
        {
            // Arrange
            var service = _ctx.Build();
            var options = new StartOptions
            {
                StdoutPath = "valid_stdout.log",
                StderrPath = "valid_stderr.log",
                RotationSizeInBytes = 12345,
                EnableSizeRotation = enableSizeRotation,
                EnableDateRotation = enableDateRotation,
                UseLocalTimeForRotation = useLocalTime,
            };

            var mockStdOutWriter = new Mock<IStreamWriter>();
            var mockStdErrWriter = new Mock<IStreamWriter>();

            _ctx.StreamWriterFactory.Setup(f => f.Create("valid_stdout.log", enableSizeRotation, 12345L, enableDateRotation, AppConfig.DefaultDateRotationType, AppConfig.DefaultMaxRotations, useLocalTime))
                .Returns(mockStdOutWriter.Object);

            _ctx.StreamWriterFactory.Setup(f => f.Create("valid_stderr.log", enableSizeRotation, 12345L, enableDateRotation, AppConfig.DefaultDateRotationType, AppConfig.DefaultMaxRotations, useLocalTime))
                .Returns(mockStdErrWriter.Object);

            _ctx.PathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert: the expected values are the literals the theory supplies rather than reads
            // back off the same options object, so a hop that reads the wrong property is caught too.
            _ctx.StreamWriterFactory.Verify(f => f.Create("valid_stdout.log", enableSizeRotation, 12345L, enableDateRotation, AppConfig.DefaultDateRotationType, AppConfig.DefaultMaxRotations, useLocalTime), Times.Once);
            _ctx.StreamWriterFactory.Verify(f => f.Create("valid_stderr.log", enableSizeRotation, 12345L, enableDateRotation, AppConfig.DefaultDateRotationType, AppConfig.DefaultMaxRotations, useLocalTime), Times.Once);

            // Check no errors logged
            _ctx.Logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void HandleLogWriters_InvalidPaths_LogsErrors()
        {
            // Arrange
            var service = _ctx.Build();
            var options = new StartOptions
            {
                StdoutPath = "invalid_stdout.log",
                StderrPath = "invalid_stderr.log",
                RotationSizeInBytes = 12345
            };

            _ctx.PathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(false);

            // Act
            service.InvokeHandleLogWriters(options);

            // Assert
            _ctx.StreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
            _ctx.Logger.Verify(l => l.Error(It.Is<string>(msg => msg.Contains("Invalid log file path")), null), Times.Exactly(2));
        }

        [Fact]
        public void HandleLogWriters_EmptyPaths_DoesNotCreateWritersOrLog()
        {
            // Arrange
            var service = _ctx.Build();
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
            _ctx.StreamWriterFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
            _ctx.Logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        #region Null, Empty, and Standard Sanitization Tests

        [Theory]
        [InlineData(null, "_")]
        [InlineData("", "_")]
        public void MakeFilenameSafe_NullOrEmptyInput_ReturnsSafeFallback(string? input, string expectedBase)
        {
            // Arrange & Act
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

        #region DOS Reserved Device Names & Multi-Extension Edge Cases

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

        #region Disambiguation & Namespace Collision Resolution

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

            // Assert: Verify that despite trimming, appending original hashes isolates filenames completely.
            // Asserting over the whole set covers all six pairs, including outBase/outSpaces and
            // outDot/outSpaces, and keeps the comparison count correct if a fifth variant is added.
            var all = new[] { outBase, outSpace, outDot, outSpaces };
            Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());

            // All must preserve base readability prefixing
            Assert.All(all, o => Assert.StartsWith("MyService_", o));
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
        /// DRY helper to initialize the base StartOptions and mocks required to get the
        /// Service through its initial ValidateAndLog and HandleLogWriters phases cleanly.
        /// </summary>
        private Mock<IServyLogger> SetupStandardServiceStart(StartOptions options)
        {
            var fullArgs = new[] { "servy.exe" };
            var mockScopedLogger = new Mock<IServyLogger>();

            _ctx.Helper.Setup(h => h.GetArgs()).Returns(fullArgs);
            _ctx.Helper.Setup(h => h.ParseOptions(It.IsAny<IServiceRepository>(), It.IsAny<string[]>())).Returns(options);
            _ctx.Logger.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockScopedLogger.Object);
            _ctx.Helper.Setup(h => h.ValidateAndLog(options, mockScopedLogger.Object)).Returns(true);
            _ctx.PathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);
            _mockProcess.Setup(p => p.Start()).Returns(true);

            return mockScopedLogger;
        }

        #endregion

        #region PersistProcessState Tests

        [Fact]
        public void PersistProcessState_WhenServiceNameIsBlank_ReturnsImmediately()
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            using (var service = _ctx.BuildService(repositoryMock.Object))
            {
                TestReflection.SetField(service, "_serviceName", "   ");

                // Act
                TestReflection.InvokeNonPublic(service, "PersistProcessState", new object?[] { 1234, true });

                // Assert
                repositoryMock.Verify(r => r.GetByName(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }
        }

        [Fact]
        public void PersistProcessState_WhenServiceDtoNotFound_DoesNotUpdateOrThrow()
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            using (var service = _ctx.BuildService(repositoryMock.Object))
            {
                TestReflection.SetField(service, "_serviceName", "ServyTest");
                repositoryMock.Setup(r => r.GetByName("ServyTest", true)).Returns((ServiceDto)null!);

                // Act
                var exception = Record.Exception(() =>
                    TestReflection.InvokeNonPublic(service, "PersistProcessState", new object?[] { 1234, true }));

                // Assert
                Assert.Null(exception);
                repositoryMock.Verify(r => r.Update(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PersistProcessState_WithActivePid_UpdatesPidAndPathsCorrectly(bool setPreviousStopTimeout)
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            using (var service = _ctx.BuildService(repositoryMock.Object))
            {
                TestReflection.SetField(service, "_serviceName", "ServyTest");

                // Mock out a test configuration options block
                var options = new StartOptions
                {
                    StopTimeoutInSeconds = 30,
                    StdoutPath = "C:\\stdout.log",
                    StderrPath = "C:\\stderr.log"
                };
                TestReflection.SetField(service, "_options", options);

                var testDto = new ServiceDto { Name = "ServyTest" };
                repositoryMock.Setup(r => r.GetByName("ServyTest", true)).Returns(testDto);

                // Act
                TestReflection.InvokeNonPublic(service, "PersistProcessState", new object?[] { 9999, setPreviousStopTimeout });

                // Assert
                Assert.Equal(9999, testDto.Pid);
                Assert.Equal("C:\\stdout.log", testDto.ActiveStdoutPath);
                Assert.Equal("C:\\stderr.log", testDto.ActiveStderrPath);

                if (setPreviousStopTimeout)
                {
                    Assert.Equal(30, testDto.PreviousStopTimeout);
                }
                else
                {
                    Assert.Null(testDto.PreviousStopTimeout);
                }

                repositoryMock.Verify(r => r.Update(testDto, false, true), Times.Once);
            }
        }

        [Fact]
        public void PersistProcessState_WhenPidIsNull_ClearsActivePaths()
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            using (var service = _ctx.BuildService(repositoryMock.Object))
            {
                TestReflection.SetField(service, "_serviceName", "ServyTest");

                var testDto = new ServiceDto
                {
                    Name = "ServyTest",
                    Pid = 5555,
                    ActiveStdoutPath = "old_out.log",
                    ActiveStderrPath = "old_err.log"
                };
                repositoryMock.Setup(r => r.GetByName("ServyTest", true)).Returns(testDto);

                // Act
                TestReflection.InvokeNonPublic(service, "PersistProcessState", new object?[] { null, false });

                // Assert
                Assert.Null(testDto.Pid);
                Assert.Null(testDto.ActiveStdoutPath);
                Assert.Null(testDto.ActiveStderrPath);

                repositoryMock.Verify(r => r.Update(testDto, false, true), Times.Once);
            }
        }

        [Fact]
        public void PersistProcessState_OnRepositoryException_IsCaughtAndLoggedSafely()
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            var loggerMock = new Mock<IServyLogger>();
            using (var service = _ctx.BuildService(repositoryMock.Object, loggerMock.Object))
            {
                string serviceName = "ServyTest";
                TestReflection.SetField(service, "_serviceName", serviceName);

                var repositoryException = new InvalidOperationException("Database deadlock or lock failure");
                repositoryMock.Setup(r => r.GetByName(serviceName, true)).Throws(repositoryException);

                // Act
                var testRunException = Record.Exception(() =>
                    TestReflection.InvokeNonPublic(service, "PersistProcessState", new object?[] { 1234, true }));

                // Assert
                Assert.Null(testRunException); // Confirms the exception branch is swallowed inside the try-catch block
                loggerMock.Verify(l => l.Error(
                    It.Is<string>(msg => msg.Contains($"Failed to persist PID 1234 for service '{serviceName}'.")),
                    repositoryException),
                    Times.Once);
            }
        }

        #endregion

        #region EmitHeartbeatPing Tests

        [Fact]
        public async Task EmitHeartbeatPing_WithValidUrl_ExecutesFireAndForgetWithoutBlocking()
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            using (var serviceInstance = _ctx.BuildService(repositoryMock.Object))
            {
                // Bind HttpListener with retry logic on ephemeral ports to prevent TOCTOU collisions in CI
                var (listener, baseAddress) = CreateAndStartHttpListener();

                var requestReceivedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var context = await listener.GetContextAsync();
                        requestReceivedTcs.TrySetResult(context.Request.Url?.AbsolutePath ?? string.Empty);
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.Close();
                    }
                    catch
                    {
                        // Listener stopped or disposed during test cleanup
                    }
                }, CancellationToken.None);

                try
                {
                    var mockOptions = new StartOptions
                    {
                        EnableHealthMonitoring = true,   // Required to pass the first guard check
                        EnableHeartbeatUrlFlags = true   // Required to allow suffix appending
                    };
                    TestReflection.SetField(serviceInstance, "_options", mockOptions);

                    object?[] parameters = new object?[] { $"{baseAddress}test-uuid", "/start", 2 };

                    // Act
                    var watch = Stopwatch.StartNew();
                    TestReflection.InvokeNonPublic(serviceInstance, "EmitHeartbeatPing", parameters);
                    watch.Stop();

                    // Assert 1: The method returned immediately without blocking the primary thread
                    Assert.True(watch.ElapsedMilliseconds < 1000, $"Method blocked primary execution thread for {watch.ElapsedMilliseconds}ms");

                    // Assert 2: The background task actually fired the HTTP request and correctly appended the suffix path
                    var timeoutTask = Task.Delay(TestTimeouts.CiGenerous, CancellationToken.None);
                    var completedTask = await Task.WhenAny(requestReceivedTcs.Task, timeoutTask);

                    Assert.True(completedTask == requestReceivedTcs.Task, "Heartbeat ping request timed out on background thread");

                    string receivedPath = await requestReceivedTcs.Task;
                    Assert.Equal("/test-uuid/start", receivedPath);
                }
                finally
                {
                    try { listener.Stop(); } catch { /* Ignore cleanup errors */ }
                    try { listener.Close(); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task EmitHeartbeatPing_WithNullOrEmptyBaseUrl_ReturnsEarlyWithoutScheduling(string? invalidUrl)
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            var loggerMock = new Mock<IServyLogger>();
            using (var serviceInstance = _ctx.BuildService(repositoryMock.Object))
            {
                var mockOptions = new StartOptions
                {
                    EnableHealthMonitoring = true,
                    EnableHeartbeatUrlFlags = true
                };
                TestReflection.SetField(serviceInstance, "_options", mockOptions);
                TestReflection.SetField(serviceInstance, "_logger", loggerMock.Object);

                // Act
                TestReflection.InvokeNonPublic(serviceInstance, "EmitHeartbeatPing", new object?[] { invalidUrl, "/start", 2 });
                await Task.Delay(TestTimeouts.NegativeObservationWindow, TestContext.Current.CancellationToken);

                // Assert
                loggerMock.Verify(l => l.Debug(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }

        [Fact]
        public async Task EmitHeartbeatPing_WithHealthMonitoringDisabled_ReturnsEarlyWithoutScheduling()
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            var loggerMock = new Mock<IServyLogger>();
            using (var serviceInstance = _ctx.BuildService(repositoryMock.Object))
            {
                var mockOptions = new StartOptions
                {
                    EnableHealthMonitoring = false,
                    EnableHeartbeatUrlFlags = true
                };
                TestReflection.SetField(serviceInstance, "_options", mockOptions);
                TestReflection.SetField(serviceInstance, "_logger", loggerMock.Object);

                // Act
                TestReflection.InvokeNonPublic(serviceInstance, "EmitHeartbeatPing", new object?[] { "http://localhost:12345/ping", "/start", 2 });
                await Task.Delay(TestTimeouts.NegativeObservationWindow, TestContext.Current.CancellationToken);

                // Assert
                loggerMock.Verify(l => l.Debug(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }

        [Fact]
        public async Task EmitHeartbeatPing_WithSuffixAndUrlFlagsDisabled_ReturnsEarlyWithoutScheduling()
        {
            // Arrange
            var repositoryMock = new Mock<IServiceRepository>();
            var loggerMock = new Mock<IServyLogger>();
            using (var serviceInstance = _ctx.BuildService(repositoryMock.Object))
            {
                var mockOptions = new StartOptions
                {
                    EnableHealthMonitoring = true,
                    EnableHeartbeatUrlFlags = false
                };
                TestReflection.SetField(serviceInstance, "_options", mockOptions);
                TestReflection.SetField(serviceInstance, "_logger", loggerMock.Object);

                // Act
                TestReflection.InvokeNonPublic(serviceInstance, "EmitHeartbeatPing", new object?[] { "http://localhost:12345/ping", "/start", 2 });
                await Task.Delay(TestTimeouts.NegativeObservationWindow, TestContext.Current.CancellationToken);

                // Assert
                loggerMock.Verify(l => l.Debug(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }

        [Fact]
        public async Task EmitHeartbeatPing_NonSuccessStatusCode_LogsMaskedStatusAtDebug()
        {
            // Arrange
            var service = _ctx.Build();
            var (listener, baseAddress) = CreateAndStartHttpListener();

            var debugTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ctx.Logger.Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<Exception>()))
                .Callback<string, Exception?>((message, _) =>
                {
                    if (message.Contains("returned unexpected status code"))
                    {
                        debugTcs.TrySetResult(message);
                    }
                });

            _ = Task.Run(async () =>
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                }
                catch
                {
                    // Listener stopped or disposed during test cleanup
                }
            }, CancellationToken.None);

            try
            {
                TestReflection.SetField(service, "_options", new StartOptions
                {
                    EnableHealthMonitoring = true // Required to pass the first guard check
                });

                // The secret sits in the path, which is the part MaskUrl exists to hide
                object?[] parameters = new object?[] { $"{baseAddress}secret-uuid-value", string.Empty, 2 };

                // Act
                TestReflection.InvokeNonPublic(service, "EmitHeartbeatPing", parameters);

                // Assert
                var completed = await Task.WhenAny(debugTcs.Task, Task.Delay(TestTimeouts.CiGenerous, CancellationToken.None));
                Assert.True(completed == debugTcs.Task, "Heartbeat non-success debug line was never logged");

                var logged = await debugTcs.Task;
                Assert.Contains("returned unexpected status code: 500 (InternalServerError)", logged, StringComparison.Ordinal);
                Assert.Contains("[MASKED]", logged, StringComparison.Ordinal);
                Assert.DoesNotContain("secret-uuid-value", logged, StringComparison.Ordinal);
            }
            finally
            {
                listener.Close();
            }
        }

        private static (HttpListener listener, string baseAddress) CreateAndStartHttpListener()
        {
            var random = new Random();
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int port = random.Next(49152, 65535);
                string baseAddress = $"http://127.0.0.1:{port}/";
                var listener = new HttpListener();
                try
                {
                    listener.Prefixes.Add(baseAddress);
                    listener.Start();
                    return (listener, baseAddress);
                }
                catch (HttpListenerException)
                {
                    try { listener.Close(); } catch { }
                }
            }

            // Fallback if random attempts encounter collisions
            int fallbackPort = GetFreeTcpPort();
            string fallbackAddress = $"http://127.0.0.1:{fallbackPort}/";
            var fallbackListener = new HttpListener();
            fallbackListener.Prefixes.Add(fallbackAddress);
            fallbackListener.Start();
            return (fallbackListener, fallbackAddress);
        }

        private static int GetFreeTcpPort()
        {
            using (var l = new TcpListener(IPAddress.Loopback, 0))
            {
                l.Start();
                return ((IPEndPoint)l.LocalEndpoint).Port;
            }
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
            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
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

            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
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

            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Returns(mockPreLaunchProcess.Object);

            // Act
            _service.StartForTest();

            // Assert
            scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Ignoring pre-launch failure")), null), Times.Once);
            _mockProcess.Verify(p => p.Start(), Times.Once); // Main process starts anyway
        }

        [Fact]
        public void OnStart_PreLaunchSynchronous_RetriesAfterBackoff_ThenSucceeds()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "test.exe",
                PreLaunchExecutablePath = "prelaunch.exe",
                PreLaunchTimeoutInSeconds = 10,
                PreLaunchIgnoreFailure = false,
                PreLaunchRetryAttempts = 1 // maxAttempts = 2, so attempt 1 < maxAttempts reaches the back-off branch
            };
            var scopedLogger = SetupStandardServiceStart(options);

            // WaitForExit must be stubbed: ProcessLauncher polls it, and an unstubbed mock returns
            // false forever, which turns every attempt into a timeout instead of an exit-code check.
            var mockFailedPreLaunch = new Mock<IProcessWrapper>();
            mockFailedPreLaunch.Setup(p => p.Start()).Returns(true);
            mockFailedPreLaunch.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            mockFailedPreLaunch.Setup(p => p.ExitCode).Returns(1); // first attempt fails

            var mockSucceededPreLaunch = new Mock<IProcessWrapper>();
            mockSucceededPreLaunch.Setup(p => p.Start()).Returns(true);
            mockSucceededPreLaunch.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            mockSucceededPreLaunch.Setup(p => p.ExitCode).Returns(0); // second attempt succeeds

            _ctx.ProcessFactory.SetupSequence(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Returns(mockFailedPreLaunch.Object)
                .Returns(mockSucceededPreLaunch.Object);

            // Act
            _service.StartForTest();

            // Assert: the back-off branch ran, so a second attempt was started and it succeeded
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("attempt 2/2")), It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Pre-launch process completed successfully")), It.IsAny<Exception>()), Times.Once);
            _mockProcess.Verify(p => p.Start(), Times.Once); // Main process starts after the retried pre-launch succeeds
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnStart_PreLaunchFireAndForgetFailsToLaunch_LogsPerIgnoreFailureAndGatesTheMainProcess(bool ignoreFailure)
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "test.exe",
                PreLaunchExecutablePath = "prelaunch.exe",
                PreLaunchTimeoutInSeconds = 0, // 0 means Fire and Forget
                PreLaunchIgnoreFailure = ignoreFailure
            };
            var scopedLogger = SetupStandardServiceStart(options);

            // The launch itself fails, which is the arm the exit-code tests above cannot reach.
            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Throws(new InvalidOperationException("the hook executable could not be launched"));

            // Act
            _service.StartForTest();

            // Assert: the severity follows PreLaunchIgnoreFailure, and so does whether the service starts at all
            scopedLogger.Verify(l => l.Warn(FireAndForgetPreLaunchFailure, It.IsAny<Exception>()),
                ignoreFailure ? Times.Once() : Times.Never());
            scopedLogger.Verify(l => l.Error(FireAndForgetPreLaunchFailure, It.IsAny<Exception>()),
                ignoreFailure ? Times.Never() : Times.Once());
            _mockProcess.Verify(p => p.Start(), ignoreFailure ? Times.Once() : Times.Never());
        }

        [Fact]
        public void OnStart_PreLaunchSynchronousTornDownDuringBackOff_AbortsWithoutASecondAttempt()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "test.exe",
                PreLaunchExecutablePath = "prelaunch.exe",
                PreLaunchTimeoutInSeconds = 10,
                PreLaunchIgnoreFailure = false,
                PreLaunchRetryAttempts = 1 // maxAttempts = 2, so a failed attempt 1 reaches the back-off wait
            };
            var scopedLogger = SetupStandardServiceStart(options);

            var mockFailedPreLaunch = new Mock<IProcessWrapper>();
            mockFailedPreLaunch.Setup(p => p.Start()).Returns(true);
            mockFailedPreLaunch.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            mockFailedPreLaunch.Setup(p => p.ExitCode).Returns(1); // the attempt fails, so the back-off runs

            // Teardown is signalled while attempt 1 is in flight: the loop's top-of-attempt check has
            // already passed, so the abort can only be observed by the check inside the back-off wait.
            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Callback(() => TestReflection.SetField(_service, "_isTearingDown", true))
                .Returns(mockFailedPreLaunch.Object);

            // Act
            _service.StartForTest();

            // Assert
            scopedLogger.Verify(l => l.Error("Pre-launch process aborted during back-off wait due to service teardown.", null), Times.Once);

            // ... and the abort returned instead of sleeping out the back-off and retrying
            _ctx.ProcessFactory.Verify(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()), Times.Once);
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("attempt 2/2")), It.IsAny<Exception>()), Times.Never);
            scopedLogger.Verify(l => l.Error("Pre-launch process failed after all retry attempts.", null), Times.Never);
            _mockProcess.Verify(p => p.Start(), Times.Never);
        }

        /// <summary>
        /// The text the top-of-attempt teardown check logs before an attempt is started.
        /// </summary>
        private const string TopOfAttemptTeardownAbort = "Pre-launch process aborted due to service teardown.";

        /// <summary>
        /// The text both of the back-off wait's teardown checks log, which is why a test that means
        /// to pin one of them has to tell them apart by something other than this string.
        /// </summary>
        private const string BackOffTeardownAbort = "Pre-launch process aborted during back-off wait due to service teardown.";

        /// <summary>
        /// How long after the back-off wait starts the token is cancelled in
        /// <see cref="OnStart_PreLaunchCancelledInsideTheBackOffSliceWait_AbortsFromTheWaitHandle"/>.
        /// The only slice is <see cref="AppConfig.PreLaunchRetryInitialDelayMs"/> long, so this leaves
        /// a wide margin on both sides: late enough that the loop's top-of-attempt check has already
        /// passed, early enough that the handle is signalled while the wait is still running.
        /// </summary>
        private const int BackOffCancelDelayMs = 200;

        /// <summary>
        /// The smallest wait the back-off abort may report and still prove it came from the wait
        /// handle: the top-of-attempt check returns without waiting at all, and both log the same text.
        /// </summary>
        private const int MinObservedBackOffWaitMs = 100;

        /// <summary>
        /// The smallest elapsed time a start whose back-off used the <see cref="Thread.Sleep(int)"/>
        /// fallback may report. A back-off that never slept its slice returns in a few milliseconds.
        /// </summary>
        private const int MinObservedSleepFallbackMs = 800;

        /// <summary>
        /// Builds the options a synchronous pre-launch retry test needs: one retry, so that a failed
        /// attempt 1 reaches the back-off wait, and a timeout long enough that the attempt is decided
        /// by its exit code rather than by the wait.
        /// </summary>
        /// <returns>The start options to hand to <see cref="SetupStandardServiceStart"/>.</returns>
        private static StartOptions CreatePreLaunchRetryOptions() => new StartOptions
        {
            ServiceName = "TestService",
            ExecutablePath = "test.exe",
            PreLaunchExecutablePath = "prelaunch.exe",
            PreLaunchTimeoutInSeconds = 10,
            PreLaunchIgnoreFailure = false,
            PreLaunchRetryAttempts = 1 // maxAttempts = 2, so a failed attempt 1 reaches the back-off wait
        };

        /// <summary>
        /// Creates a pre-launch wrapper whose attempt starts and then exits with a non-zero code,
        /// which is what drives the synchronous pre-launch into its back-off wait.
        /// </summary>
        /// <returns>The configured mock.</returns>
        private static Mock<IProcessWrapper> CreateFailingPreLaunch()
        {
            var failing = new Mock<IProcessWrapper>();
            failing.Setup(p => p.Start()).Returns(true);

            // WaitForExit must be stubbed: ProcessLauncher polls it, and an unstubbed mock returns
            // false forever, which turns the attempt into a timeout instead of an exit-code check.
            failing.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            failing.Setup(p => p.ExitCode).Returns(1);
            return failing;
        }

        [Fact]
        public void OnStart_PreLaunchFireAndForgetOwnsANativeProcess_IsTrackedAsPreLaunchAndNotDisposed()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "test.exe",
                PreLaunchExecutablePath = "prelaunch.exe",
                PreLaunchTimeoutInSeconds = 0 // 0 means Fire and Forget
            };
            SetupStandardServiceStart(options);

            using (var nativeProcess = new Process())
            {
                var mockPreLaunchProcess = new Mock<IProcessWrapper>();
                mockPreLaunchProcess.Setup(p => p.Start()).Returns(true);
                mockPreLaunchProcess.Setup(p => p.UnderlyingProcess).Returns(nativeProcess);
                _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                    .Returns(mockPreLaunchProcess.Object);

                var tracked = TestReflection.GetField<List<Hook>>(_service, "_trackedHooks");

                // Act
                _service.StartForTest();

                // Assert: a fire-and-forget hook that owns a native handle is kept so teardown can
                // kill the orphan, which is also why it must not be released here
                lock (tracked)
                {
                    Assert.Contains(tracked, h => h.OperationName == "Pre-Launch" && ReferenceEquals(h.Process, nativeProcess));
                }
                mockPreLaunchProcess.Verify(p => p.Dispose(), Times.Never);
            }
        }

        [Fact]
        public void OnStart_PreLaunchTornDownAfterTheLastBackOffSlice_AbortsBeforeTheNextAttempt()
        {
            // Arrange
            var options = CreatePreLaunchRetryOptions();
            var scopedLogger = SetupStandardServiceStart(options);

            var failing = CreateFailingPreLaunch();
            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Returns(failing.Object);

            // Teardown is signalled by the SCM pulse that follows the only back-off slice, so both of
            // the back-off checks have already run and the wait is over; the only arm left to see it
            // is the top-of-attempt check of attempt 2. The flag is needed because the pre-launch
            // process wait pulses the SCM through the same helper method.
            var inBackOff = false;
            scopedLogger.Setup(l => l.Info(It.Is<string>(s => s.StartsWith("Waiting ")), It.IsAny<Exception>()))
                .Callback(() => inBackOff = true);
            _ctx.Helper.Setup(h => h.RequestAdditionalTime(_service, It.IsAny<int>(), null))
                .Callback(() =>
                {
                    if (inBackOff)
                    {
                        TestReflection.SetField(_service, "_isTearingDown", true);
                    }
                });

            // Act
            _service.StartForTest();

            // Assert: the abort came from the top of attempt 2, not from the back-off wait
            scopedLogger.Verify(l => l.Error(TopOfAttemptTeardownAbort, null), Times.Once);
            scopedLogger.Verify(l => l.Error(BackOffTeardownAbort, null), Times.Never);

            // ... and attempt 2 never got as far as starting a process
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("attempt 2/2")), It.IsAny<Exception>()), Times.Never);
            _ctx.ProcessFactory.Verify(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()), Times.Once);
            scopedLogger.Verify(l => l.Error("Pre-launch process failed after all retry attempts.", null), Times.Never);
            _mockProcess.Verify(p => p.Start(), Times.Never);
        }

        [Fact]
        public void OnStart_PreLaunchCancelledInsideTheBackOffSliceWait_AbortsFromTheWaitHandle()
        {
            // Arrange
            var options = CreatePreLaunchRetryOptions();
            var scopedLogger = SetupStandardServiceStart(options);

            var failing = CreateFailingPreLaunch();
            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Returns(failing.Object);

            var backOffWait = new Stopwatch();
            using (var backOffStarted = new ManualResetEventSlim(false))
            {
                // The token is cancelled from another thread a little after the wait starts, so the
                // top-of-attempt check has already passed and only the wait handle can observe it.
                // Both aborts log the same text, so the elapsed wait is what tells them apart.
                var canceller = new Thread(() =>
                {
                    if (backOffStarted.Wait(TestTimeouts.CiGenerous))
                    {
                        Thread.Sleep(BackOffCancelDelayMs);
                        TestReflection.GetField<CancellationTokenSource>(_service, "_cancellationSource").Cancel();
                    }
                });
                canceller.IsBackground = true;
                canceller.Start();

                scopedLogger.Setup(l => l.Info(It.Is<string>(s => s.StartsWith("Waiting ")), It.IsAny<Exception>()))
                    .Callback(() =>
                    {
                        backOffWait.Start();
                        backOffStarted.Set();
                    });
                scopedLogger.Setup(l => l.Error(BackOffTeardownAbort, null)).Callback(() => backOffWait.Stop());

                // Act
                _service.StartForTest();

                // Assert: the wait handle saw the cancellation, and it had been waiting when it did
                Assert.True(canceller.Join(TestTimeouts.CiGenerous), "the cancelling thread never finished");
                scopedLogger.Verify(l => l.Error(BackOffTeardownAbort, null), Times.Once);
                scopedLogger.Verify(l => l.Error(TopOfAttemptTeardownAbort, null), Times.Never);
                Assert.True(backOffWait.ElapsedMilliseconds >= MinObservedBackOffWaitMs,
                    $"the abort came after only {backOffWait.ElapsedMilliseconds}ms of waiting, which is the top-of-attempt check rather than the wait handle");

                // ... and the abort returned instead of sleeping the slice out and retrying
                scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("attempt 2/2")), It.IsAny<Exception>()), Times.Never);
                _mockProcess.Verify(p => p.Start(), Times.Never);
            }
        }

        [Fact]
        public void OnStart_PreLaunchBackOffWithoutACancellationSource_SleepsTheSliceAndRetries()
        {
            // Arrange
            var options = CreatePreLaunchRetryOptions();
            var scopedLogger = SetupStandardServiceStart(options);

            var failing = CreateFailingPreLaunch();

            // Clearing the source inside attempt 1 takes the wait-handle arm out of the back-off, so
            // the Thread.Sleep fallback is the only thing left that can consume the slice.
            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == "prelaunch.exe"), It.IsAny<IServyLogger>()))
                .Callback(() => TestReflection.SetField(_service, "_cancellationSource", null))
                .Returns(failing.Object);

            var elapsed = Stopwatch.StartNew();

            // Act
            _service.StartForTest();
            elapsed.Stop();

            // Assert: the fallback waited the slice out, so the retry ran and no abort was logged
            Assert.True(elapsed.ElapsedMilliseconds >= MinObservedSleepFallbackMs,
                $"the start returned after {elapsed.ElapsedMilliseconds}ms, so the back-off slice was never slept");
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("attempt 2/2")), It.IsAny<Exception>()), Times.Once);
            scopedLogger.Verify(l => l.Error(TopOfAttemptTeardownAbort, null), Times.Never);
            scopedLogger.Verify(l => l.Error(BackOffTeardownAbort, null), Times.Never);
            _ctx.Helper.Verify(h => h.RequestAdditionalTime(_service, It.IsAny<int>(), null), Times.AtLeastOnce);
            _mockProcess.Verify(p => p.Start(), Times.Never);
        }

        #endregion

        #region Post-Launch Hook Tests

        private const string PostLaunchExe = @"C:\hooks\post.exe";
        private const string FireAndForgetPreLaunchFailure = "Failed to launch fire-and-forget pre-launch process.";
        private const string PostLaunchCancelled = "Post-launch action cancelled because service is stopping.";

        /// <summary>
        /// Builds the options a post-launch test needs: a configured hook, and a start timeout short
        /// enough that the confirmation wait is never the reason a test is slow.
        /// </summary>
        /// <param name="postLaunchExePath">The post-launch executable to configure, or <see langword="null"/> for no hook.</param>
        /// <returns>The start options to hand to <see cref="SetupStandardServiceStart"/>.</returns>
        private static StartOptions CreatePostLaunchOptions(string? postLaunchExePath) => new StartOptions
        {
            ServiceName = "TestService",
            ExecutablePath = "test.exe",
            PostLaunchExecutablePath = postLaunchExePath,
            StartTimeoutInSeconds = 1
        };

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnStart_PostLaunchHook_RunsOnlyOnceTheChildIsConfirmedStillRunning(bool stillRunning)
        {
            // Arrange
            var options = CreatePostLaunchOptions(PostLaunchExe);
            SetupStandardServiceStart(options);

            using var postLaunchStarted = new ManualResetEventSlim(false);
            var mockPostLaunchProcess = new Mock<IProcessWrapper>();
            mockPostLaunchProcess.Setup(p => p.Start()).Returns(true);
            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == PostLaunchExe), It.IsAny<IServyLogger>()))
                .Callback(() => postLaunchStarted.Set())
                .Returns(mockPostLaunchProcess.Object);

            _mockProcess.Setup(p => p.WaitAndCheckStillRunningAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(stillRunning);

            // Act
            _service.StartForTest();

            // Assert: a confirmed child runs the hook; an unconfirmed one must not, even after the
            // observation window a fire-and-forget body needs to be scheduled at all
            Assert.Equal(stillRunning, postLaunchStarted.Wait(
                stillRunning ? TestTimeouts.CiGenerous : TestTimeouts.NegativeObservationWindow,
                TestContext.Current.CancellationToken));
            _ctx.ProcessFactory.Verify(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == PostLaunchExe), It.IsAny<IServyLogger>()),
                stillRunning ? Times.Once() : Times.Never());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnStart_PostLaunchWaitCancelled_LogsTheCancellationOnlyWhenAHookIsConfigured(bool hookConfigured)
        {
            // Arrange
            var options = CreatePostLaunchOptions(hookConfigured ? PostLaunchExe : null);
            var scopedLogger = SetupStandardServiceStart(options);

            using var cancellationLogged = new ManualResetEventSlim(false);
            scopedLogger.Setup(l => l.Info(PostLaunchCancelled, It.IsAny<Exception>()))
                .Callback(() => cancellationLogged.Set());

            _mockProcess.Setup(p => p.WaitAndCheckStillRunningAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            _service.StartForTest();

            // Assert: the line is about a hook that will now not run, so with no hook configured it is noise
            Assert.Equal(hookConfigured, cancellationLogged.Wait(
                hookConfigured ? TestTimeouts.CiGenerous : TestTimeouts.NegativeObservationWindow,
                TestContext.Current.CancellationToken));
            scopedLogger.Verify(l => l.Info(PostLaunchCancelled, It.IsAny<Exception>()),
                hookConfigured ? Times.Once() : Times.Never());

            // ... and a cancelled wait is not an unexpected error
            scopedLogger.Verify(l => l.Error("Unexpected error in post-launch action.", It.IsAny<Exception>()), Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnStart_PostLaunchHook_IsTrackedOnlyWhenItOwnsANativeProcess(bool ownsNativeProcess)
        {
            // Arrange
            var options = CreatePostLaunchOptions(PostLaunchExe);
            SetupStandardServiceStart(options);

            using var hookDisposed = new ManualResetEventSlim(false);
            using var nativeProcess = new Process();
            var mockPostLaunchProcess = new Mock<IProcessWrapper>();
            mockPostLaunchProcess.Setup(p => p.Start()).Returns(true);
            mockPostLaunchProcess.Setup(p => p.Dispose()).Callback(() => hookDisposed.Set());
            if (ownsNativeProcess)
                mockPostLaunchProcess.Setup(p => p.UnderlyingProcess).Returns(nativeProcess);

            _ctx.ProcessFactory.Setup(f => f.Create(It.Is<ProcessStartInfo>(psi => psi.FileName == PostLaunchExe), It.IsAny<IServyLogger>()))
                .Returns(mockPostLaunchProcess.Object);

            _mockProcess.Setup(p => p.WaitAndCheckStillRunningAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var tracked = TestReflection.GetField<List<Hook>>(_service, "_trackedHooks");

            // Act
            _service.StartForTest();

            // Assert
            if (ownsNativeProcess)
            {
                // A hook with a native handle is kept so teardown can kill the orphan
                Assert.True(SpinWait.SpinUntil(() => { lock (tracked) return tracked.Count == 1; }, TestTimeouts.CiGenerous),
                    "the post-launch hook was never tracked");
                lock (tracked) Assert.Equal("Post-Launch", tracked[0].OperationName);
                mockPostLaunchProcess.Verify(p => p.Dispose(), Times.Never);
            }
            else
            {
                // Nothing to kill later, so the wrapper is released immediately instead of being tracked
                Assert.True(hookDisposed.Wait(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken),
                    "the untracked post-launch hook was never disposed");
                lock (tracked) Assert.Empty(tracked);
            }
        }

        [Fact]
        public void CleanupTrackedHooks_WhenOneHookThrowsOnDispose_LogsItAndStillClearsTheList()
        {
            // Arrange
            var tracked = TestReflection.GetField<List<Hook>>(_service, "_trackedHooks");
            tracked.Add(new ThrowingHook { OperationName = "Post-Launch" });
            tracked.Add(new Hook { OperationName = "Pre-Launch" });

            // Act
            TestReflection.InvokeNonPublic(_service, "CleanupTrackedHooks");

            // Assert
            _ctx.Logger.Verify(l => l.Warn($"Failed to dispose tracked hook: {ThrowingHook.FailureMessage}", It.IsAny<Exception>()), Times.Once);

            // ... and the throw did not abort the loop, so the surviving hook was still released
            Assert.Empty(tracked);
        }

        /// <summary>
        /// A <see cref="Hook"/> whose disposal always fails, so a test can drive the catch arm of
        /// <c>CleanupTrackedHooks</c> without depending on a real process handle being in a state
        /// that makes <see cref="Process.Dispose()"/> throw.
        /// </summary>
        private sealed class ThrowingHook : Hook
        {
            /// <summary>
            /// The message carried by the exception this hook throws, so an assertion can match the
            /// logged line character for character.
            /// </summary>
            public const string FailureMessage = "the process handle is already closed";

            /// <summary>
            /// Throws instead of releasing anything.
            /// </summary>
            /// <param name="disposing">Ignored; this override fails on every disposal path.</param>
            /// <exception cref="InvalidOperationException">Always thrown, carrying <see cref="FailureMessage"/>.</exception>
            protected override void Dispose(bool disposing) => throw new InvalidOperationException(FailureMessage);
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
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe", StdoutPath = "stdout.log" };
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
            var stoppedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _service.OnStoppedForTest += () =>
            {
                stopped = true;
                stoppedSignal.TrySetResult(true);
            };

            _service.StartForTest();

            _mockProcess.Setup(p => p.HasExited).Returns(true);
            _mockProcess.Setup(p => p.ExitCode).Returns(0); // Clean exit

            // Act
            // Invoke the non-public async void event handler via reflection
            TestReflection.InvokeNonPublic(_service, "OnProcessExited", _mockProcess.Object, EventArgs.Empty);

            // Assert
            // Await the stop event completion signal deterministically, bounded by the shared CI timeout budget.
            await Task.WhenAny(stoppedSignal.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));

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
                HeartbeatIntervalInSeconds = 10
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
            // Await the completion signal deterministically, bounded by the shared CI timeout budget.
            await Task.WhenAny(recoveryLoggedSignal.Task, Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));

            Assert.True(recoveryLoggedSignal.Task.IsCompleted,
                "The internal recovery sequence was not scheduled or executed within the time limit.");

            // Verify exactly once at the tail end. If a regression occurs, Moq will now surface its precise mismatch diagnostics.
            scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Initiating recovery")), null), Times.Once);
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
                HeartbeatIntervalInSeconds = 10,
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

            // 2. Verify process stop was requested
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);

            // 3. Verify that the health-monitoring timer was stopped
            _mockTimer.Verify(t => t.Stop(), Times.Once);

            // 4. Verify stdout and stderr writers were disposed
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

        [Fact]
        public void SafeKillProcess_ProcessAlreadyExited_SkipsStopButStillCleansDescendants()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _service.StartForTest();

            _mockProcess.Setup(p => p.HasExited).Returns(true);

            // Act
            TestReflection.InvokeNonPublic(_service, "SafeKillProcess", _mockProcess.Object, 1000);

            // Assert
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Never);
            _mockProcess.Verify(p => p.StopDescendants(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Once);
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("had already exited")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void OnCustomCommand_PreShutdownWhileRebooting_SignalsStoppedWithoutTeardown()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);

            using (var service = BuildStatusRecordingService())
            {
                service.StartForTest();

                // OnCustomCommand checks _isRebooting before it looks at the handle, so the handle does
                // not decide this branch. It is set non-zero anyway so that a later reordering of those
                // two checks cannot reach the null-handle fallback, whose Environment.Exit would end the test host.
                TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));
                TestReflection.SetField(service, "_isRebooting", true);

                // Act
                TestReflection.InvokeNonPublic(service, "OnCustomCommand", NativeMethods.SERVICE_CONTROL_PRESHUTDOWN);

                // Assert
                scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Pre-Shutdown bypassed")), It.IsAny<Exception>()), Times.Once);

                // SERVICE_STOPPED is signalled immediately, with no STOP_PENDING window in front of it
                Assert.Equal(new[] { NativeMethods.SERVICE_STOPPED }, service.StatusStates.ToArray());

                // and the teardown sequence never runs
                _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Never);
            }
        }

        [Fact]
        public void OnCustomCommand_PreShutdownWithServiceHandle_SignalsStopPendingThenStopped()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            using (var service = BuildStatusRecordingService())
            {
                service.StartForTest();
                TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));

                // Act
                TestReflection.InvokeNonPublic(service, "OnCustomCommand", NativeMethods.SERVICE_CONTROL_PRESHUTDOWN);

                // Assert
                // 1. SCM is moved to STOP_PENDING with the pre-shutdown wait hint before the teardown
                //    starts, and to STOPPED once it has completed
                Assert.Equal(
                    new[] { NativeMethods.SERVICE_STOP_PENDING, NativeMethods.SERVICE_STOPPED },
                    service.StatusStates.ToArray());
                Assert.Equal(AppConfig.PreShutdownWaitHintMs, service.StatusWaitHints[0]);
                Assert.Equal(0, service.StatusWaitHints[service.StatusWaitHints.Count - 1]);

                // 2. The teardown itself ran
                _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);

                // 3. A successful teardown reports success and leaves the exit code untouched
                scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Pre-Shutdown handling complete")), It.IsAny<Exception>()), Times.Once);
                Assert.Equal(0, service.ExitCode);
            }
        }

        [Fact]
        public void OnCustomCommand_UnrelatedCommand_IsIgnored()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);

            using (var service = BuildStatusRecordingService())
            {
                service.StartForTest();
                TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));

                // Act
                // Any control code other than SERVICE_CONTROL_PRESHUTDOWN falls through to the base implementation
                TestReflection.InvokeNonPublic(service, "OnCustomCommand", UnrelatedControlCode);

                // Assert
                Assert.Empty(service.StatusStates);
                _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Never);
                scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Pre-Shutdown")), It.IsAny<Exception>()), Times.Never);
            }
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(5, 5)]
        public void OnCustomCommand_PreShutdownWithoutServiceHandle_RunsTeardownThenTerminates(int ambientExitCode, int expectedTerminationCode)
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Environment.ExitCode is process-global, so it is restored however this test ends.
            var originalExitCode = Environment.ExitCode;

            try
            {
                using (var service = BuildStatusRecordingService())
                {
                    service.StartForTest();

                    // _serviceHandle is deliberately left at IntPtr.Zero: that is what routes
                    // OnCustomCommand into the synchronous fallback instead of the SCM-pulsed path.
                    Environment.ExitCode = ambientExitCode;

                    // Act
                    var thrown = Record.Exception(() =>
                        TestReflection.InvokeNonPublic(service, "OnCustomCommand", NativeMethods.SERVICE_CONTROL_PRESHUTDOWN));

                    // Assert
                    // 1. The fallback was entered, and no SCM status transition was attempted
                    scopedLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Service handle is null!")), It.IsAny<Exception>()), Times.Once);
                    Assert.Empty(service.StatusStates);

                    // 2. The teardown ran synchronously before the host process was terminated
                    _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);

                    // 3. The ambient exit code is preserved when non-zero, and replaced by 1 otherwise
                    Assert.Equal(new[] { expectedTerminationCode }, service.TerminatedWith.ToArray());

                    // 4. The real TerminateProcess never returns; the recording override models that by throwing
                    Assert.IsType<ProcessTerminatedException>(thrown);
                }
            }
            finally
            {
                Environment.ExitCode = originalExitCode;
            }
        }

        [Fact]
        public void OnCustomCommand_PreShutdownWithSlowTeardown_KeepsPulsingStopPendingUntilItCompletes()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);

            // A teardown that outlasts one pulse interval is what drives the wait loop round a second time.
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(() =>
            {
                Thread.Sleep(AppConfig.PreShutdownPulseIntervalMs + 200);
                return true;
            });

            using (var service = BuildStatusRecordingService())
            {
                service.StartForTest();
                TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));

                // Act
                TestReflection.InvokeNonPublic(service, "OnCustomCommand", NativeMethods.SERVICE_CONTROL_PRESHUTDOWN);

                // Assert
                // 1. The opening STOP_PENDING is followed by at least one pulse before STOPPED
                var pendingCount = service.StatusStates.Count(s => s == NativeMethods.SERVICE_STOP_PENDING);
                Assert.True(
                    pendingCount >= 2,
                    $"expected at least two SERVICE_STOP_PENDING updates, got [{string.Join(", ", service.StatusStates)}]");
                Assert.Equal(NativeMethods.SERVICE_STOPPED, service.StatusStates[service.StatusStates.Count - 1]);

                // 2. Every pulse carries the pre-shutdown wait hint, and the checkpoint the SCM watches advances
                //    so the wait does not read as a hung service
                Assert.All(
                    service.StatusWaitHints.Take(service.StatusWaitHints.Count - 1),
                    hint => Assert.Equal(AppConfig.PreShutdownWaitHintMs, hint));
                Assert.True(TestReflection.GetField<uint>(service, "_checkPoint") >= 1);

                // 3. The teardown still completed successfully
                scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Pre-Shutdown handling complete")), It.IsAny<Exception>()), Times.Once);
            }
        }

        [Fact]
        public void OnCustomCommand_PreShutdownWithFailingTeardown_ReportsItAndSetsTheServiceSpecificExitCode()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Cleanup disposes the child process in a finally that is outside its own try/catch, so a
            // throwing Dispose is what makes the teardown report failure rather than success.
            _mockProcess.Setup(p => p.Dispose()).Throws(new InvalidOperationException("dispose failed"));

            using (var service = BuildStatusRecordingService())
            {
                service.StartForTest();
                TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));

                // Act
                TestReflection.InvokeNonPublic(service, "OnCustomCommand", NativeMethods.SERVICE_CONTROL_PRESHUTDOWN);

                // Assert
                // 1. The teardown recorded the failure and released the tearing-down flag, which is what
                //    lets a later stop attempt the teardown again
                scopedLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Teardown error during PreShutdown")), It.IsAny<Exception>()), Times.Once);
                Assert.False(TestReflection.GetField<bool>(service, "_isTearingDown"));

                // 2. The SCM still reaches STOPPED, but with the service-specific exit code set so the
                //    failure is recorded rather than reported as a clean stop
                scopedLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("teardown reported failure")), It.IsAny<Exception>()), Times.Once);
                Assert.Equal(AppConfig.ServiceSpecificErrorCode, service.ExitCode);
                Assert.Equal(NativeMethods.SERVICE_STOPPED, service.StatusStates[service.StatusStates.Count - 1]);
            }
        }

        [Fact]
        public void OnStop_WhenStoppingTheHealthCheckTimerThrows_WarnsAndCompletesTheTeardown()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                EnableHealthMonitoring = true,
                HeartbeatIntervalInSeconds = 1,
                MaxFailedChecks = 1,
                RecoveryAction = RecoveryAction.RestartService
            };
            var scopedLogger = SetupStandardServiceStart(options);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);
            _mockTimer.Setup(t => t.Stop()).Throws(new InvalidOperationException("timer already gone"));
            _service.StartForTest();

            // Act
            TestReflection.InvokeNonPublic(_service, "OnStop");

            // Assert
            // The timer failure is warned about and swallowed, so the rest of the teardown still runs
            scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Error stopping health check timer")), It.IsAny<Exception>()), Times.Once);
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void OnShutdown_TearsDownTheChildProcess()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);
            _service.StartForTest();

            // Act
            TestReflection.InvokeNonPublic(_service, "OnShutdown");

            // Assert
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Executing teardown for reason: Shutdown")), It.IsAny<Exception>()), Times.Once);
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void OnShutdown_WhileRebooting_SkipsTheTeardown()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupStandardServiceStart(options);
            _mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);
            _service.StartForTest();
            TestReflection.SetField(_service, "_isRebooting", true);

            // Act
            TestReflection.InvokeNonPublic(_service, "OnShutdown");

            // Assert
            // The recovery logic owns the reboot, so the child is left to the OS instead of being stopped here
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Shutdown bypassed")), It.IsAny<Exception>()), Times.Once);
            _mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Never);
            Assert.False(TestReflection.GetField<bool>(_service, "_isTearingDown"));
        }

        [Fact]
        public void OnStart_CalledASecondTime_CancelsAndReplacesThePreviousCancellationSource()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            SetupRestartableServiceStart(options);
            _service.StartForTest();
            var firstSource = TestReflection.GetField<CancellationTokenSource>(_service, "_cancellationSource");

            // The token is captured before the second start, because the source is disposed once it is replaced
            var firstToken = firstSource.Token;

            // Act
            _service.StartForTest();

            // Assert
            // Everything the first start handed the token to is cancelled, and the field carries a fresh source
            Assert.True(firstToken.IsCancellationRequested);
            var secondSource = TestReflection.GetField<CancellationTokenSource>(_service, "_cancellationSource");
            Assert.NotNull(secondSource);
            Assert.NotSame(firstSource, secondSource);
            Assert.False(secondSource.IsCancellationRequested);
        }

        [Fact]
        public void OnStart_WhenThePreviousCancellationSourceIsAlreadyDisposed_StartsAnyway()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            SetupRestartableServiceStart(options);
            _service.StartForTest();
            var firstSource = TestReflection.GetField<CancellationTokenSource>(_service, "_cancellationSource");
            firstSource.Dispose();

            // Act
            _service.StartForTest();

            // Assert
            // Cancel on a disposed source raises ObjectDisposedException; swallowing it is what lets the
            // rest of the start sequence run, so the monitored process is started a second time
            _mockProcess.Verify(p => p.Start(), Times.Exactly(2));
            Assert.NotSame(firstSource, TestReflection.GetField<CancellationTokenSource>(_service, "_cancellationSource"));
        }

        [Fact]
        public void OnStart_WhenAPreviousCancellationCallbackThrows_WarnsAndStartsAnyway()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "Test", ExecutablePath = "test.exe" };
            var scopedLogger = SetupRestartableServiceStart(options);
            _service.StartForTest();
            var firstSource = TestReflection.GetField<CancellationTokenSource>(_service, "_cancellationSource");
            firstSource.Token.Register(() => throw new InvalidOperationException("callback failed"));

            // Act
            _service.StartForTest();

            // Assert
            // Cancel wraps a throwing callback in an AggregateException; it is warned about and swallowed,
            // so the rest of the start sequence still runs
            scopedLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Exception(s) raised during CTS cancellation")), It.IsAny<Exception>()), Times.Once);
            _mockProcess.Verify(p => p.Start(), Times.Exactly(2));
        }

        [Fact]
        public void OnStart_WhenTheBackgroundResetTaskFaults_LogsTheFailure()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "Test",
                ExecutablePath = "test.exe",
                EnableHealthMonitoring = true,
                HeartbeatIntervalInSeconds = 30,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartService
            };
            var scopedLogger = SetupRestartableServiceStart(options);
            using var logged = new ManualResetEventSlim(false);
            scopedLogger
                .Setup(l => l.Error(It.Is<string>(s => s.Contains("Background restart-attempts reset failed")), It.IsAny<Exception>()))
                .Callback(() => logged.Set());

            // ConditionalResetRestartAttemptsAsync awaits _fileSemaphore outside its own try, and nothing
            // else on the synchronous start path takes it, so disposing it faults the reset task and only
            // the reset task
            TestReflection.GetField<SemaphoreSlim>(_service, "_fileSemaphore").Dispose();

            // Act
            _service.StartForTest();

            // Assert
            // The continuation runs on the thread pool, so wait for the log line instead of reading it at once
            Assert.True(logged.Wait(TestTimeouts.CiGenerous), "the faulted reset task was never logged");
            scopedLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Background restart-attempts reset failed")),
                It.Is<Exception>(e => e is ObjectDisposedException)), Times.Once);
        }

        [Fact]
        public async Task FlushAndShutdownLogger_WhenTheLoggerDisposeHangs_ReturnsAfterTheFlushBudget()
        {
            // Arrange
            using var release = new ManualResetEventSlim(false);
            var hangingLogger = new Mock<IServyLogger>();
            hangingLogger.Setup(l => l.Dispose()).Callback(() => release.Wait(HangingDisposeRelease));
            TestReflection.SetField(_service, "_logger", hangingLogger.Object);

            try
            {
                // Act
                var flush = Task.Run(() => TestReflection.InvokeNonPublic(_service, "FlushAndShutdownLogger"));
                var winner = await Task.WhenAny(
                    flush,
                    Task.Delay(TestTimeouts.CiGenerous, TestContext.Current.CancellationToken));

                // Assert
                // The flush gives up after AppConfig.LoggerFlushTimeoutMs instead of waiting for the hung
                // Dispose, so a stuck logger can never hold a stop past the SCM's patience
                Assert.Same(flush, winner);
                Assert.Null(TestReflection.GetField<IServyLogger>(_service, "_logger"));
                hangingLogger.Verify(l => l.Dispose(), Times.Once);
            }
            finally
            {
                release.Set();
            }
        }

        /// <summary>
        /// Wires the fixture for a service that is started more than once.
        /// <see cref="SetupStandardServiceStart"/> alone only survives one start: OnStart promotes the
        /// logger with <c>_logger.CreateScoped(...)</c>, and the scoped mock it returns has no CreateScoped
        /// of its own, so a second start would promote null and fail validation long before it reaches the
        /// cancellation-source swap.
        /// </summary>
        /// <param name="options">The start options the service helper is set up to parse.</param>
        /// <returns>The scoped logger every start of this service writes through.</returns>
        private Mock<IServyLogger> SetupRestartableServiceStart(StartOptions options)
        {
            var scopedLogger = SetupStandardServiceStart(options);
            scopedLogger.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(scopedLogger.Object);
            _ctx.Helper.Setup(h => h.ValidateAndLog(options, It.IsAny<IServyLogger>())).Returns(true);
            return scopedLogger;
        }

        /// <summary>
        /// A user-defined SCM control code (128-255) that the service does not handle.
        /// </summary>
        private const int UnrelatedControlCode = 200;

        /// <summary>
        /// How long a deliberately hung <see cref="IServyLogger.Dispose"/> stays blocked. It outlasts
        /// <see cref="TestTimeouts.CiGenerous"/> so the hang cannot end by itself while a test is still
        /// observing it; the test releases it in its own finally.
        /// </summary>
        private static readonly TimeSpan HangingDisposeRelease = TestTimeouts.CiGenerous + TimeSpan.FromSeconds(30);

        /// <summary>
        /// Builds a service wired to this fixture's mocks whose SCM status updates are recorded
        /// instead of being pushed through the SetServiceStatus P/Invoke.
        /// </summary>
        private StatusRecordingService BuildStatusRecordingService() =>
            new StatusRecordingService(
                _ctx.Helper.Object,
                _ctx.Logger.Object,
                _ctx.StreamWriterFactory.Object,
                _ctx.TimerFactory.Object,
                _ctx.ProcessFactory.Object,
                _ctx.PathValidator.Object,
                _ctx.ServiceRepository.Object);

        /// <summary>
        /// Records the SCM status transitions the service requests. The real UpdateServiceStatus
        /// needs a live service handle, so the pre-shutdown orchestration is otherwise only
        /// observable from inside a hosted service.
        /// </summary>
        private sealed class StatusRecordingService : Service
        {
            public StatusRecordingService(
                IServiceHelper serviceHelper,
                IServyLogger logger,
                IStreamWriterFactory streamWriterFactory,
                ITimerFactory timerFactory,
                IProcessFactory processFactory,
                IPathValidator pathValidator,
                IServiceRepository serviceRepository)
                : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, serviceRepository)
            {
            }

            /// <summary>Gets the states passed to UpdateServiceStatus, in call order.</summary>
            public List<int> StatusStates { get; } = new List<int>();

            /// <summary>Gets the wait hints passed to UpdateServiceStatus, in call order.</summary>
            public List<int> StatusWaitHints { get; } = new List<int>();

            /// <summary>Gets the exit codes passed to TerminateProcess, in call order.</summary>
            public List<int> TerminatedWith { get; } = new List<int>();

            protected override void UpdateServiceStatus(int state, int waitHint)
            {
                StatusStates.Add(state);
                StatusWaitHints.Add(waitHint);
            }

            protected override void TerminateProcess(int exitCode)
            {
                TerminatedWith.Add(exitCode);

                // The production TerminateProcess forwards to Environment.Exit, which does not return.
                // Throwing keeps everything after the call unreachable here too, so a test cannot
                // accidentally assert on statements the real service would never reach.
                throw new ProcessTerminatedException(exitCode);
            }
        }

        /// <summary>
        /// Sentinel thrown by <see cref="StatusRecordingService.TerminateProcess(int)"/> in place of the
        /// process termination the production seam performs. It exists only to unwind the call the way
        /// Environment.Exit would, so it carries no behaviour of its own.
        /// </summary>
        private sealed class ProcessTerminatedException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ProcessTerminatedException"/> class.
            /// </summary>
            /// <param name="exitCode">The exit code the service asked the host process to terminate with.</param>
            public ProcessTerminatedException(int exitCode)
                : base($"Service requested process termination with exit code {exitCode}.")
            {
            }
        }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Explicit teardown hook called by the xUnit test runner framework execution loop after each test finishes.
        /// Disposes every service the fixture's <see cref="ServiceTestContext"/> built, so their cancellation sources
        /// and semaphores do not leak into the next test.
        /// </summary>
        public void Dispose()
        {
            _ctx.Dispose();
        }

        #endregion
    }
}
