using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management;
using System.Threading;

namespace Servy.Core.Services
{
    /// <summary>
    /// Implementation of <see cref="IWmiSearcher"/> for querying WMI (Windows Management Instrumentation) objects.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class WmiSearcher : IWmiSearcher
    {
        /// <inheritdoc />
        /// <remarks>
        /// The <paramref name="cancellationToken"/> is checked on each <see cref="ManagementBaseObject"/> returned,
        /// allowing cooperative cancellation while iterating the results.
        /// </remarks>
        public IEnumerable<ManagementBaseObject> Get(string query, CancellationToken cancellationToken = default)
        {
            using (var searcher = new ManagementObjectSearcher(query))
            using (var results = searcher.Get())
            {
                foreach (ManagementObject obj in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (obj) // dispose immediately after using
                    {
                        yield return obj; // safe to access inside using
                    }
                }
            }
        }
    }
}
