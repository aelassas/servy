using Servy.Core.Resources;
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

        #region Recursive Dependency Resolution Tests

        [Fact]
        public void GetDependencies_ValidWindowsService_ResolvesDependencyTreeCleanly()
        {
            // Arrange
            var wrapper = new ServiceControllerWrapper(StandardTestService);

            // Act
            var rootNode = wrapper.GetDependencies(cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(rootNode);
            Assert.Equal(StandardTestService, rootNode.ServiceName);
            Assert.False(rootNode.IsCyclic);

            // Dependencies collection must verify accurate structural sorting parameters
            if (rootNode.Dependencies.Count > 1)
            {
                for (int i = 0; i < rootNode.Dependencies.Count - 1; i++)
                {
                    var current = rootNode.Dependencies[i].DisplayName;
                    var next = rootNode.Dependencies[i + 1].DisplayName;
                    Assert.True(string.Compare(current, next, StringComparison.OrdinalIgnoreCase) <= 0,
                        $"Dependencies are incorrectly ordered: '{current}' appeared before '{next}'");
                }
            }
        }

        [Fact]
        public void GetDependencies_CancellationRequested_AbortsExecutionAndThrows()
        {
            // Arrange
            var wrapper = new ServiceControllerWrapper(StandardTestService);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                Assert.Throws<OperationCanceledException>(() => wrapper.GetDependencies(cts.Token));
            }
        }

        [Fact]
        public void GetDependencies_NonExistentService_ReturnsGracefulUnavailableNode()
        {
            // Arrange
            string phantomService = $"PhantomService_{Guid.NewGuid()}";
            var wrapper = new ServiceControllerWrapper(phantomService);

            // Act
            var result = wrapper.GetDependencies(cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(phantomService, result.ServiceName);

            // Check that the fallback string template hydration matches internal catch definitions
            string expectedErrorMessage = string.Format(Strings.Msg_DependencyUnavailable, phantomService);
            Assert.Equal(expectedErrorMessage, result.DisplayName);
            Assert.False(result.IsRunning);
            Assert.False(result.IsCyclic);
        }

        #endregion
    }
}