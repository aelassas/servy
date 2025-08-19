using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using Servy.Service.ServiceHelpers;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace Servy.Service.UnitTests
{
    public class ProcessManagementTests
    {
        private static TestableService CreateService(
            out Mock<ILogger> mockLogger,
            out Mock<IServiceHelper> mockHelper,
            out Mock<IStreamWriterFactory> mockStreamWriterFactory,
            out Mock<ITimerFactory> mockTimerFactory,
            out Mock<IProcessFactory> mockProcessFactory,
            out Mock<IPathValidator> mockPathValidator)
        {
            mockLogger = new Mock<ILogger>();
            mockHelper = new Mock<IServiceHelper>();
            mockStreamWriterFactory = new Mock<IStreamWriterFactory>();
            mockTimerFactory = new Mock<ITimerFactory>();
            mockProcessFactory = new Mock<IProcessFactory>();
            mockPathValidator = new Mock<IPathValidator>();

            mockPathValidator.Setup(p => p.IsValidPath(It.IsAny<string>())).Returns(true);

            return new TestableService(
                mockHelper.Object,
                mockLogger.Object,
                mockStreamWriterFactory.Object,
                mockTimerFactory.Object,
                mockProcessFactory.Object,
                mockPathValidator.Object);
        }

        [Fact]
        public void StartProcess_StartsProcess()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(123);
            mockProcess.Setup(p => p.Start());

            processFactory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>())).Returns(mockProcess.Object);

            service.InvokeStartProcess("C:\\myapp.exe", "--arg", "C:\\workdir", new List<EnvironmentVariable>());

            var childProcess = service.GetChildProcess();
            Assert.NotNull(childProcess);
            Assert.Equal(mockProcess.Object, childProcess);
        }

        [Fact]
        public void SafeKillProcess_KillsProcessGracefully()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.MainWindowHandle).Returns(new IntPtr(123456));
            mockProcess.Setup(p => p.CloseMainWindow()).Returns(true);
            mockProcess.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);

            service.InvokeSafeKillProcess(mockProcess.Object);

            mockProcess.Verify(p => p.CloseMainWindow(), Times.Once);
            mockProcess.Verify(p => p.WaitForExit(It.IsAny<int>()), Times.Once);
            mockProcess.Verify(p => p.Kill(), Times.Never);

            logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SafeKillProcess_ForcesKillIfGracefulFails()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.MainWindowHandle).Returns(new IntPtr(123456));
            mockProcess.Setup(p => p.CloseMainWindow()).Returns(false);
            mockProcess.Setup(p => p.Kill());

            service.InvokeSafeKillProcess(mockProcess.Object);

            mockProcess.Verify(p => p.CloseMainWindow(), Times.Once);
            mockProcess.Verify(p => p.Kill(), Times.Once);

            logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void SafeKillProcess_LogsWarningOnException()
        {
            var service = CreateService(
                out var logger,
                out var helper,
                out var swFactory,
                out var timerFactory,
                out var processFactory,
                out var pathValidator);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.MainWindowHandle).Returns(new IntPtr(123456));
            mockProcess.Setup(p => p.CloseMainWindow()).Throws(new Exception("Exception during CloseMainWindow"));

            service.InvokeSafeKillProcess(mockProcess.Object);

            logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("SafeKillProcess error"))), Times.Once);
        }
    }
}
