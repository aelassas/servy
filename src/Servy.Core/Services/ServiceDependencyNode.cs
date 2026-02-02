using System.Collections.Generic;

namespace Servy.Core.Services
{
    /// <summary>
    /// Represents a node in a Windows service dependency tree.
    /// Each node corresponds to a service and contains the services
    /// it directly depends on.
    /// </summary>
    public sealed class ServiceDependencyNode
    {
        /// <summary>
        /// Gets the internal service name as registered with the
        /// Windows Service Control Manager.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Gets the human-readable display name of the service.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the collection of services that this service
        /// directly depends on.
        /// </summary>
        public List<ServiceDependencyNode> Dependencies { get; } = new List<ServiceDependencyNode>();

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ServiceDependencyNode"/> class.
        /// </summary>
        /// <param name="serviceName">
        /// The internal service name.
        /// </param>
        /// <param name="displayName">
        /// The display name of the service.
        /// </param>
        public ServiceDependencyNode(string serviceName, string displayName)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
        }
    }
}
