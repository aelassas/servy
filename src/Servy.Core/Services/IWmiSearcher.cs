using System.Collections.Generic;
using System.Management;
using System.Threading;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides an abstraction for querying WMI (Windows Management Instrumentation) objects.
    /// </summary>
    public interface IWmiSearcher
    {
        /// <summary>
        /// Executes a WMI query and returns the matching <see cref="ManagementBaseObject"/> instances.
        /// </summary>
        /// <param name="query">The WMI query string to execute.</param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe while waiting for the query to complete.
        /// Allows the operation to be cancelled cooperatively.
        /// </param>
        /// <returns>An <see cref="IEnumerable{ManagementBaseObject}"/> containing the WMI objects that match the query.</returns>
        IEnumerable<ManagementBaseObject> Get(string query, CancellationToken cancellationToken = default);
    }
}
