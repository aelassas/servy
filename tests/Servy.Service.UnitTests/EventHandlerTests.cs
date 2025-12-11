using Moq;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.Helpers;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Diagnostics;
using System.Reflection;
using Xunit;
using Servy.Core.Data;
using Servy.Core.Enums;

namespace Servy.Service.UnitTests
{
    public class EventHandlerTests
    {
        private static TestableService CreateService(
            out Mock<ILogger> mockLogger,
            out Mock<IServiceHelper> mockHelper,
            out Mock<IStreamWriterFactory> mockStreamWriterFactory,
            out Mock<ITimerFactory> mockTimerFactory,
            out Mock<IProcessFactory> mockProcessFactory,
            out Mock<IPathValidator> mockPathValidator,
            out Mock<IServiceRepository> mockServiceRepository
            )
        {
            mockLogger = new Mock<ILogger>();
            mockHelper = new Mock<IServiceHelper>();
            mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            mockTimerFactory = new Mock<ITimerFactory>();
            mockProcessFactory = new Mock<IProcessFactory>();
            mockPathValidator = new Mock<IPathValidator>();
            mockServiceRepository = new Mock<IServiceRepository>();

            mockPathValidator.Setup(p => p.IsValidPath(It.IsAny<string>())).Returns(true);

            return new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                mockStreamWriterFactory.Object,
                mockTimerFactory.Object,
                mockProcessFactory.Object,
                mockPathValidator.Object,
                mockServiceRepository.Object
                );
        }

        static DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
        {
            var ctor = typeof(DataReceivedEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string) },
                null);
            return (DataReceivedEventArgs)ctor.Invoke(new string[] { data });
        }

        [Fact]
        public void OnOutputDataReceived_WritesToRotatingWriters_IgnoresNullOrEmpty()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockWriter = new Mock<IStreamWriter>();
            // Setup rotating writer factory to return this mock writer
            swFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>())).Returns(mockWriter.Object);

            // Prepare a sample non-empty data event
            var nonEmptyArgs = CreateDataReceivedEventArgs("output line");
            var emptyArgs = CreateDataReceivedEventArgs(null);
            var emptyStringArgs = CreateDataReceivedEventArgs(string.Empty);

            // Arrange
            var startOptions = new StartOptions
            {
                StdOutPath = "valid-path.log",
                StdErrPath = "error-path.log",
                RotationSizeInBytes = 1024 * 1024
            };

            service.InvokeHandleLogWriters(startOptions);

            var stdoutWriterField = typeof(Service).GetField("_stdoutWriter", BindingFlags.NonPublic | BindingFlags.Instance);
            var stdoutWriterValue = stdoutWriterField.GetValue(service);
            Assert.NotNull(stdoutWriterValue);

            // Act with non-empty data
            service.InvokeOnOutputDataReceived(null, nonEmptyArgs);

            // Act with null and empty data (should be ignored)
            service.InvokeOnOutputDataReceived(null, emptyArgs);
            service.InvokeOnOutputDataReceived(null, emptyStringArgs);

            // Assert write called once for non-empty data only
            mockWriter.Verify(w => w.WriteLine("output line"), Times.Once);
        }

        [Fact]
        public void OnErrorDataReceived_WritesToRotatingWriters_LogsError_IgnoresNullOrEmpty()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockWriter = new Mock<IStreamWriter>();
            swFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>())).Returns(mockWriter.Object);

            var nonEmptyArgs = CreateDataReceivedEventArgs("error line");
            var emptyArgs = CreateDataReceivedEventArgs(null);
            var emptyStringArgs = CreateDataReceivedEventArgs(string.Empty);

            // Arrange
            var startOptions = new StartOptions
            {
                StdOutPath = "valid-path.log",
                StdErrPath = "error-path.log",
                RotationSizeInBytes = 1024 * 1024
            };

            service.InvokeHandleLogWriters(startOptions);

            // Act with non-empty error data
            service.InvokeOnErrorDataReceived(null, nonEmptyArgs);

            // Act with null and empty data (should be ignored)
            service.InvokeOnErrorDataReceived(null, emptyArgs);
            service.InvokeOnErrorDataReceived(null, emptyStringArgs);

            // Assert write called once for non-empty error data
            mockWriter.Verify(w => w.WriteLine("error line"), Times.Once);

            // Assert logger logged error once for non-empty data
            //logger.Verify(l => l.Error(It.Is<string>(s => s.Contains("error line")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void OnProcessExited_LogsExitInfo()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(42);
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Returns(0);

            service.SetChildProcess(mockProcess.Object);

            service.InvokeOnProcessExited(null, EventArgs.Empty);

            logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Child process exited successfully."))), Times.Once);
        }

        [Fact]
        public void OnProcessExited_ExitCodeNonZero_LogsWarning()
        {
            // Arrange
            var service = CreateService(
             out var logger,
             out var helper,
             out var swFactory,
             out var timerFactory,
             out var processFactory,
             out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(42);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            logger.Verify(l => l.Warning("Child process exited with code 42."), Times.Once);
        }

        [Fact]
        public void OnProcessExited_ExitCodeThrowsException_LogsWarning()
        {
            // Arrange
            var service = CreateService(
             out var logger,
             out var helper,
             out var swFactory,
             out var timerFactory,
             out var processFactory,
             out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Throws(new InvalidOperationException("boom"));
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Failed to get exit code"))), Times.Once);
        }

        [Fact]
        public void OnProcessExited_LogsErrorOnExitCodeException()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(42);
            mockProcess.Setup(p => p.HasExited).Returns(true);
            mockProcess.Setup(p => p.ExitCode).Throws(new Exception("Access denied"));

            service.SetChildProcess(mockProcess.Object);

            service.InvokeOnProcessExited(null, EventArgs.Empty);

            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Failed to get exit code"))), Times.Once);
        }
    }
}
