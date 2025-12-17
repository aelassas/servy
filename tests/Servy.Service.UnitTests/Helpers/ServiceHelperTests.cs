using Moq;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

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

        [Fact]
        public void ValidateStartupOptions_ReturnsFalse_WhenExecutablePathIsMissing()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = ""
            };

            var mockLog = new Mock<ILogger>();

            // Act
            var result = _helper.ValidateStartupOptions(mockLog.Object, options);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateStartupOptions_ReturnsFalse_WhenExecutableDoesNotExist()
        {
            // Arrange
            var options = new StartOptions
            {
                ServiceName = "TestService",
                ExecutablePath = "C:\\Nonexistent\\fake.exe"
            };

            var mockLog = new Mock<ILogger>();

            // Act
            var result = _helper.ValidateStartupOptions(mockLog.Object, options);

            // Assert
            Assert.False(result);
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
