using Moq;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using Servy.Service.UnitTests.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace Servy.Service.UnitTests
{
    public class ProcessManagementTests
    {
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public ProcessManagementTests()
        {
            _mockProcessKiller = new Mock<IProcessKiller>();
        }

        [Fact]
        public void StartProcess_StartsProcess()
        {
            // Arrange
            var ctx = new ServiceTestContext();

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.Id).Returns(123);
                mockProcess.Setup(p => p.Start()).Returns(true);

                ctx.ProcessFactory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>())).Returns(mockProcess.Object);

                // Act
                service.InvokeStartProcess("C:\\myapp.exe", "--arg", "C:\\workdir", new List<EnvironmentVariable>(), CancellationToken.None);

                // Assert
                var childProcess = service.GetChildProcess();
                Assert.NotNull(childProcess);
                Assert.Equal(mockProcess.Object, childProcess);
            }
        }

        [Fact]
        public void SafeKillProcess_KillsProcessGracefully()
        {
            // Arrange
            var ctx = new ServiceTestContext();

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.HasExited).Returns(false);
                mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Returns(true);

                // Act
                service.InvokeSafeKillProcess(mockProcess.Object);

                // Assert
                mockProcess.Verify(p => p.Stop(It.IsAny<int>()), Times.Once);
                ctx.Logger.Verify(l => l.Info(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeast(1));
            }
        }

        [Fact]
        public void SafeKillProcess_LogsErrorOnException()
        {
            // Arrange
            var ctx = new ServiceTestContext();

            using (var service = ctx.Build(_mockProcessKiller.Object))
            {
                var mockProcess = new Mock<IProcessWrapper>();
                mockProcess.Setup(p => p.Stop(It.IsAny<int>())).Throws(new Exception("Boom!"));

                // Act
                service.InvokeSafeKillProcess(mockProcess.Object);

                // Assert
                ctx.Logger.Verify(l => l.Error(It.Is<string>(s => s.Equals("SafeKillProcess background task failed: Boom!")), It.IsAny<Exception>()), Times.Once);
            }
        }
    }
}