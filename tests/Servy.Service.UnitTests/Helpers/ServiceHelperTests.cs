using Moq;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using ServiceHelper = Servy.Service.Helpers.ServiceHelper;

namespace Servy.Service.UnitTests.Helpers
{
    public class ServiceHelperTests
    {
        private readonly Mock<ICommandLineProvider> _mockCommandLineProvider;
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly ServiceHelper _helper;

        public ServiceHelperTests()
        {
            _mockCommandLineProvider = new Mock<ICommandLineProvider>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockProcessHelper.Setup(ph => ph.ValidatePath(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);
            _helper = new ServiceHelper(_mockCommandLineProvider.Object);
        }

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

            // We only need one mock here because this method just uses the provided logger
            var mockLog = new Mock<IServyLogger>();

            // Act
            // We pass the mock directly. In the real app, OnStart passes the Scoped logger here.
            _helper.LogStartupArguments(mockLog.Object, args, options);

            // Assert
            // 1. Verify the logger received the info message.
            mockLog.Verify(l => l.Info(It.Is<string>(s =>
                s.IndexOf("Startup Parameters", StringComparison.OrdinalIgnoreCase) > -1)),
                Times.Once);

            // 2. Verify that specific public data fields exist in that log string
            mockLog.Verify(l => l.Info(It.Is<string>(s => s.Contains("serviceName: TestService"))), Times.Once);
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
        public void ValidateAndLog_ExecutablePath_NullOrWhitespace_ReturnsFalse_LogsError()
        {
            // Arrange
            var fullArgs = new[] { "ignored.exe" };
            var options = new StartOptions { ServiceName = "TestService", ExecutablePath = " " };
            var mockLog = new Mock<IServyLogger>();

            // Setup: We must handle CreateScoped because ValidateAndLog calls LogStartupArguments,
            // which creates a scope.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            // Act
            var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

            // Assert
            Assert.False(result);
            mockLog.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Executable path not provided.")),
                It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void ValidateAndLog_ServiceName_Empty_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe", "--name", "" };
            var options = new StartOptions { ServiceName = "", ExecutablePath = tempExe };

            var mockLog = new Mock<IServyLogger>();
            // Setup: Handle CreateScoped because ValidateAndLog calls LogStartupArguments internally
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(mockLog.Object);

            try
            {
                // Act - Using the new unified entry point
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the specific validation error was logged
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Service name empty")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe)) File.Delete(tempExe);
            }
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

            // Setup: LogStartupArguments creates a scope. We return the same mock 
            // so we can verify the error call on it.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.ExecutablePath, It.IsAny<bool>())).Returns(false);

            // Act
            // We call the new orchestration method instead of the old private one
            var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

            // Assert
            Assert.False(result);

            // Verify the error was logged to the logger instance
            mockLog.Verify(l => l.Error(
                It.Is<string>(s => s.Contains($"Process path {options.ExecutablePath} is invalid.")),
                It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void ValidateAndLog_FailureProgramPath_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = tempExe,
                FailureProgramPath = @"C:\Invalid\failure.exe"
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: LogStartupArguments creates a scope. 
            // We return the same mock object to verify the error call on it.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.FailureProgramPath, It.IsAny<bool>())).Returns(false);

            try
            {
                // Act
                // Use the new orchestration method
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the validation error was logged to the logger instance
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains($"Failure program path {options.FailureProgramPath} is invalid.")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    File.Delete(tempExe);
                }
            }
        }

        [Fact]
        public void ValidateAndLog_PreLaunchExecutablePath_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = tempExe,
                PreLaunchExecutablePath = @"C:\Invalid\prelaunch.exe"
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: ValidateAndLog calls LogStartupArguments, which creates a scope.
            // We return the same mock so the verification below catches the error call.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.PreLaunchExecutablePath, It.IsAny<bool>())).Returns(false);

            try
            {
                // Act
                // Use the new orchestration method that handles logging and validation
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the specific pre-launch validation error was logged
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains($"Pre-launch process path {options.PreLaunchExecutablePath} is invalid.")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    File.Delete(tempExe);
                }
            }
        }

        [Fact]
        public void ValidateAndLog_PostLaunchExecutablePath_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = tempExe,
                PostLaunchExecutablePath = @"C:\Invalid\postlaunch.exe"
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: ValidateAndLog calls LogStartupArguments, which creates a scope.
            // We return the same mock so the verification below catches the error call.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.PostLaunchExecutablePath, It.IsAny<bool>())).Returns(false);

            try
            {
                // Act
                // Use the new orchestration method that handles both logging and validation
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the specific post-launch validation error was logged
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains($"Post-launch process path {options.PostLaunchExecutablePath} is invalid.")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    File.Delete(tempExe);
                }
            }
        }

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

            // Setup: Handle the scoping that happens inside LogStartupArguments
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            try
            {
                // Act
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.True(result);

                // Ensure no Errors were logged (Info logs are expected and okay)
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
        public void ValidateAndLog_OptionalPaths_NullOrWhitespace_Skipped_ReturnsTrue()
        {
            // Arrange
            var exe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = exe,
                FailureProgramPath = null,
                PreLaunchExecutablePath = "",
                PostLaunchExecutablePath = "   "
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: Handle the scoping logic inside LogStartupArguments.
            // Returning the same mock object allows Verify to track all calls.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            try
            {
                // Act
                // Use the new orchestration method
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.True(result);

                // Verify that no errors were logged during validation
                mockLog.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            }
            finally
            {
                if (File.Exists(exe))
                {
                    File.Delete(exe);
                }
            }
        }

        [Fact]
        public void ValidateAndLog_WorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = tempExe,
                WorkingDirectory = @"C:\Invalid\workingdir"
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: Handle the scoping logic that happens inside ValidateAndLog -> LogStartupArguments
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.WorkingDirectory, It.IsAny<bool>())).Returns(false);

            try
            {
                // Act
                // Use the new orchestration method that handles logging and validation
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the specific working directory error was logged
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains($"Process working directory {options.WorkingDirectory} is invalid.")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    File.Delete(tempExe);
                }
            }
        }

        [Fact]
        public void ValidateAndLog_FailureProgramWorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = tempExe,
                FailureProgramWorkingDirectory = @"C:\Invalid\failureWorkDir"
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: Handle the scoping logic inside ValidateAndLog -> LogStartupArguments.
            // Returning the same mock object allows Verify to track all calls on the same instance.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.FailureProgramWorkingDirectory, It.IsAny<bool>())).Returns(false);

            try
            {
                // Act
                // Call the new unified method for logging and validation
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the specific error for the failure program working directory was logged
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Failure program working directory")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    File.Delete(tempExe);
                }
            }
        }

        [Fact]
        public void ValidateAndLog_PreLaunchWorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = tempExe,
                PreLaunchWorkingDirectory = @"C:\Invalid\preLaunchWorkDir"
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: Handle the scoping logic inside ValidateAndLog -> LogStartupArguments.
            // We return the same mock object so we can verify the error call on it later.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.PreLaunchWorkingDirectory, It.IsAny<bool>())).Returns(false);

            try
            {
                // Act
                // Use the new orchestration method that handles both logging and validation
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the specific pre-launch working directory error was logged
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Pre-launch process working directory")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    File.Delete(tempExe);
                }
            }
        }

        [Fact]
        public void ValidateAndLog_PostLaunchWorkingDirectory_Invalid_ReturnsFalse_LogsError()
        {
            // Arrange
            var tempExe = TempFile();
            var fullArgs = new[] { "servy.exe" };
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = tempExe,
                PostLaunchWorkingDirectory = @"C:\Invalid\postLaunchWorkDir"
            };

            var mockLog = new Mock<IServyLogger>();

            // Setup: Handle the scoping logic inside ValidateAndLog -> LogStartupArguments.
            // We return the same mock object so we can verify the error call on it later.
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);

            _mockProcessHelper.Setup(ph => ph.ValidatePath(options.PostLaunchWorkingDirectory, It.IsAny<bool>())).Returns(false);

            try
            {
                // Act
                // Use the new orchestration method that handles both logging and validation
                var result = _helper.ValidateAndLog(options, mockLog.Object, _mockProcessHelper.Object, fullArgs);

                // Assert
                Assert.False(result);

                // Verify the specific post-launch working directory error was logged
                mockLog.Verify(l => l.Error(
                    It.Is<string>(s => s.Contains("Post-launch process working directory")),
                    It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    File.Delete(tempExe);
                }
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

            var mockLog = new Mock<IServyLogger>();

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

            var mockLog = new Mock<IServyLogger>();
            mockLog.Setup(l => l.CreateScoped(It.IsAny<string>()))
                   .Returns(mockLog.Object);
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
            var mockLog = new Mock<IServyLogger>();
            var mockServiceRepo = new Mock<IServiceRepository>();

            // Assert
            var result = Assert.Throws<ArgumentException>(() => _helper.InitializeStartup(mockServiceRepo.Object, _mockProcessHelper.Object, mockLog.Object));
        }
    }
}
