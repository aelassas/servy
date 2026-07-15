using Moq;
using Servy.Core.Enums;
using Servy.Core.Helpers;
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
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly List<IDisposable> _disposableServices = new List<IDisposable>();

        public EventHandlerTests()
        {
            _mockProcessKiller = new Mock<IProcessKiller>();
        }

        [Fact]
        public void OnOutputDataReceived_WritesToRotatingWriters_IgnoresNullOrEmpty()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service); // LEAK FIX: Track SUT instance for teardown disposal

            var mockWriter = new Mock<IStreamWriter>();
            ctx.StreamWriterFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>())).Returns(mockWriter.Object);

            var nonEmptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("output line");
            var emptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(null!);
            var emptyStringArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(string.Empty);

            var startOptions = new StartOptions
            {
                StdoutPath = "valid-path.log",
                StderrPath = "error-path.log",
                RotationSizeInBytes = 1024 * 1024
            };

            service.InvokeHandleLogWriters(startOptions);

            var stdoutWriterValue = TestReflection.GetField<object>(service, "_stdoutWriter");
            Assert.NotNull(stdoutWriterValue);

            // Act
            service.InvokeOnOutputDataReceived(null, nonEmptyArgs);
            service.InvokeOnOutputDataReceived(null, emptyArgs);
            service.InvokeOnOutputDataReceived(null, emptyStringArgs);

            // Assert
            // 1. Verify the non-empty line was written exactly once
            mockWriter.Verify(w => w.WriteLine("output line"), Times.Once);

            // 2. Verify that blank lines (empty strings) are written to preserve log formatting
            mockWriter.Verify(w => w.WriteLine(string.Empty), Times.Once);

            // 3. Verify that the stream-end sentinel (null) is ignored completely
            mockWriter.Verify(w => w.WriteLine(null!), Times.Never);
        }

        [Fact]
        public void OnErrorDataReceived_WritesToRotatingWriters_IgnoresNullOrEmpty()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

            var mockWriter = new Mock<IStreamWriter>();
            ctx.StreamWriterFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>())).Returns(mockWriter.Object);

            var nonEmptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("error line");
            var emptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(null!);
            var emptyStringArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(string.Empty);

            var startOptions = new StartOptions
            {
                StdoutPath = "valid-path.log",
                StderrPath = "error-path.log",
                RotationSizeInBytes = 1024 * 1024
            };

            service.InvokeHandleLogWriters(startOptions);

            // Symmetry Verification: Assert the private _stderrWriter field was populated via reflection
            var stderrWriterValue = TestReflection.GetField<object>(service, "_stderrWriter");
            Assert.NotNull(stderrWriterValue);

            // Act
            service.InvokeOnErrorDataReceived(null, nonEmptyArgs);
            service.InvokeOnErrorDataReceived(null, emptyArgs);
            service.InvokeOnErrorDataReceived(null, emptyStringArgs);

            // Assert
            // 1. Verify the non-empty line was written exactly once
            mockWriter.Verify(w => w.WriteLine("error line"), Times.Once);

            // 2. Verify that blank lines (empty strings) are written to preserve log formatting
            mockWriter.Verify(w => w.WriteLine(string.Empty), Times.Once);

            // 3. Verify that the stream-end sentinel (null) is ignored completely
            mockWriter.Verify(w => w.WriteLine(null!), Times.Never);
        }

        [Fact]
        public void OnProcessExited_LogsExitInfo()
        {
            // Arrange
            var ctx = new ServiceTestContext();
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

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
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

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
            var service = ctx.Build(_mockProcessKiller.Object);
            _disposableServices.Add(service);

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