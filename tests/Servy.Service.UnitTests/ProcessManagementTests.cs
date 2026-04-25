using Moq;
using Servy.Core.Data;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
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
        private readonly Mock<IProcessHelper> _mockProcessHelper;

        public ProcessManagementTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
        }

        private TestableService CreateService(
            out Mock<IServyLogger> mockLogger,
            out Mock<IServiceHelper> mockHelper,
            out Mock<IStreamWriterFactory> mockStreamWriterFactory,
            out Mock<ITimerFactory> mockTimerFactory,
            out Mock<IProcessFactory> mockProcessFactory,
            out Mock<IPathValidator> mockPathValidator,
            out Mock<IServiceRepository> mockServiceRepository)
        {
            mockLogger = new Mock<IServyLogger>();
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
                mockServiceRepository.Object,
                _mockProcessHelper.Object
                );
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
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(123);
            mockProcess.Setup(p => p.Start()).Returns(true);

            processFactory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>())).Returns(mockProcess.Object);

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
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.HasExited).Returns(false);
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);
            mockProcess.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);

            service.InvokeSafeKillProcess(mockProcess.Object);

            mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);

            logger.Verify(l => l.Info(It.IsAny<string>()), Times.AtLeast(1));
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
                out var pathValidator,
                out var serviceRepository);

            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Throws(new Exception("Boom!"));

            service.InvokeSafeKillProcess(mockProcess.Object);

            logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("SafeKillProcess error"))), Times.Once);
        }
    }
}
