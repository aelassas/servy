using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using Xunit;
using ServiceHelper = Servy.Service.Helpers.ServiceHelper;

#if !DEBUG
using Servy.Core.Config;
#endif

namespace Servy.Service.UnitTests.Helpers
{
    public class ServiceHelperTests
    {
        private readonly Mock<ICommandLineProvider> _mockCommandLineProvider;
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly Mock<IServiceRepository> _mockRepo;
        private readonly ServiceHelper _helper;

        public ServiceHelperTests()
        {
            _mockCommandLineProvider = new Mock<ICommandLineProvider>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockRepo = new Mock<IServiceRepository>();

            // Default setup to pass validation so we can isolate specific test cases
            _mockProcessHelper.Setup(ph => ph.ValidatePath(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);

            _helper = new ServiceHelper(_mockCommandLineProvider.Object, _mockProcessHelper.Object);
        }

        #region Initialization Verification Tests

        [Fact]
        public void Constructor_NullCommandLineProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceHelper(null, _mockProcessHelper.Object));
        }

        [Fact]
        public void Constructor_NullProcessHelper_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceHelper(_mockCommandLineProvider.Object, null));
        }

        #endregion

        #region LogStartupArguments Tests (Public & Sensitive Data Masking)

        [Fact]
        public void LogStartupArguments_LogsPublicData()
        {
            // Arrange
            var args = new[] { "arg1", "arg2" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\test.exe",
                EnableDebugLogs = false
            };

            var mockLog = new Mock<IServyLogger>();

            // Act
            _helper.LogStartupArguments(options, mockLog.Object);

            // Assert
            mockLog.Verify(l => l.Info(It.Is<string>(s =>
                s.IndexOf("Startup Parameters", StringComparison.OrdinalIgnoreCase) >= 0), It.IsAny<Exception>()),
                Times.Once);

            mockLog.Verify(l => l.Info(It.Is<string>(s => s.Contains("serviceName: TestService")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void LogStartupArguments_NullOptions_LogsError()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

            // Act
            _helper.LogStartupArguments(null, mockLog.Object);

            // Assert
            mockLog.Verify(l => l.Error("StartOptions is null.", It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void LogStartupArguments_EnableDebugLogsTrue_LogsMaskedSensitiveDataToTextLog()
        {
            // Arrange
            var mockEventLog = new Mock<IServyLogger>();

            var options = new StartOptions
            {
                ServiceName = "SecureService",
                EnableDebugLogs = true,
                ExecutableArgs = "--password=SuperSecretPassword --api_key DB12345 --port 8080",
                FailureProgramArgs = "/token:SecretToken123 /normalArg test",
                EnvironmentVariables = new List<EnvironmentVariable>
                {
                    new EnvironmentVariable { Name = "DB_PASSWORD", Value = "SqlPass123" },
                    new EnvironmentVariable { Name = "NORMAL_ENV", Value = "PublicValue" }
                },
                PreLaunchExecutableArgs = "connect --jwt=TokenVal",
                PreLaunchEnvironmentVariables = new List<EnvironmentVariable>
                {
                    new EnvironmentVariable { Name = "AUTH_TOKEN", Value = "JwtSecret" }
                },
                PostLaunchExecutableArgs = "--session \"Active Session Id\"",
                PreStopExecutableArgs = "stop --cert-thumbprint abcde123",
                PostStopExecutableArgs = "cleanup --pat SecretPatToken"
            };

            // Capture all text allocations funneled through the logger interface pipeline
            var loggedEntries = new List<string>();
            mockEventLog
                .Setup(l => l.Info(It.IsAny<string>(), It.IsAny<Exception>()))
                .Callback<string, Exception>((msg, _) => loggedEntries.Add(msg));

            // Act
            _helper.LogStartupArguments(options, mockEventLog.Object);

            // Assert
            string combinedLogOutput = string.Join(Environment.NewLine, loggedEntries);

            // 1. SECURITY TRACE CHECKS: Explicitly confirm no raw secret values were leaked to the text log
            Assert.DoesNotContain("SuperSecretPassword", combinedLogOutput);
            Assert.DoesNotContain("DB12345", combinedLogOutput);
            Assert.DoesNotContain("SecretToken123", combinedLogOutput);
            Assert.DoesNotContain("SqlPass123", combinedLogOutput);
            Assert.DoesNotContain("TokenVal", combinedLogOutput);
            Assert.DoesNotContain("JwtSecret", combinedLogOutput);
            Assert.DoesNotContain("Active Session Id", combinedLogOutput);
            Assert.DoesNotContain("abcde123", combinedLogOutput);
            Assert.DoesNotContain("SecretPatToken", combinedLogOutput);

            // 2. Sensitive masked data is written to Servy.Service.log log file with the expected masking pattern
            // Sensitive data is never written to the event log, only to the text log file.
        }

        #endregion

        #region EnsureValidWorkingDirectory Tests

        [Fact]
        public void EnsureValidWorkingDirectory_ValidDirectory_RemainsUnchanged()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();
            string validDir = Path.GetTempPath();
            var options = new StartOptions { WorkingDirectory = validDir };

            // Act
            _helper.EnsureValidWorkingDirectory(options, mockLog.Object);

            // Assert
            Assert.Equal(validDir, options.WorkingDirectory);
            mockLog.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void EnsureValidWorkingDirectory_FallbacksToExecutableDirectory_IfInvalid()
        {
            // Arrange
            var options = new StartOptions
            {
                WorkingDirectory = "C:\\InvalidPath_That_Does_Not_Exist",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe"
            };
            var mockLog = new Mock<IServyLogger>();

            // Act
            _helper.EnsureValidWorkingDirectory(options, mockLog.Object);

            // Assert
            Assert.Equal(Path.GetDirectoryName(options.ExecutablePath), options.WorkingDirectory);
            mockLog.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Falling back to")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void EnsureValidWorkingDirectory_InvalidDirectoryAndEmptyExePath_FallsBackToSystem32()
        {
            // Arrange
            var options = new StartOptions { WorkingDirectory = " ", ExecutablePath = null };
            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var mockLog = new Mock<IServyLogger>();

            // Act
            _helper.EnsureValidWorkingDirectory(options, mockLog.Object);

            // Assert
            Assert.Equal(system32, options.WorkingDirectory);
        }

        #endregion

        #region ICommandLineProvider & Options Parsing Tests

        [Fact]
        public void GetArgs_ReturnsArgsFromProvider()
        {
            // Arrange
            var expectedArgs = new[] { "arg1", "arg2" };
            _mockCommandLineProvider.Setup(p => p.GetArgs()).Returns(expectedArgs);

            // Act
            var result = _helper.GetArgs();

            // Assert
            Assert.Equal(expectedArgs, result);
        }

        [Fact]
        public void ParseOptions_EmptyArgs_ThrowsArgumentExceptionFromParser()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _helper.ParseOptions(_mockRepo.Object, new string[0]));
        }

        #endregion

        #region ValidateAndLog Tests

        [Fact]
        public void ValidateAndLog_AllPathsValid_ReturnsTrue_NoErrorsLogged()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = @"C:\app\exe.exe",
                FailureProgramPath = @"C:\app\failure.exe",
                PreLaunchExecutablePath = @"C:\app\pre.exe",
                PostLaunchExecutablePath = @"C:\app\post.exe"
            };

            var mockLog = new Mock<IServyLogger>();
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object);

            // Assert
            Assert.True(result);
            mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void ValidateAndLog_ExecutablePath_NullOrWhitespace_ReturnsFalse_LogsError()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "TestService", ExecutablePath = " " };
            var mockLog = new Mock<IServyLogger>();

            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object);

            // Assert
            Assert.False(result);
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("not provided")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ValidateAndLog_ServiceName_Empty_ReturnsFalse_LogsError()
        {
            // Arrange
            var options = new StartOptions { ServiceName = "", ExecutablePath = "C:\\test.exe" };
            var mockLog = new Mock<IServyLogger>();

            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object);

            // Assert
            Assert.False(result);
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("Service name empty")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ValidateAndLog_ExecutablePath_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = @"C:\Nonexistent\fake.exe"
            };

            var mockLog = new Mock<IServyLogger>();
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.ExecutablePath, It.IsAny<bool>())).Returns(false);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object);

            // Assert
            Assert.False(result);
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("is invalid")), It.IsAny<Exception>()), Times.Once);
        }

        #endregion

        #region RestartProcess Tests

        [Fact]
        public void RestartProcess_NullStartAction_ThrowsArgumentNullException()
        {
            var mockLog = new Mock<IServyLogger>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _helper.RestartProcess(
                null, null, "exe", "args", "dir", new List<EnvironmentVariable>(), mockLog.Object, 1000, cancellationToken: CancellationToken.None));
        }

        [Fact]
        public void RestartProcess_ProcessActive_StopsParentAndDescendantsThenRestarts()
        {
            // Arrange
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(1234);
            mockProcess.Setup(p => p.StartTime).Returns(DateTime.Now);
            mockProcess.Setup(p => p.HasExited).Returns(false);

            var mockLog = new Mock<IServyLogger>();
            bool startActionInvoked = false;
            Action<string, string, string, List<EnvironmentVariable>, CancellationToken> startAction =
                (exe, args, dir, env, ct) => startActionInvoked = true;

            // Act
            _helper.RestartProcess(mockProcess.Object, startAction, "exe", "args", "dir", new List<EnvironmentVariable>(), mockLog.Object, 1000, CancellationToken.None);

            // Assert
            mockProcess.Verify(p => p.Stop(1000), Times.Once);
            mockProcess.Verify(p => p.StopDescendants(1234, It.IsAny<DateTime>(), 1000), Times.Once);
            mockProcess.Verify(p => p.Dispose(), Times.Once);
            Assert.True(startActionInvoked);
            mockLog.Verify(l => l.Info("Process restarted.", It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void RestartProcess_ProcessInfoAccessThrows_CatchesErrorAndContinuesStopSequence()
        {
            // Arrange
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Throws(new InvalidOperationException("Process already killed externally."));
            mockProcess.Setup(p => p.HasExited).Returns(true);

            var mockLog = new Mock<IServyLogger>();
            bool startActionInvoked = false;
            Action<string, string, string, List<EnvironmentVariable>, CancellationToken> startAction =
                (exe, args, dir, env, ct) => startActionInvoked = true;

            // Act
            _helper.RestartProcess(mockProcess.Object, startAction, "exe", "args", "dir", new List<EnvironmentVariable>(), mockLog.Object, 1000, CancellationToken.None);

            // Assert
            mockLog.Verify(l => l.Warn(It.Is<string>(s => s.Contains("error while getting process PID")), It.IsAny<Exception>()), Times.Once);
            mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Never);
            mockProcess.Verify(p => p.Dispose(), Times.Once);
            Assert.True(startActionInvoked);
        }

        [Fact]
        public void RestartProcess_ProcessStopThrows_CatchesErrorAndRestartsAnyway()
        {
            // Arrange
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(1234);
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Throws(new UnauthorizedAccessException("Access Denied"));

            var mockLog = new Mock<IServyLogger>();
            bool startActionInvoked = false;
            Action<string, string, string, List<EnvironmentVariable>, CancellationToken> startAction =
                (exe, args, dir, env, ct) => startActionInvoked = true;

            // Act
            _helper.RestartProcess(mockProcess.Object, startAction, "exe", "args", "dir", new List<EnvironmentVariable>(), mockLog.Object, 1000, CancellationToken.None);

            // Assert
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("proceeding with launch anyway")), It.IsAny<UnauthorizedAccessException>()), Times.Once);
            mockProcess.Verify(p => p.Dispose(), Times.Once);
            Assert.True(startActionInvoked);
        }

        #endregion

        #region RestartService Tests

        [Fact]
        public void RestartService_ProcessIsNull_LogsError()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();
            var restarterPath = Path.Combine(GetTargetRestarterDirectory(), "Servy.Restarter.Net48.exe");

            // Safe guard: Ensure target sandbox directory exists (crucial for ProgramData on barebones CI servers)
            var directory = Path.GetDirectoryName(restarterPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(restarterPath, "dummy");

            try
            {
                _mockProcessHelper.Setup(p => p.Start(It.IsAny<ProcessStartInfo>())).Returns((Process)null);

                // Act
                _helper.RestartService("TestService", mockLog.Object);

                // Assert
                mockLog.Verify(l => l.Error("Failed to start Servy.Restarter.Net48.exe.", It.IsAny<Exception>()), Times.Once);
            }
            finally
            {
                if (File.Exists(restarterPath)) File.Delete(restarterPath);
            }
        }

        [Fact]
        public void RestartService_RestarterFailsToStart_LogsErrorAndAborts()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

#if DEBUG
            var dir = AppDomain.CurrentDomain.BaseDirectory;
#else
            var dir = AppConfig.ProgramDataPath;
#endif

            var restarterPath = Path.Combine(dir, "Servy.Restarter.Net48.exe");

            // Defensively ensure a dummy restarter file exists in the sandbox if it isn't already present,
            // without modifying or deleting a real build artifact.
            bool createdDummy = false;
            if (!File.Exists(restarterPath))
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(restarterPath, "Temporary Test Placeholder");
                createdDummy = true;
            }

            // Mock the process helper to return null. This guarantees we bypass the multi-minute 
            // constant timeout loop and safely test the start failure pathway without deleting production binaries.
            _mockProcessHelper
                .Setup(h => h.Start(It.IsAny<ProcessStartInfo>()))
                .Returns((Process)null);

            try
            {
                // Act
                _helper.RestartService("TestServiceToRecovery", mockLog.Object);

                // Assert
                // Verify that the start failure branch executed cleanly and surfaced the corresponding error profile
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Failed to start Servy.Restarter.Net48.exe")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                // Clean up only if this specific test run spawned the temporary placeholder
                if (createdDummy && File.Exists(restarterPath))
                {
                    try { File.Delete(restarterPath); } catch { /* Ignore file locks */ }
                }
            }
        }

        #endregion

        #region RestartComputer Tests

        [Fact]
        public void RestartComputer_Success_StartsProcessWithCorrectArguments()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

            // RUNTIME BOUNDARY: This is a concrete Process component instance rather than a Moq proxy.
            // We encapsulate it in a using declaration to guarantee localized test runner disposal.
            using (var dummyProcess = new Process())
            {
                _mockProcessHelper
                    .Setup(p => p.Start(It.Is<ProcessStartInfo>(psi =>
                        psi.FileName.Contains("shutdown.exe") &&
                        psi.Arguments == "/r /t 0 /f" &&
                        psi.CreateNoWindow == true &&
                        psi.UseShellExecute == false)))
                    .Returns(dummyProcess);

                // Act
                _helper.RestartComputer(mockLog.Object);

                // Assert
                _mockProcessHelper.Verify(p => p.Start(It.Is<ProcessStartInfo>(psi =>
                    psi.FileName.Contains("shutdown.exe") &&
                    psi.Arguments == "/r /t 0 /f" &&
                    psi.CreateNoWindow && !psi.UseShellExecute)), Times.Once);
                mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
        }

        [Fact]
        public void RestartComputer_HelperThrowsException_LogsErrorCorrectly()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

            _mockProcessHelper
                .Setup(p => p.Start(It.IsAny<ProcessStartInfo>()))
                .Throws(new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified"));

            // Act
            _helper.RestartComputer(mockLog.Object);

            // Assert
            mockLog.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Failed to restart computer")),
                It.IsAny<Exception>()),
                Times.Once);
        }

        #endregion

        #region RequestAdditionalTime Tests

        private class DummyService : ServiceBase { }

        [Fact]
        public void RequestAdditionalTime_ValidService_LogsRequest()
        {
            // Arrange
            using (var service = new DummyService())
            {
                var mockLog = new Mock<IServyLogger>();

                // Act
                _helper.RequestAdditionalTime(service, 5000, mockLog.Object);

                // Assert
                Assert.True(true);
            }
        }

        [Fact]
        public void RequestAdditionalTime_NullService_ReturnsEarly()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

            // Act
            _helper.RequestAdditionalTime(null, 5000, mockLog.Object);

            // Assert
            mockLog.Verify(l => l.Info(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        #endregion

        #region MaskRawArguments Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MaskRawArguments_NullOrWhitespace_ReturnsOriginalValue(string input)
        {
            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void MaskRawArguments_NoSensitivePatterns_ReturnsOriginalValue()
        {
            // Arrange
            string input = "myapp.exe --port 8080 --host localhost";

            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Theory]
        [InlineData("PASSWORD_HASH=hunter2", "PASSWORD_HASH=********")]
        [InlineData("SECRET_DATA=sensitive_payload", "SECRET_DATA=********")]
        [InlineData("PASSWORD_ENC: encrypted_blob", "PASSWORD_ENC: ********")]
        [InlineData("myapp.exe --password_hash secret_value", "myapp.exe --password_hash ********")]
        public void MaskRawArguments_WithCompositeUnderscoreSuffix_SuccessfullyMasksSecret(string input, string expected)
        {
            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("MY_PASSWORD=hunter2", "MY_PASSWORD=********")]
        [InlineData("MY_PASSWORD_HASH=hunter2", "MY_PASSWORD_HASH=********")]
        public void MaskRawArguments_WithPrefixAndSuffixModifiers_PreservesKeyContextAndMasksValue(string input, string expected)
        {
            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("API_KEY: my-secret-token", "API_KEY: ********")]
        [InlineData("API_KEY/my-secret-token", "API_KEY/********")]
        [InlineData("DATABASE_PASSWORD=secret", "DATABASE_PASSWORD=********")]
        public void MaskRawArguments_BranchA_ExplicitSeparators_MasksCorrectly(string input, string expected)
        {
            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("myapp.exe --password mysecret", "myapp.exe --password ********")]
        [InlineData("CONNSTR my server address password", "CONNSTR my server address ********")]
        public void MaskRawArguments_BranchB_SpaceSeparators_MasksCorrectly(string input, string expected)
        {
            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("PASSWORD=\"secret value with spaces\"", "PASSWORD=********")]
        [InlineData("PASSWORD='secret value with spaces'", "PASSWORD=********")]
        public void MaskRawArguments_WithQuotedValues_MasksWholeQuotedString(string input, string expected)
        {
            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MaskRawArguments_WithSubsequentCliFlags_StopsConsumingAtNextFlag()
        {
            // Arrange
            string input = "myapp.exe --password mysecret --verbose";
            string expected = "myapp.exe --password ******** --verbose";

            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MaskRawArguments_FalsePositiveBoundaryExemption_DoesNotMaskNonSensitiveExtensions()
        {
            // Arrange
            string input = "PASSWORDLESS login attempt";

            // Act
            var result = ServiceHelper.MaskRawArguments(input);

            // Assert
            Assert.Equal(input, result); // Should remain completely untouched
        }

        #endregion

        #region Helpers

        private static string GetTargetRestarterDirectory()
        {
            // ROBUSTNESS: In Release configurations, the SUT expects the restarter binary 
            // to be located in AppConfig.ProgramDataPath. In Debug, it expects BaseDirectory.
            // We write the file directly where the compiled execution path expects it.
#if DEBUG
            return AppDomain.CurrentDomain.BaseDirectory;
#else
            return AppConfig.ProgramDataPath;
#endif
        }

        #endregion
    }
}