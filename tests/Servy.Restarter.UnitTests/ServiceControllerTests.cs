using Servy.Testing;
using System;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    public class ServiceControllerTests
    {
        private const string DummyServiceName = "NonExistentServiceForTesting";

        [Fact]
        public void Constructor_ShouldInitializeWithoutThrowing()
        {
            // Arrange & Act
            var exception = Record.Exception(() => new ServiceController(DummyServiceName));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_FirstInvocation_SetsDisposedFlagAndCleansUpState()
        {
            // Arrange
            var controller = new ServiceController(DummyServiceName);

            // Act: Assert lifecycle field mutations using the centralized reflection infrastructure
            bool isDisposedBefore = TestReflection.GetField<bool>(controller, "_disposed");
            Assert.False(isDisposedBefore, "The service controller wrapper should not initialize in a pre-disposed state.");

            controller.Dispose();

            // Assert
            bool isDisposedAfter = TestReflection.GetField<bool>(controller, "_disposed");
            Assert.True(isDisposedAfter, "The internal _disposed state guard was not toggled to true during clean teardown execution.");
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShortCircuitsSafelyViaGuard()
        {
            // Arrange
            var controller = new ServiceController(DummyServiceName);

            // Execute the initial disposal loop to alter core backing states
            controller.Dispose();
            Assert.True(TestReflection.GetField<bool>(controller, "_disposed"));

            // Act
            // Manually override the state flag back to false using TestReflection to verify short-circuit behavior re-entry paths 
            TestReflection.SetField(controller, "_disposed", false);

            var exception = Record.Exception(() => controller.Dispose());

            // Assert
            Assert.Null(exception);
            Assert.True(TestReflection.GetField<bool>(controller, "_disposed"), "The state engine failed to toggle back to an active disposed layout configuration.");
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
                        controller.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(1)));
                    break;
                default:
                    Assert.Fail($"Unhandled method name '{methodName}' in test case. Please ensure the test data is up to date with the ServiceController methods.");
                    break;
            }
        }
    }
}