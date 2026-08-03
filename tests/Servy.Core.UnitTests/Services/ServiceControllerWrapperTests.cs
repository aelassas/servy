using Servy.Core.Services;

namespace Servy.Core.UnitTests.Services
{
    public class ServiceControllerWrapperTests
    {
        private const string StandardTestService = "LanmanServer";

        #region Lifecycle & Invariant Validation Tests

        [Fact]
        public void ServiceName_ValidState_ReturnsInitializedValue()
        {
            // Arrange
            var wrapper = new ServiceControllerWrapper(StandardTestService);

            // Act
            var name = wrapper.ServiceName;

            // Assert
            Assert.Equal(StandardTestService, name);
        }

        [Fact]
        public void InstanceMutations_PostDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var wrapper = new ServiceControllerWrapper(StandardTestService);
            wrapper.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => wrapper.ServiceName);
            Assert.Throws<ObjectDisposedException>(() => wrapper.GetDependencies(cancellationToken: TestContext.Current.CancellationToken));
        }

        #endregion
    }
}