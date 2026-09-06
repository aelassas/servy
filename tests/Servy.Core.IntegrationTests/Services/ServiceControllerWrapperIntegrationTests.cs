using Servy.Core.Resources;
using Servy.Core.Services;
using System.ServiceProcess;

namespace Servy.Core.IntegrationTests.Services
{
    [Collection("CoreOsIntegration")]
    public class ServiceControllerWrapperIntegrationTests
    {
        private const string StandardTestService = "LanmanServer";

        /// <summary>
        /// Enforces the OS platform check required by every live service query in this class.
        /// </summary>
        private static void SkipUnlessWindows() =>
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Live SCM dependency resolution requires Windows OS.");

        /// <summary>
        /// Enforces OS platform and SCM availability checks before executing live service queries.
        /// </summary>
        private static void SkipUnlessScmAndServiceAvailable(string serviceName)
        {
            SkipUnlessWindows();

            bool isInstalled = false;
            try
            {
                isInstalled = ServiceController.GetServices().Any(s => string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                Assert.Skip("Service Control Manager access failed or restricted on this host.");
            }

            Assert.SkipUnless(isInstalled, $"Target test service '{serviceName}' is not installed on this host.");
        }

        #region Recursive Dependency Resolution Tests

        [Fact]
        public void GetDependencies_ValidWindowsService_ResolvesDependencyTreeCleanly()
        {
            SkipUnlessScmAndServiceAvailable(StandardTestService);

            // Arrange
            using (var wrapper = new ServiceControllerWrapper(StandardTestService))
            {
                // Act
                var rootNode = wrapper.GetDependencies(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.NotNull(rootNode);
                Assert.Equal(StandardTestService, rootNode.ServiceName);
                Assert.False(rootNode.IsCyclic);

                // Ensure the ordering assertion is never silently bypassed when running on environments with < 2 dependencies
                Assert.SkipWhen(rootNode.Dependencies.Count < 2,
                    $"'{StandardTestService}' resolved {rootNode.Dependencies.Count} dependencies on this host; alphabetical ordering evaluation requires at least 2.");

                // Dependencies collection must verify accurate structural sorting parameters.
                // Mirror the SUT's conditional sort key (ServiceControllerWrapper.cs): unavailable nodes are
                // ordered by ServiceName, since their DisplayName holds a formatted error message instead.
                static string SortKey(ServiceDependencyNode node) => node.IsUnavailable ? node.ServiceName : node.DisplayName;

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
            SkipUnlessWindows();

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
            SkipUnlessWindows();

            // Arrange
            string phantomService = $"PhantomService_{Guid.NewGuid()}";
            using (var wrapper = new ServiceControllerWrapper(phantomService))
            {
                // Act
                var result = wrapper.GetDependencies(cancellationToken: TestContext.Current.CancellationToken);

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
