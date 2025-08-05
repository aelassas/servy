using System;
using System.ServiceProcess;
using Moq;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    public class ServiceControllerTests
    {
        private readonly Mock<ISystemServiceController> _mockSystemController;
        private readonly ServiceController _serviceController;

        public ServiceControllerTests()
        {
            _mockSystemController = new Mock<ISystemServiceController>();
            _serviceController = new ServiceController(_mockSystemController.Object);
        }

        [Fact]
        public void Start_CallsStartOnSystemController()
        {
            _serviceController.Start();

            _mockSystemController.Verify(m => m.Start(), Times.Once);
        }

        [Fact]
        public void WaitForStatus_CallsWaitForStatusOnSystemController()
        {
            var status = ServiceControllerStatus.Running;
            var timeout = TimeSpan.FromSeconds(10);

            _serviceController.WaitForStatus(status, timeout);

            _mockSystemController.Verify(m => m.WaitForStatus(status, timeout), Times.Once);
        }

        [Fact]
        public void Status_ReturnsSystemControllerStatus()
        {
            _mockSystemController.Setup(m => m.Status).Returns(ServiceControllerStatus.Stopped);

            var result = _serviceController.Status;

            Assert.Equal(ServiceControllerStatus.Stopped, result);
        }

        [Fact]
        public void Dispose_CallsDisposeOnSystemController()
        {
            _serviceController.Dispose();

            _mockSystemController.Verify(m => m.Dispose(), Times.Once);
        }
    }
}
