using Servy.Core.Services;
using System;
using System.Threading;
using Xunit;

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
            Assert.Throws<ObjectDisposedException>(() => wrapper.GetDependencies(cancellationToken: CancellationToken.None));
        }

        #endregion
    }
}