using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xunit;

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

            ProcessStartInfo seenPsi = null;
            _ctx.ProcessFactory
                .Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>()))
                .Callback<ProcessStartInfo, IServyLogger>((psi, _) => seenPsi = psi)
                .Returns(mockProcess.Object);

            // Act
            service.InvokeStartProcess("C:\\myapp.exe", "--arg", "C:\\workdir", new List<EnvironmentVariable>(), CancellationToken.None);

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
        public void SafeKillProcess_KillsProcessGracefully()
        {
            // Arrange
            var service = _ctx.Build();

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

            // Act
            service.InvokeSafeKillProcess(mockProcess.Object);

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
            service.InvokeSafeKillProcess(mockProcess.Object);

            // Assert
            _ctx.Logger.Verify(l => l.Error("SafeKillProcess background task failed: Boom!", It.IsAny<Exception>()), Times.Once);
        }

        public void Dispose() => _ctx.Dispose();
    }
}
