using Moq;
using Servy.Core.Enums;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.UnitTests.Helpers;
using Servy.Service.UnitTests.Utilities;
using Servy.Testing;

namespace Servy.Service.UnitTests
{
    public class EventHandlerTests : IDisposable
    {
        private readonly List<IDisposable> _disposableServices = new List<IDisposable>();

        private static StartOptions CreateDefaultStartOptions() => new StartOptions
        {
            StdoutPath = "valid-path.log",
            StderrPath = "error-path.log",
            RecoveryOnCleanExit = false,
            HeartbeatUrl = "https://127.0.0.1:1/test-uuid",
            HeartbeatUrlTimeoutInSeconds = 10,
            HeartbeatIntervalInSeconds = 30
        };

        [Fact]
        public void OnOutputDataReceived_WritesToRotatingWriters_IgnoresNullOrEmpty()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build();
            _disposableServices.Add(service); // Track SUT instance for teardown disposal

            var mockStdoutWriter = new Mock<IStreamWriter>();
            var mockStderrWriter = new Mock<IStreamWriter>();

            ctx.StreamWriterFactory
               .Setup(f => f.Create("valid-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStdoutWriter.Object);

            ctx.StreamWriterFactory
               .Setup(f => f.Create("error-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStderrWriter.Object);

            var nonEmptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("output line");
            var emptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(null!);
            var emptyStringArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(string.Empty);

            var startOptions = CreateDefaultStartOptions();
            startOptions.RotationSizeInBytes = 1024 * 1024;

            service.InvokeHandleLogWriters(startOptions);

            var stdoutWriterValue = TestReflection.GetField<object>(service, "_stdoutWriter");
            var stderrWriterValue = TestReflection.GetField<object>(service, "_stderrWriter");
            Assert.NotNull(stdoutWriterValue);
            Assert.NotNull(stderrWriterValue);
            Assert.NotSame(stdoutWriterValue, stderrWriterValue);

            // Act
            service.InvokeOnOutputDataReceived(null, nonEmptyArgs);
            service.InvokeOnOutputDataReceived(null, emptyArgs);
            service.InvokeOnOutputDataReceived(null, emptyStringArgs);

            // Assert
            // 1. Verify the non-empty line was written exactly once to stdout writer
            mockStdoutWriter.Verify(w => w.WriteLine("output line"), Times.Once);

            // 2. Verify that blank lines (empty strings) are written to preserve log formatting
            mockStdoutWriter.Verify(w => w.WriteLine(string.Empty), Times.Once);

            // 3. Verify that the stream-end sentinel (null) is ignored completely
            mockStdoutWriter.Verify(w => w.WriteLine(null!), Times.Never);

            // 4. Verify that stdout lines are never written to the stderr writer
            mockStderrWriter.Verify(w => w.WriteLine(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void OnErrorDataReceived_WritesToRotatingWriters_IgnoresNullOrEmpty()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build();
            _disposableServices.Add(service);

            var mockStdoutWriter = new Mock<IStreamWriter>();
            var mockStderrWriter = new Mock<IStreamWriter>();

            ctx.StreamWriterFactory
               .Setup(f => f.Create("valid-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStdoutWriter.Object);

            ctx.StreamWriterFactory
               .Setup(f => f.Create("error-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStderrWriter.Object);

            var nonEmptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("error line");
            var emptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(null!);
            var emptyStringArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(string.Empty);

            var startOptions = CreateDefaultStartOptions();
            startOptions.RotationSizeInBytes = 1024 * 1024;

            service.InvokeHandleLogWriters(startOptions);

            // Symmetry Verification: Assert the private _stderrWriter field was populated via reflection
            var stdoutWriterValue = TestReflection.GetField<object>(service, "_stdoutWriter");
            var stderrWriterValue = TestReflection.GetField<object>(service, "_stderrWriter");
            Assert.NotNull(stdoutWriterValue);
            Assert.NotNull(stderrWriterValue);
            Assert.NotSame(stdoutWriterValue, stderrWriterValue);

            // Act
            service.InvokeOnErrorDataReceived(null, nonEmptyArgs);
            service.InvokeOnErrorDataReceived(null, emptyArgs);
            service.InvokeOnErrorDataReceived(null, emptyStringArgs);

            // Assert
            // 1. Verify the non-empty line was written exactly once to stderr writer
            mockStderrWriter.Verify(w => w.WriteLine("error line"), Times.Once);

            // 2. Verify that blank lines (empty strings) are written to preserve log formatting
            mockStderrWriter.Verify(w => w.WriteLine(string.Empty), Times.Once);

            // 3. Verify that the stream-end sentinel (null) is ignored completely
            mockStderrWriter.Verify(w => w.WriteLine(null!), Times.Never);

            // 4. Verify that stderr lines are never written to the stdout writer
            mockStdoutWriter.Verify(w => w.WriteLine(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void HandleLogWriters_SameStdoutAndStderrPath_MultiplexesToSingleWriter()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build();
            _disposableServices.Add(service);

            var mockSharedWriter = new Mock<IStreamWriter>();

            ctx.StreamWriterFactory
               .Setup(f => f.Create("shared-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockSharedWriter.Object);

            var startOptions = CreateDefaultStartOptions();
            startOptions.StdoutPath = "shared-path.log";
            startOptions.StderrPath = "shared-path.log";

            // Act
            service.InvokeHandleLogWriters(startOptions);

            var stdoutWriterValue = TestReflection.GetField<object>(service, "_stdoutWriter");
            var stderrWriterValue = TestReflection.GetField<object>(service, "_stderrWriter");

            // Assert
            Assert.NotNull(stdoutWriterValue);
            Assert.NotNull(stderrWriterValue);
            Assert.Same(stdoutWriterValue, stderrWriterValue);

            // Verify Create was invoked only once for the shared multiplexed stream
            ctx.StreamWriterFactory.Verify(
                f => f.Create("shared-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public void OnProcessExited_LogsExitInfo()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build();
            _disposableServices.Add(service);

            TestReflection.SetField(service, "_options", CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(0);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Child process exited successfully (Code 0).")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void OnProcessExited_ExitCodeNonZero_LogsError()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build();
            _disposableServices.Add(service);

            TestReflection.SetField(service, "_options", CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(42);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            ctx.Logger.Verify(l => l.Error("Process exited with code 42 and recovery is disabled.", It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void OnProcessExited_ExitCodeThrowsException_LogsWarning()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build();
            _disposableServices.Add(service);

            TestReflection.SetField(service, "_options", CreateDefaultStartOptions());

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Throws(new InvalidOperationException("boom"));
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Failed to get exit code")), It.IsAny<Exception>()), Times.Once);
        }

        public void Dispose()
        {
            // Unified Cleanup: Iterate and safely drop transient test services to avoid CTS leaks
            foreach (var service in _disposableServices)
            {
                service?.Dispose();
            }
        }
    }
}
