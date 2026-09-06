using Moq;
using Servy.Core.Resources;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
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
        public void MemberAccess_PostDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var wrapper = new ServiceControllerWrapper(StandardTestService);
            wrapper.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => wrapper.ServiceName);
            Assert.Throws<ObjectDisposedException>(() => wrapper.DisplayName);
            Assert.Throws<ObjectDisposedException>(() => wrapper.Status);
            Assert.Throws<ObjectDisposedException>(() => wrapper.GetDependencyNames());
            Assert.Throws<ObjectDisposedException>(() => wrapper.GetDependencies(cancellationToken: CancellationToken.None));
        }

        #endregion

        #region Tree Resolution & Dependency Hierarchy Tests

        [Fact]
        public void GetDependencies_ValidHierarchy_BuildsFullDependencyTree()
        {
            // Arrange: ServiceA -> [ServiceB, ServiceC]; ServiceB -> [ServiceD]
            using (var wrapper = new ServiceControllerWrapper("ServiceA"))
            {
                var mockA = CreateMockWrapper("ServiceA", "Service A Display", ServiceControllerStatus.Running, new[] { "ServiceB", "ServiceC" });
                var mockB = CreateMockWrapper("ServiceB", "Service B Display", ServiceControllerStatus.Running, new[] { "ServiceD" });
                var mockC = CreateMockWrapper("ServiceC", "Service C Display", ServiceControllerStatus.Stopped, Array.Empty<string>());
                var mockD = CreateMockWrapper("ServiceD", "Service D Display", ServiceControllerStatus.Running, Array.Empty<string>());

                var mocks = new Dictionary<string, IServiceControllerWrapper>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ServiceA", mockA.Object },
                    { "ServiceB", mockB.Object },
                    { "ServiceC", mockC.Object },
                    { "ServiceD", mockD.Object }
                };

                // Act
                var result = wrapper.GetDependenciesInternal(name => mocks[name], CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("ServiceA", result.ServiceName);
                Assert.Equal("Service A Display", result.DisplayName);
                Assert.True(result.IsRunning);

                Assert.Equal(2, result.Dependencies.Count);
                Assert.Equal("ServiceB", result.Dependencies[0].ServiceName);
                Assert.Equal("ServiceC", result.Dependencies[1].ServiceName);

                // Nested leaf node verification
                var nodeB = result.Dependencies[0];
                Assert.Single(nodeB.Dependencies);
                Assert.Equal("ServiceD", nodeB.Dependencies[0].ServiceName);
            }
        }

        [Fact]
        public void GetDependencies_UnsortedDisplayNames_SortsChildrenAlphabeticallyByDisplayName()
        {
            // Arrange: Root depends on ServiceZ (Display: "Alpha Service") and ServiceA (Display: "Zulu Service")
            using (var wrapper = new ServiceControllerWrapper("RootService"))
            {
                var mockRoot = CreateMockWrapper("RootService", "Root Service", ServiceControllerStatus.Running, new[] { "ServiceZ", "ServiceA" });
                var mockZ = CreateMockWrapper("ServiceZ", "Alpha Service", ServiceControllerStatus.Running, Array.Empty<string>());
                var mockA = CreateMockWrapper("ServiceA", "Zulu Service", ServiceControllerStatus.Running, Array.Empty<string>());

                var mocks = new Dictionary<string, IServiceControllerWrapper>(StringComparer.OrdinalIgnoreCase)
                {
                    { "RootService", mockRoot.Object },
                    { "ServiceZ", mockZ.Object },
                    { "ServiceA", mockA.Object }
                };

                // Act
                var result = wrapper.GetDependenciesInternal(name => mocks[name], CancellationToken.None);

                // Assert: "Alpha Service" (ServiceZ) must precede "Zulu Service" (ServiceA)
                Assert.Equal(2, result.Dependencies.Count);
                Assert.Equal("Alpha Service", result.Dependencies[0].DisplayName);
                Assert.Equal("ServiceZ", result.Dependencies[0].ServiceName);
                Assert.Equal("Zulu Service", result.Dependencies[1].DisplayName);
                Assert.Equal("ServiceA", result.Dependencies[1].ServiceName);
            }
        }

        [Fact]
        public void GetDependencies_SharedDependency_ResolvesFromCache()
        {
            // Arrange: Root -> [ServiceB, ServiceC]; both depend on ServiceShared (diamond)
            using (var wrapper = new ServiceControllerWrapper("Root"))
            {
                var mockRoot = CreateMockWrapper("Root", "Root Service", ServiceControllerStatus.Running, new[] { "ServiceB", "ServiceC" });
                var mockB = CreateMockWrapper("ServiceB", "Service B", ServiceControllerStatus.Running, new[] { "ServiceShared" });
                var mockC = CreateMockWrapper("ServiceC", "Service C", ServiceControllerStatus.Running, new[] { "ServiceShared" });
                var mockShared = CreateMockWrapper("ServiceShared", "Shared Service", ServiceControllerStatus.Running, Array.Empty<string>());

                var mocks = new Dictionary<string, IServiceControllerWrapper>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Root", mockRoot.Object },
                    { "ServiceB", mockB.Object },
                    { "ServiceC", mockC.Object },
                    { "ServiceShared", mockShared.Object }
                };

                int sharedFactoryInvocations = 0;

                Func<string, IServiceControllerWrapper> factory = name =>
                {
                    if (string.Equals(name, "ServiceShared", StringComparison.OrdinalIgnoreCase))
                    {
                        sharedFactoryInvocations++;
                    }
                    return mocks[name];
                };

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert: ServiceShared should be expanded under both ServiceB and ServiceC, constructed only once via memoization, but cloned for independent UI state
                Assert.Equal(2, result.Dependencies.Count);
                var sharedFromB = result.Dependencies[0].Dependencies.Single();
                var sharedFromC = result.Dependencies[1].Dependencies.Single();

                Assert.Equal("ServiceShared", sharedFromB.ServiceName);
                Assert.Equal("ServiceShared", sharedFromC.ServiceName);

                // Memoization proof: SCM factory invoked only once for shared dependency
                Assert.Equal(1, sharedFactoryInvocations);

                // UI state independence: distinct node instances per tree position so IsExpanded does not mirror
                Assert.NotSame(sharedFromB, sharedFromC);

                sharedFromB.IsExpanded = true;
                Assert.False(sharedFromC.IsExpanded);
            }
        }

        [Fact]
        public void GetDependencies_TokenAlreadyCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            using (var wrapper = new ServiceControllerWrapper("Root"))
            using (var cts = new CancellationTokenSource())
            {
                var mockRoot = CreateMockWrapper("Root", "Root Service", ServiceControllerStatus.Running, new[] { "ChildA" });
                var mockChildA = CreateMockWrapper("ChildA", "Child A", ServiceControllerStatus.Running, Array.Empty<string>());

                var mocks = new Dictionary<string, IServiceControllerWrapper>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Root", mockRoot.Object },
                    { "ChildA", mockChildA.Object }
                };

                cts.Cancel();

                // Act & Assert
                Assert.Throws<OperationCanceledException>(() =>
                    wrapper.GetDependenciesInternal(name => mocks[name], cts.Token));
            }
        }

        [Fact]
        public void GetDependencies_TokenCancelledDuringChildEnumeration_StopsWalking()
        {
            // Arrange: Root has three children; the factory cancels once the first child has been
            // requested, so the walk has to abort on a per-child checkpoint rather than the entry one.
            using (var wrapper = new ServiceControllerWrapper("Root"))
            using (var cts = new CancellationTokenSource())
            {
                var mockRoot = CreateMockWrapper("Root", "Root Service", ServiceControllerStatus.Running, new[] { "ChildA", "ChildB", "ChildC" });
                var mockChildA = CreateMockWrapper("ChildA", "Child A", ServiceControllerStatus.Running, Array.Empty<string>());

                int childrenBuilt = 0;

                Func<string, IServiceControllerWrapper> factory = name =>
                {
                    if (string.Equals(name, "Root", StringComparison.OrdinalIgnoreCase)) return mockRoot.Object;

                    childrenBuilt++;
                    cts.Cancel();
                    return mockChildA.Object;
                };

                // Act & Assert
                Assert.Throws<OperationCanceledException>(() =>
                    wrapper.GetDependenciesInternal(factory, cts.Token));

                // The second and third children must never be requested.
                Assert.Equal(1, childrenBuilt);
            }
        }

        #endregion

        #region Win32Exception & Edge Case Resolution Tests

        [Fact]
        public void GetDependencies_InvalidOperationException_ReturnsUnavailableNode()
        {
            // Arrange
            using (var wrapper = new ServiceControllerWrapper("TargetService"))
            {
                Func<string, IServiceControllerWrapper> factory = name =>
                    throw new InvalidOperationException($"Service {name} was not found on computer '.'.");

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("TargetService", result.ServiceName);
                Assert.Equal(string.Format(Strings.Msg_DependencyUnavailable, "TargetService"), result.DisplayName);
                Assert.False(result.IsRunning);
                Assert.False(result.IsCyclic);
                Assert.True(result.IsUnavailable);
            }
        }

        [Fact]
        public void GetDependencies_Win32ExceptionAccessDenied_ReturnsAccessDeniedNode()
        {
            // Arrange
            using (var wrapper = new ServiceControllerWrapper("TargetService"))
            {
                Func<string, IServiceControllerWrapper> factory = name => throw new Win32Exception(5); // ERROR_ACCESS_DENIED

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("TargetService", result.ServiceName);
                Assert.Equal(string.Format(Strings.Msg_DependencyAccessDenied, "TargetService"), result.DisplayName);
                Assert.False(result.IsRunning);
                Assert.False(result.IsCyclic);
                Assert.True(result.IsUnavailable);
            }
        }

        [Fact]
        public void GetDependencies_Win32ExceptionOtherError_ReturnsUnavailableNode()
        {
            // Arrange
            using (var wrapper = new ServiceControllerWrapper("TargetService"))
            {
                Func<string, IServiceControllerWrapper> factory = name => throw new Win32Exception(1060); // ERROR_SERVICE_DOES_NOT_EXIST

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("TargetService", result.ServiceName);
                Assert.Equal(string.Format(Strings.Msg_DependencyUnavailable, "TargetService"), result.DisplayName);
                Assert.False(result.IsRunning);
                Assert.False(result.IsCyclic);
                Assert.True(result.IsUnavailable);
            }
        }

        [Fact]
        public void GetDependencies_FailingChildDependency_DoesNotAbortSiblingDependencies()
        {
            // Arrange: Root -> [ChildGoodA, ChildMissing, ChildGoodB]
            using (var wrapper = new ServiceControllerWrapper("Root"))
            {
                var mockRoot = CreateMockWrapper("Root", "Root Service", ServiceControllerStatus.Running, new[] { "ChildGoodA", "ChildMissing", "ChildGoodB" });
                var mockGoodA = CreateMockWrapper("ChildGoodA", "Alpha Service", ServiceControllerStatus.Running, Array.Empty<string>());
                var mockGoodB = CreateMockWrapper("ChildGoodB", "Zulu Service", ServiceControllerStatus.Running, Array.Empty<string>());

                Func<string, IServiceControllerWrapper> factory = name =>
                {
                    if (string.Equals(name, "Root", StringComparison.OrdinalIgnoreCase)) return mockRoot.Object;
                    if (string.Equals(name, "ChildGoodA", StringComparison.OrdinalIgnoreCase)) return mockGoodA.Object;
                    if (string.Equals(name, "ChildGoodB", StringComparison.OrdinalIgnoreCase)) return mockGoodB.Object;

                    throw new InvalidOperationException($"Service {name} was not found on computer '.'.");
                };

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert: All 3 children should be resolved, including the unavailable middle dependency
                Assert.NotNull(result);
                Assert.Equal(3, result.Dependencies.Count);

                var childMissingNode = result.Dependencies.Single(n => n.ServiceName == "ChildMissing");
                Assert.Equal(string.Format(Strings.Msg_DependencyUnavailable, "ChildMissing"), childMissingNode.DisplayName);
                Assert.False(childMissingNode.IsRunning);
                Assert.True(childMissingNode.IsUnavailable);

                Assert.Contains(result.Dependencies, n => n.ServiceName == "ChildGoodA");
                Assert.Contains(result.Dependencies, n => n.ServiceName == "ChildGoodB");

                Assert.All(result.Dependencies.Where(n => n.ServiceName != "ChildMissing"),
                    n => Assert.False(n.IsUnavailable));
            }
        }

        [Fact]
        public void GetDependencies_UnavailableChild_SortsByServiceNameNotErrorMessage()
        {
            // Arrange: "Zulu" sorts last by ServiceName, but an unavailable node's DisplayName is
            // the localized "Dependency 'Zulu' is unavailable." sentence, which sorts under 'D'.
            using (var wrapper = new ServiceControllerWrapper("Root"))
            {
                var mockRoot = CreateMockWrapper("Root", "Root Service", ServiceControllerStatus.Running, new[] { "Alpha", "Zulu", "Mike" });
                var mockAlpha = CreateMockWrapper("Alpha", "Alpha Service", ServiceControllerStatus.Running, Array.Empty<string>());
                var mockMike = CreateMockWrapper("Mike", "Mike Service", ServiceControllerStatus.Running, Array.Empty<string>());

                Func<string, IServiceControllerWrapper> factory = name =>
                {
                    if (string.Equals(name, "Root", StringComparison.OrdinalIgnoreCase)) return mockRoot.Object;
                    if (string.Equals(name, "Alpha", StringComparison.OrdinalIgnoreCase)) return mockAlpha.Object;
                    if (string.Equals(name, "Mike", StringComparison.OrdinalIgnoreCase)) return mockMike.Object;

                    throw new InvalidOperationException($"Service {name} was not found on computer '.'.");
                };

                // Act
                var result = wrapper.GetDependenciesInternal(factory, CancellationToken.None);

                // Assert: the unavailable child is ordered by its ServiceName, not by its error sentence
                Assert.Equal(new[] { "Alpha", "Mike", "Zulu" }, result.Dependencies.Select(n => n.ServiceName).ToArray());
                Assert.True(result.Dependencies.Single(n => n.ServiceName == "Zulu").IsUnavailable);
            }
        }

        [Fact]
        public void GetDependencies_CyclicDependency_MarksRevisitedNodeAsCyclic()
        {
            // Arrange: ServiceA -> ServiceB -> ServiceA (Cycle)
            using (var wrapper = new ServiceControllerWrapper("ServiceA"))
            {
                var mockA = CreateMockWrapper("ServiceA", "Service A", ServiceControllerStatus.Running, new[] { "ServiceB" });
                var mockB = CreateMockWrapper("ServiceB", "Service B", ServiceControllerStatus.Running, new[] { "ServiceA" });

                var mocks = new Dictionary<string, IServiceControllerWrapper>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ServiceA", mockA.Object },
                    { "ServiceB", mockB.Object }
                };

                // Act
                var result = wrapper.GetDependenciesInternal(name => mocks[name], CancellationToken.None);

                // Assert: Root ServiceA is not cyclic
                Assert.NotNull(result);
                Assert.Equal("ServiceA", result.ServiceName);
                Assert.False(result.IsCyclic);

                // Intermediate ServiceB is not cyclic
                var nodeB = Assert.Single(result.Dependencies);
                Assert.Equal("ServiceB", nodeB.ServiceName);
                Assert.False(nodeB.IsCyclic);

                // Revisited ServiceA is marked cyclic and recursion stops (empty dependencies)
                var cyclicNodeA = Assert.Single(nodeB.Dependencies);
                Assert.Equal("ServiceA", cyclicNodeA.ServiceName);
                Assert.True(cyclicNodeA.IsCyclic);
                Assert.Empty(cyclicNodeA.Dependencies);
            }
        }

        #endregion

        #region Test Helpers

        private static Mock<IServiceControllerWrapper> CreateMockWrapper(
            string serviceName,
            string displayName,
            ServiceControllerStatus status,
            IEnumerable<string> dependencies)
        {
            var mock = new Mock<IServiceControllerWrapper>();
            mock.Setup(m => m.ServiceName).Returns(serviceName);
            mock.Setup(m => m.DisplayName).Returns(displayName);
            mock.Setup(m => m.Status).Returns(status);
            mock.Setup(m => m.GetDependencyNames()).Returns(dependencies);
            return mock;
        }

        #endregion
    }
}
