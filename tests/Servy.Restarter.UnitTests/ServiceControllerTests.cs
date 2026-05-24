using System;
using System.ServiceProcess;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    public class ServiceControllerTests
    {
        private const string DummyServiceName = "NonExistentServiceForTesting";

        [Fact]
        public void Constructor_ShouldInitializeWithoutThrowing()
        {
            // The constructor just assigns the new instance, it doesn't 
            // validate the service existence until a method is called.
            var controller = new ServiceController(DummyServiceName);
            Assert.NotNull(controller);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            var controller = new ServiceController(DummyServiceName);

            // Should be idempotent
            controller.Dispose();
            var exception = Record.Exception(controller.Dispose);

            Assert.Null(exception);
        }

        [Theory]
        [InlineData("Start")]
        [InlineData("Stop")]
        [InlineData("Refresh")]
        [InlineData("Status")]
        [InlineData("WaitForStatus")]
        public void Methods_WhenDisposed_ThrowObjectDisposedException(string methodName)
        {
            // Arrange
            var controller = new ServiceController(DummyServiceName);
            controller.Dispose();

            // Act & Assert
            switch (methodName)
            {
                case "Start":
                    Assert.Throws<ObjectDisposedException>(controller.Start);
                    break;
                case "Stop":
                    Assert.Throws<ObjectDisposedException>(controller.Stop);
                    break;
                case "Refresh":
                    Assert.Throws<ObjectDisposedException>(controller.Refresh);
                    break;
                case "Status":
                    Assert.Throws<ObjectDisposedException>(() => { var s = controller.Status; });
                    break;
                case "WaitForStatus":
                    Assert.Throws<ObjectDisposedException>(() =>
                        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(1)));
                    break;
            }
        }
    }
}