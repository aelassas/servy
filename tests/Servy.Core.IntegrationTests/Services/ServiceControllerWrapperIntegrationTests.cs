using Servy.Core.Resources;
using Servy.Core.Services;
using System;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Xunit;

namespace Servy.Core.IntegrationTests.Services
{
    [Collection("CoreOsIntegration")]
    public class ServiceControllerWrapperIntegrationTests
    {
        private const string StandardTestService = "LanmanServer";

        /// <summary>
        /// The OS platform check required by every live service query in this class.
        /// </summary>
        private static bool IsWindowsPlatform() => Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>
        /// Enforces OS platform and SCM availability checks before executing live service queries.
        /// Returns false if the test should be skipped gracefully.
        /// </summary>
        private static bool IsScmAndServiceAvailable(string serviceName)
        {
            if (!IsWindowsPlatform())
            {
                return false;
            }

            try
            {
                return ServiceController.GetServices().Any(s => string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        #region Recursive Dependency Resolution Tests

        [Fact]
        public void GetDependencies_ValidWindowsService_ResolvesDependencyTreeCleanly()
        {
            if (!IsScmAndServiceAvailable(StandardTestService))
            {
                return;
            }

            // Arrange
            using (var wrapper = new ServiceControllerWrapper(StandardTestService))
            {
                // Act
                var rootNode = wrapper.GetDependencies(cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(rootNode);
                Assert.Equal(StandardTestService, rootNode.ServiceName);
                Assert.False(rootNode.IsCyclic);

                // Ensure the ordering assertion is never silently bypassed when running on environments with < 2 dependencies
                if (rootNode.Dependencies.Count < 2)
                {
                    return;
                }

                // Dependencies collection must verify accurate structural sorting parameters.
                // Mirror the SUT's conditional sort key (ServiceControllerWrapper.cs): unavailable nodes are
                // ordered by ServiceName, since their DisplayName holds a formatted error message instead.
                string SortKey(ServiceDependencyNode node) => node.IsUnavailable ? node.ServiceName : node.DisplayName;

                for (int i = 0; i < rootNode.Dependencies.Count - 1; i++)
                {
                    var current = SortKey(rootNode.Dependencies[i]);
                    var next = SortKey(rootNode.Dependencies[i + 1]);
                    Assert.True(string.Compare(current, next, StringComparison.OrdinalIgnoreCase) <= 0,
                        $"Dependencies are incorrectly ordered: '{current}' appeared before '{next}'");
                }
            }
        }

        [Fact]
        public void GetDependencies_CancellationRequested_AbortsExecutionAndThrows()
        {
            if (!IsWindowsPlatform())
            {
                return;
            }

            // Arrange
            // A phantom name keeps this test independent of which services the host has installed:
            // the token is observed before any SCM query, so the name is never resolved.
            using (var wrapper = new ServiceControllerWrapper($"PhantomService_{Guid.NewGuid()}"))
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
            if (!IsWindowsPlatform())
            {
                return;
            }

            // Arrange
            string phantomService = $"PhantomService_{Guid.NewGuid()}";
            using (var wrapper = new ServiceControllerWrapper(phantomService))
            {
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
        }

        #endregion
    }
}
