using Moq;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.UnitTests.Helpers;
using Servy.Testing;
using System.Diagnostics;

namespace Servy.Service.UnitTests
{
    public class EventHandlerTests : IDisposable
    {
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        [Fact]
        public void OnOutputDataReceived_WritesToRotatingWriters_IgnoresNullOrEmpty()
        {
            // Arrange
            var service = _ctx.Build();

            var mockStdoutWriter = new Mock<IStreamWriter>();
            var mockStderrWriter = new Mock<IStreamWriter>();

            _ctx.StreamWriterFactory
               .Setup(f => f.Create("valid-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStdoutWriter.Object);

            _ctx.StreamWriterFactory
               .Setup(f => f.Create("error-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStderrWriter.Object);

            var nonEmptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("output line");
            var emptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(null);
            var emptyStringArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(string.Empty);

            var startOptions = ServiceTestContext.CreateDefaultStartOptions();
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
            var service = _ctx.Build();

            var mockStdoutWriter = new Mock<IStreamWriter>();
            var mockStderrWriter = new Mock<IStreamWriter>();

            _ctx.StreamWriterFactory
               .Setup(f => f.Create("valid-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStdoutWriter.Object);

            _ctx.StreamWriterFactory
               .Setup(f => f.Create("error-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStderrWriter.Object);

            var nonEmptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("error line");
            var emptyArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(null);
            var emptyStringArgs = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs(string.Empty);

            var startOptions = ServiceTestContext.CreateDefaultStartOptions();
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
            var service = _ctx.Build();

            var mockSharedWriter = new Mock<IStreamWriter>();

            _ctx.StreamWriterFactory
               .Setup(f => f.Create("shared-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockSharedWriter.Object);

            var startOptions = ServiceTestContext.CreateDefaultStartOptions();
            startOptions.StdoutPath = "shared-path.log";
            // A different spelling of the same file: the multiplex decision is made on the
            // canonicalized paths (Helper.NormalizePath), so a raw string comparison misses it.
            startOptions.StderrPath = Path.Combine(".", "shared-path.log");

            // Act
            service.InvokeHandleLogWriters(startOptions);

            var stdoutWriterValue = TestReflection.GetField<object>(service, "_stdoutWriter");
            var stderrWriterValue = TestReflection.GetField<object>(service, "_stderrWriter");

            // Assert
            Assert.NotNull(stdoutWriterValue);
            Assert.NotNull(stderrWriterValue);
            Assert.Same(stdoutWriterValue, stderrWriterValue);

            // Verify Create was invoked only once for the shared multiplexed stream
            _ctx.StreamWriterFactory.Verify(
                f => f.Create("shared-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public void OnProcessExited_LogsExitInfo()
        {
            // Arrange
            var service = _ctx.Build();

            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.FailureProgramPath = @"C:\App\alert.exe";
            TestReflection.SetField(service, "_options", options);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(0);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Child process exited successfully (Code 0).")), It.IsAny<Exception>()), Times.Once);

            // Verify clean exit does not launch failure program
            _ctx.ProcessFactory.Verify(f => f.Create(
                It.Is<ProcessStartInfo>(psi => psi.FileName == @"C:\App\alert.exe"), It.IsAny<IServyLogger>()), Times.Never);
        }

        [Fact]
        public void OnProcessExited_ExitCodeNonZero_LogsErrorAndLaunchesFailureProgram()
        {
            // Arrange
            var service = _ctx.Build();

            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.FailureProgramPath = @"C:\App\alert.exe";
            TestReflection.SetField(service, "_options", options);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Returns(42);
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.Logger.Verify(l => l.Error(
                    "[OnProcessExited] Process exited with code 42 (0x0000002A) and recovery is disabled.",
                    It.IsAny<Exception>()), Times.Once);

            // Verify non-zero exit code launches configured failure program
            _ctx.ProcessFactory.Verify(f => f.Create(
                It.Is<ProcessStartInfo>(psi => psi.FileName == @"C:\App\alert.exe"), It.IsAny<IServyLogger>()), Times.Once);
        }

        [Fact]
        public void OnProcessExited_ExitCodeThrowsException_LogsWarning()
        {
            // Arrange
            var service = _ctx.Build();

            var options = ServiceTestContext.CreateDefaultStartOptions();
            options.FailureProgramPath = @"C:\App\alert.exe";
            TestReflection.SetField(service, "_options", options);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.ExitCode).Throws(new InvalidOperationException("boom"));
            service.SetChildProcess(mockProcess.Object);

            // Act
            service.InvokeOnProcessExited(null, EventArgs.Empty);

            // Assert
            _ctx.Logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Failed to get exit code")), It.IsAny<Exception>()), Times.Once);

            // An unreadable exit code falls through as -1, so the recovery-disabled error carries that fallback value
            _ctx.Logger.Verify(l => l.Error(
                    "[OnProcessExited] Process exited with code -1 (0xFFFFFFFF) and recovery is disabled.",
                    It.IsAny<Exception>()), Times.Once);

            // Verify the same -1 drives the stop sequence, which launches the configured failure program
            _ctx.ProcessFactory.Verify(f => f.Create(
                It.Is<ProcessStartInfo>(psi => psi.FileName == @"C:\App\alert.exe"), It.IsAny<IServyLogger>()), Times.Once);
        }

        [Fact]
        public void HandleLogWriters_StdoutFactoryThrows_LogsErrorAndStillCreatesTheStderrWriter()
        {
            // Arrange
            var service = _ctx.Build();

            var ioFailure = new IOException("The network path was not found.");
            var mockStderrWriter = new Mock<IStreamWriter>();

            _ctx.StreamWriterFactory
               .Setup(f => f.Create("valid-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Throws(ioFailure);

            _ctx.StreamWriterFactory
               .Setup(f => f.Create("error-path.log", It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockStderrWriter.Object);

            var startOptions = ServiceTestContext.CreateDefaultStartOptions();

            // Act
            service.InvokeHandleLogWriters(startOptions);

            // Assert
            // The failing stream is logged with its path and the original exception, then dropped.
            _ctx.Logger.Verify(l => l.Error(
                "Could not open log file 'valid-path.log'; continuing without redirection for this stream.",
                ioFailure), Times.Once);
            Assert.Null(TestReflection.GetField<object>(service, "_stdoutWriter"));

            // A redirection failure on one stream never stops the service: stderr still gets its own writer.
            Assert.Same(mockStderrWriter.Object, TestReflection.GetField<object>(service, "_stderrWriter"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnDataReceived_WriterThrowsIOException_LogsWarningAndDoesNotThrow(bool stdout)
        {
            // Arrange
            var service = _ctx.Build();

            var mockWriter = new Mock<IStreamWriter>();
            mockWriter.Setup(w => w.WriteLine(It.IsAny<string>())).Throws(new IOException("disk full"));

            _ctx.StreamWriterFactory
               .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockWriter.Object);

            service.InvokeHandleLogWriters(ServiceTestContext.CreateDefaultStartOptions());

            var args = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("line");

            // Act
            var ex = Record.Exception(() =>
            {
                if (stdout) service.InvokeOnOutputDataReceived(null, args);
                else service.InvokeOnErrorDataReceived(null, args);
            });

            // Assert
            // The output pump threads must never let a write failure escape; it is logged and swallowed.
            Assert.Null(ex);
            _ctx.Logger.Verify(l => l.Warn(
                stdout ? "Failed to write stdout line: disk full" : "Failed to write stderr line: disk full",
                It.IsAny<Exception>()), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnDataReceived_WriterAlreadyDisposed_IsSilent(bool stdout)
        {
            // Arrange
            var service = _ctx.Build();

            var mockWriter = new Mock<IStreamWriter>();
            mockWriter.Setup(w => w.WriteLine(It.IsAny<string>())).Throws(new ObjectDisposedException("writer"));

            _ctx.StreamWriterFactory
               .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
               .Returns(mockWriter.Object);

            service.InvokeHandleLogWriters(ServiceTestContext.CreateDefaultStartOptions());

            var args = DataReceivedEventArgsFactory.CreateDataReceivedEventArgs("line");

            // Act
            var ex = Record.Exception(() =>
            {
                if (stdout) service.InvokeOnOutputDataReceived(null, args);
                else service.InvokeOnErrorDataReceived(null, args);
            });

            // Assert
            // Shutting down: the dedicated ObjectDisposedException arm must not fall through to the Warn arm.
            Assert.Null(ex);
            mockWriter.Verify(w => w.WriteLine("line"), Times.Once);
            _ctx.Logger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        public void Dispose() => _ctx.Dispose();
    }
}
