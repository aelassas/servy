using System;
using System.ServiceProcess;
using Moq;
using Xunit;
using Servy.Restarter;

namespace Servy.Restarter.UnitTests
{
    public class ServiceRestarterTests
    {
        private readonly Mock<IServiceController> _mockController;
        private readonly ServiceRestarter _restarter;

        public ServiceRestarterTests()
        {
            _mockController = new Mock<IServiceController>();

            // Inject factory returning the mock controller
            _restarter = new ServiceRestarter(name => _mockController.Object);
        }

        [Fact]
        public void RestartService_StopsAndStartsServiceSuccessfully()
        {
            // Arrange - set up mock behavior
            _mockController.Setup(c => c.WaitForStatus(ServiceControllerStatus.Stopped, It.IsAny<TimeSpan>()));
            _mockController.Setup(c => c.Start());
            _mockController.Setup(c => c.WaitForStatus(ServiceControllerStatus.Running, It.IsAny<TimeSpan>()));
            _mockController.Setup(c => c.Dispose());

            // Act
            _restarter.RestartService("MyService");

            // Assert - verify calls in correct order
            _mockController.Verify(c => c.WaitForStatus(ServiceControllerStatus.Stopped, It.IsAny<TimeSpan>()), Times.Once);
            _mockController.Verify(c => c.Start(), Times.Once);
            _mockController.Verify(c => c.WaitForStatus(ServiceControllerStatus.Running, It.IsAny<TimeSpan>()), Times.Once);
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void RestartService_WhenExceptionThrown_Throws()
        {
            _mockController.Setup(c => c.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Service failure"));

            Assert.Throws<Exception>(() => _restarter.RestartService("MyService"));

            _mockController.Verify(c => c.Dispose(), Times.Once);
        }
    }
}
