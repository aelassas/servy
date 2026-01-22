using Moq;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using System.Diagnostics;

namespace Servy.Service.UnitTests.Helpers
{
    public class ServiceHelperTests
    {
        private readonly Mock<ICommandLineProvider> _mockCommandLineProvider;
        private readonly ServiceHelper _helper;

        public ServiceHelperTests()
        {
            _mockCommandLineProvider = new Mock<ICommandLineProvider>();
            _helper = new ServiceHelper(_mockCommandLineProvider.Object);
        }

        [Fact]
        public void GetSanitizedArgs_RemovesQuotesAndWhitespace()
        {
            // Arrange
            var originalArgs = new[] { "ignored.exe", " \"arg1\" ", "\"arg2\"" };
            var expectedArgs = new[] { "arg1", "arg2" };

            _mockCommandLineProvider.Setup(p => p.GetArgs()).Returns(originalArgs);

            // Act
            var result = _helper.GetSanitizedArgs();

            // Assert
            Assert.Contains(expectedArgs[0], result);
            Assert.Contains(expectedArgs[1], result);
        }

        // Helper: create a temporary file
        private string TempFile() => Path.GetTempFileName();

        [Fact]
        public void ValidateStartupOptions_ExecutablePath_NullOrWhitespace_ReturnsFalse_LogsError()
        {
            var options = new StartOptions { ServiceName = "TestService", ExecutablePath = " " };
            var mockLog = new Mock<ILogger>();

            var result = _helper.ValidateStartupOptions(mockLog.Object, options);

            Assert.False(result);
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("Executable path not provided.")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ValidateStartupOptions_ServiceName_Empty_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions { ServiceName = "", ExecutablePath = tempExe };
                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains("Service name empty")), It.IsAny<Exception>()), Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_ExecutablePath_Invalid_ReturnsFalse_LogsError()
        {
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = @"C:\Nonexistent\fake.exe"
            };
            var mockLog = new Mock<ILogger>();

            var result = _helper.ValidateStartupOptions(mockLog.Object, options);

            Assert.False(result);
            mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains($"Process path {options.ExecutablePath} is invalid.")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ValidateStartupOptions_FailureProgramPath_Invalid_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = tempExe,
                    FailureProgramPath = @"C:\Invalid\failure.exe"
                };
                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains($"Failure program path {options.FailureProgramPath} is invalid.")), It.IsAny<Exception>()), Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_PreLaunchExecutablePath_Invalid_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = tempExe,
                    PreLaunchExecutablePath = @"C:\Invalid\prelaunch.exe"
                };
                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains($"Pre-launch process path {options.PreLaunchExecutablePath} is invalid.")), It.IsAny<Exception>()), Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_PostLaunchExecutablePath_Invalid_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = tempExe,
                    PostLaunchExecutablePath = @"C:\Invalid\postlaunch.exe"
                };
                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(It.Is<string>(s => s.Contains($"Post-launch process path {options.PostLaunchExecutablePath} is invalid.")), It.IsAny<Exception>()), Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_AllPathsValid_ReturnsTrue_NoLogs()
        {
            var exe = TempFile();
            var failure = TempFile();
            var pre = TempFile();
            var post = TempFile();

            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = exe,
                    FailureProgramPath = failure,
                    PreLaunchExecutablePath = pre,
                    PostLaunchExecutablePath = post
                };
                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.True(result);
                mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
            finally
            {
                File.Delete(exe);
                File.Delete(failure);
                File.Delete(pre);
                File.Delete(post);
            }
        }

        [Fact]
        public void ValidateStartupOptions_OptionalPaths_NullOrWhitespace_Skipped_ReturnsTrue()
        {
            var exe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = exe,
                    FailureProgramPath = null,
                    PreLaunchExecutablePath = "",
                    PostLaunchExecutablePath = "   "
                };
                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.True(result);
                mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
            finally
            {
                File.Delete(exe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_WorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = tempExe,
                    WorkingDirectory = @"C:\Invalid\workingdir"
                };

                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains($"Process working directory {options.WorkingDirectory} is invalid.")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_FailureProgramWorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = tempExe,
                    FailureProgramWorkingDirectory = @"C:\Invalid\failureWorkDir"
                };

                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Failure program working directory")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_PreLaunchWorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = tempExe,
                    PreLaunchWorkingDirectory = @"C:\Invalid\preLaunchWorkDir"
                };

                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Pre-launch process working directory")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void ValidateStartupOptions_PostLaunchWorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            var tempExe = TempFile();
            try
            {
                var options = new StartOptions
                {
                    ServiceName = "TestService",
                    ExecutablePath = tempExe,
                    PostLaunchWorkingDirectory = @"C:\Invalid\postLaunchWorkDir"
                };

                var mockLog = new Mock<ILogger>();

                var result = _helper.ValidateStartupOptions(mockLog.Object, options);

                Assert.False(result);
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Post-launch process working directory")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                File.Delete(tempExe);
            }
        }

        [Fact]
        public void EnsureValidWorkingDirectory_FallbacksToExecutableDirectory_IfInvalid()
        {
            // Arrange
            var options = new StartOptions
            {
                WorkingDirectory = "C:\\InvalidPath",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe"
            };

            var mockLog = new Mock<ILogger>();

            // Act
            _helper.EnsureValidWorkingDirectory(options, mockLog.Object);

            // Assert
            Assert.Equal(Path.GetDirectoryName(options.ExecutablePath), options.WorkingDirectory);
        }



        [Fact]
        public void LogStartupArguments_WritesToILogger_WhenOptionsProvided()
        {
            // Arrange
            var args = new[] { "arg1", "arg2" };
            var options = new StartOptions
            {
                ServiceName = "MyService",
                ExecutablePath = "path.exe",
                ExecutableArgs = "--test",
                WorkingDirectory = "C:\\Work",
                Priority = ProcessPriorityClass.Normal,
                StdOutPath = "stdout.txt",
                StdErrPath = "stderr.txt",
                RotationSizeInBytes = 1024,
                HeartbeatInterval = 10,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.RestartService,
                MaxRestartAttempts = 5
            };

            var mockLog = new Mock<ILogger>();
            mockLog.Setup(l => l.Info(It.IsAny<string>()))
                   .Verifiable();

            // Act
            _helper.LogStartupArguments(mockLog.Object, args, options);

            // Assert
            mockLog.Verify(l => l.Info(It.Is<string>(s => s.Contains("[Startup Parameters]"))), Times.Once);
        }

        [Fact]
        public void InitializeStartup_Throws_IfServiceNameIsEmpty()
        {
            // Arrange
            _mockCommandLineProvider.Setup(p => p.GetArgs()).Returns(new string[] { "ignored.exe" }); // no valid args
            var mockLog = new Mock<ILogger>();

            // Assert
            var result = Assert.Throws<ArgumentException>(() => _helper.InitializeStartup(mockLog.Object));
        }
    }
}
