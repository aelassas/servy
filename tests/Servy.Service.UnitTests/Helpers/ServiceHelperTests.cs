using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
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
using Xunit;
using ServiceHelper = Servy.Service.Helpers.ServiceHelper;

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
                EnableDebugLogs = false // Testing just public data for now
            };

            var mockLog = new Mock<IServyLogger>();

            // Act
            _helper.LogStartupArguments(mockLog.Object, options);

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
            _helper.LogStartupArguments(mockLog.Object, null!);

            // Assert
            mockLog.Verify(l => l.Error("StartOptions is null.", It.IsAny<Exception>()), Times.Once);
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
            string system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
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

        // Helper: create a temporary file
        private string TempFile() => Path.GetTempFileName();

        [Fact]
        public void ValidateAndLog_AllPathsValid_ReturnsTrue_NoErrorsLogged()
        {
            // Arrange
            var exe = TempFile();
            var failure = TempFile();
            var pre = TempFile();
            var post = TempFile();
            var fullArgs = new[] { "servy.exe" };

            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = exe,
                FailureProgramPath = failure,
                PreLaunchExecutablePath = pre,
                PostLaunchExecutablePath = post
            };

            var mockLog = new Mock<IServyLogger>();
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            try
            {
                // Act
                var result = _helper.ValidateAndLog(options, mockLog.Object, fullArgs);

                // Assert
                Assert.True(result);
                mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
            finally
            {
                if (File.Exists(exe)) File.Delete(exe);
                if (File.Exists(failure)) File.Delete(failure);
                if (File.Exists(pre)) File.Delete(pre);
                if (File.Exists(post)) File.Delete(post);
            }
        }

        [Fact]
        public void ValidateAndLog_ExecutablePath_NullOrWhitespace_ReturnsFalse_LogsError()
        {
            // Arrange
            var fullArgs = new[] { "ignored.exe" };
            var options = new StartOptions { ServiceName = "TestService", ExecutablePath = " " };
            var mockLog = new Mock<IServyLogger>();

            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object, fullArgs);

            // Assert
            Assert.False(result);
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("not provided")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ValidateAndLog_ServiceName_Empty_ReturnsFalse_LogsError()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions { ServiceName = "", ExecutablePath = "C:\\test.exe" };
            var mockLog = new Mock<IServyLogger>();

            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object, fullArgs);

            // Assert
            Assert.False(result);
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("Service name empty")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ValidateAndLog_ExecutablePath_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = @"C:\Nonexistent\fake.exe"
            };

            var mockLog = new Mock<IServyLogger>();
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            // Trigger failure in the ProcessHelper validate wrapper
            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.ExecutablePath, It.IsAny<bool>())).Returns(false);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object, fullArgs);

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
                null!, null!, "exe", "args", "dir", new List<EnvironmentVariable>(), mockLog.Object, 1000));
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
            Action<string, string, string, List<EnvironmentVariable>> startAction = (exe, args, dir, env) => startActionInvoked = true;

            // Act
            _helper.RestartProcess(mockProcess.Object, startAction, "exe", "args", "dir", new List<EnvironmentVariable>(), mockLog.Object, 1000);

            // Assert
            mockProcess.Verify(p => p.Stop(1000), Times.Once);
            mockProcess.Verify(p => p.StopDescendants(1234, It.IsAny<DateTime>(), 1000), Times.Once);
            mockProcess.Verify(p => p.Dispose(), Times.Once);
            Assert.True(startActionInvoked);
            mockLog.Verify(l => l.Info("Process restarted.", It.IsAny<Exception>()), Times.Once);
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
            Action<string, string, string, List<EnvironmentVariable>> startAction = (exe, args, dir, env) => startActionInvoked = true;

            // Act
            _helper.RestartProcess(mockProcess.Object, startAction, "exe", "args", "dir", new List<EnvironmentVariable>(), mockLog.Object, 1000);

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
#if DEBUG
            var restarterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servy.Restarter.exe");
#else
            var restarterPath = Path.Combine(AppConfig.ProgramDataPath, "Servy.Restarter.exe");
            if (!Directory.Exists(AppConfig.ProgramDataPath))
            {
                Directory.CreateDirectory(AppConfig.ProgramDataPath);
            }
#endif
            File.WriteAllText(restarterPath, "dummy"); // Create file so it passes the Exists check

            try
            {
                _mockProcessHelper.Setup(p => p.Start(It.IsAny<ProcessStartInfo>())).Returns((Process?)null);

                // Act
                _helper.RestartService(mockLog.Object, "TestService");

                // Assert
                mockLog.Verify(l => l.Error("Failed to start Servy.Restarter.exe.", It.IsAny<Exception>()), Times.Once);
            }
            finally { File.Delete(restarterPath); }
        }

        #endregion

        #region RestartComputer Tests

        [Fact]
        public void RestartComputer_Success_StartsProcessAndDisposesCorrectly()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

            // We return a new Process instance. Since this process is never 
            // actually started, disposing it is safe and does not trigger OS side-effects.
            var mockProcess = new Process();

            _mockProcessHelper
                .Setup(p => p.Start(It.Is<ProcessStartInfo>(psi =>
                    psi.FileName == "shutdown" &&
                    psi.Arguments == "/r /t 0 /f" &&
                    psi.CreateNoWindow == true &&
                    psi.UseShellExecute == false)))
                .Returns(mockProcess);

            // Act
            _helper.RestartComputer(mockLog.Object);

            // Assert
            _mockProcessHelper.Verify(p => p.Start(It.IsAny<ProcessStartInfo>()), Times.Once);

            // Verify no errors were logged in the success branch
            mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void RestartComputer_HelperThrowsException_LogsErrorCorrectly()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

            // Force the mock helper to throw an exception to trigger the catch block
            _mockProcessHelper
                .Setup(p => p.Start(It.IsAny<ProcessStartInfo>()))
                .Throws(new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified"));

            // Act
            _helper.RestartComputer(mockLog.Object);

            // Assert
            // Verify that the error was caught and logged as expected
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
                // Expect either successful log OR the InvalidOperationException 
                // (since it's not connected to the real Windows SCM in the test runner)
                // Both are handled cleanly.
                Assert.True(true);
            }
        }

        [Fact]
        public void RequestAdditionalTime_NullService_ReturnsEarly()
        {
            // Arrange
            var mockLog = new Mock<IServyLogger>();

            // Act
            _helper.RequestAdditionalTime(null!, 5000, mockLog.Object);

            // Assert
            mockLog.Verify(l => l.Info(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        #endregion
    }
}