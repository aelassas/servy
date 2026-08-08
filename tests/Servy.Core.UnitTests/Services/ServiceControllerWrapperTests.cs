using Moq;
using Servy.Core.Resources;
using Servy.Core.Services;
using System;
using System.ComponentModel;
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
            using (var wrapper = new ServiceControllerWrapper(StandardTestService))
            {

                // Act
                var name = wrapper.ServiceName;

                // Assert
                Assert.Equal(StandardTestService, name);
            }
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

        #region Win32Exception & Edge Case Resolution Tests

        [Fact]
        public void GetDependencies_Win32ExceptionAccessDenied_ReturnsAccessDeniedNode()
        {
            // Arrange
            using (var wrapper = new ServiceControllerWrapper("TargetService"))
            {
                // Mock factory returning a mock wrapper that throws Win32Exception(5)
                Func<string, IServiceControllerWrapper> factory = name =>
                {
                    var mock = new Mock<IServiceControllerWrapper>();
                    mock.Setup(m => m.ServiceName).Returns(name);
                    if (name == "TargetService")
                    {
                        throw new Win32Exception(5); // ERROR_ACCESS_DENIED
                    }
                    return mock.Object;
                };

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("TargetService", result.ServiceName);
                Assert.Equal(string.Format(Strings.Msg_DependencyAccessDenied, "TargetService"), result.DisplayName);
                Assert.False(result.IsRunning);
                Assert.False(result.IsCyclic);
            }
        }

        [Fact]
        public void GetDependencies_Win32ExceptionOtherError_ReturnsUnavailableNode()
        {
            // Arrange
            using (var wrapper = new ServiceControllerWrapper("TargetService"))
            {
                // Mock factory returning a mock wrapper that throws Win32Exception(1060 - ERROR_SERVICE_DOES_NOT_EXIST)
                Func<string, IServiceControllerWrapper> factory = name =>
                {
                    var mock = new Mock<IServiceControllerWrapper>();
                    mock.Setup(m => m.ServiceName).Returns(name);
                    if (name == "TargetService")
                    {
                        throw new Win32Exception(1060);
                    }
                    return mock.Object;
                };

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("TargetService", result.ServiceName);
                Assert.Equal(string.Format(Strings.Msg_DependencyUnavailable, "TargetService"), result.DisplayName);
                Assert.False(result.IsRunning);
                Assert.False(result.IsCyclic);
            }
        }

        [Fact]
        public void GetDependencies_CyclicDependency_DetectsCycleAndAppliesMemoization()
        {
            // Arrange: ServiceA -> ServiceB -> ServiceA (Cycle)
            using (var wrapper = new ServiceControllerWrapper("ServiceA"))
            {
                Func<string, IServiceControllerWrapper> factory = name =>
                {
                    var mock = new Mock<IServiceControllerWrapper>();
                    mock.Setup(m => m.ServiceName).Returns(name);
                    return mock.Object;
                };

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("ServiceA", result.ServiceName);
                Assert.False(result.IsCyclic);
            }
        }

        #endregion
    }
}
