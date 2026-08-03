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
        /// Enforces OS platform and SCM availability checks before executing live service queries.
        /// </summary>
        private static void SkipUnlessScmAndServiceAvailable(string serviceName)
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Live SCM dependency resolution requires Windows OS.");

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
            var wrapper = new ServiceControllerWrapper(StandardTestService);

            // Act
            var rootNode = wrapper.GetDependencies(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(rootNode);
            Assert.Equal(StandardTestService, rootNode.ServiceName);
            Assert.False(rootNode.IsCyclic);

            // Ensure the ordering assertion is never silently bypassed when running on environments with < 2 dependencies
            Assert.SkipWhen(rootNode.Dependencies.Count < 2,
                $"'{StandardTestService}' resolved {rootNode.Dependencies.Count} dependencies on this host; alphabetical ordering evaluation requires at least 2.");

            // Dependencies collection must verify accurate structural sorting parameters
            for (int i = 0; i < rootNode.Dependencies.Count - 1; i++)
            {
                var current = rootNode.Dependencies[i].DisplayName;
                var next = rootNode.Dependencies[i + 1].DisplayName;
                Assert.True(string.Compare(current, next, StringComparison.OrdinalIgnoreCase) <= 0,
                    $"Dependencies are incorrectly ordered: '{current}' appeared before '{next}'");
            }
        }

        [Fact]
        public void GetDependencies_CancellationRequested_AbortsExecutionAndThrows()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Live SCM dependency resolution requires Windows OS.");

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
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Live SCM dependency resolution requires Windows OS.");

            // Arrange
            string phantomService = $"PhantomService_{Guid.NewGuid()}";
            var wrapper = new ServiceControllerWrapper(phantomService);

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

        #endregion
    }
}