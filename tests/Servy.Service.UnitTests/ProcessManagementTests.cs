using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace Servy.Service.UnitTests
{
    public class ProcessManagementTests : IDisposable
    {
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        [Fact]
        public void StartProcess_StartsProcess()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(123);
            mockProcess.Setup(p => p.Start()).Returns(true);

            ProcessStartInfo? seenPsi = null;
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Callback<ProcessStartInfo, IServyLogger>((psi, _) => seenPsi = psi)
                .Returns(mockProcess.Object);

            // Act
            service.InvokeStartProcess("C:\\myapp.exe", "--arg", "C:\\workdir", new List<EnvironmentVariable>(), TestContext.Current.CancellationToken);

            // Assert
            var childProcess = service.GetChildProcess();
            Assert.Equal(mockProcess.Object, childProcess);
            mockProcess.Verify(p => p.Start(), Times.Once);

            // Verify ProcessStartInfo propagation
            Assert.NotNull(seenPsi);
            Assert.Equal("C:\\myapp.exe", seenPsi.FileName);
            Assert.Equal("--arg", seenPsi.Arguments);
            Assert.Equal("C:\\workdir", seenPsi.WorkingDirectory);

            // Verify event handler wiring
            mockProcess.VerifySet(p => p.EnableRaisingEvents = true, Times.Once);
            mockProcess.VerifyAdd(p => p.OutputDataReceived += It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyAdd(p => p.ErrorDataReceived += It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyAdd(p => p.Exited += It.IsAny<EventHandler>(), Times.Once);

            // Verify asynchronous stream reading calls
            mockProcess.Verify(p => p.BeginOutputReadLine(), Times.Once);
            mockProcess.Verify(p => p.BeginErrorReadLine(), Times.Once);
        }

        [Fact]
        public void StartProcess_StartFails_LogsCleansUpAndRethrows()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Start()).Throws(new Win32Exception(2)); // file not found

            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(mockProcess.Object);

            // Act
            var ex = Assert.Throws<TargetInvocationException>(() =>
                service.InvokeStartProcess("C:\\missing.exe", "", "C:\\", new List<EnvironmentVariable>(), TestContext.Current.CancellationToken));

            // Assert: the failure is rethrown to the caller so OnStart can signal the SCM
            Assert.IsType<Win32Exception>(ex.InnerException);

            // The single source of truth for start-up failure logging
            _ctx.Logger.Verify(l => l.Error(
                It.Is<string>(s => s.StartsWith("Failed to start process")), It.IsAny<Exception>()), Times.Once);

            // CleanupFailedProcess detaches every handler attached before Start()
            mockProcess.VerifyRemove(p => p.OutputDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyRemove(p => p.ErrorDataReceived -= It.IsAny<DataReceivedEventHandler>(), Times.Once);
            mockProcess.VerifyRemove(p => p.Exited -= It.IsAny<EventHandler>(), Times.Once);

            // ... disposes the failed wrapper and clears the field
            mockProcess.Verify(p => p.Dispose(), Times.Once);
            Assert.Null(service.GetChildProcess());

            // ... and the stream pumps are never started for a process that did not start
            mockProcess.Verify(p => p.BeginOutputReadLine(), Times.Never);
            mockProcess.Verify(p => p.BeginErrorReadLine(), Times.Never);
        }

        [Fact]
        public void StartProcess_StartFailsAndDisposeThrows_WarnsAndStillClearsChildProcess()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Start()).Throws(new Win32Exception(5)); // access denied
            mockProcess.Setup(p => p.Dispose()).Throws(new InvalidOperationException("dispose failed"));

            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Returns(mockProcess.Object);

            // Act
            var ex = Assert.Throws<TargetInvocationException>(() =>
                service.InvokeStartProcess("C:\\denied.exe", "", "C:\\", new List<EnvironmentVariable>(), TestContext.Current.CancellationToken));

            // Assert: the secondary failure is downgraded to a warning and never masks the original one
            Assert.IsType<Win32Exception>(ex.InnerException);
            _ctx.Logger.Verify(l => l.Warn(
                It.Is<string>(s => s.StartsWith("Secondary error during failed process cleanup")), It.IsAny<Exception>()), Times.Once);

            // The finally arm clears the field even when disposal threw
            Assert.Null(service.GetChildProcess());
        }

        [Fact]
        public void SafeKillProcess_KillsProcessGracefully()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert
            mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
            _ctx.Logger.Verify(l => l.Info(
                It.Is<string>(s => s.Contains("stopped gracefully")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SafeKillProcess_LogsErrorOnException()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Throws(new Exception("Boom!"));

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object, TestTimeouts.ProcessWrapperProcessTimeoutMs);

            // Assert
            _ctx.Logger.Verify(l => l.Error("SafeKillProcess background task failed: Boom!", It.IsAny<Exception>()), Times.Once);
        }

        public void Dispose() => _ctx.Dispose();
    }
}
