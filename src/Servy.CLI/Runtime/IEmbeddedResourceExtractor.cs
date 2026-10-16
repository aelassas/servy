using Servy.Core.Helpers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// Extracts the binaries embedded in the CLI assembly to the folder the services run them from.
    /// </summary>
    /// <remarks>
    /// Production uses <see cref="EmbeddedResourceExtractor"/>, which reads the CLI assembly's resources through
    /// <see cref="Core.Helpers.ResourceHelper"/>. Tests use a fake so <see cref="ServiceBinariesBootstrapper"/>
    /// can run every branch without writing to disk.
    /// </remarks>
    internal interface IEmbeddedResourceExtractor
    {
        /// <summary>
        /// Gets the folder the resources are extracted to.
        /// </summary>
        string BaseExtractionDirectory { get; }

        /// <summary>
        /// Determines whether a resource is missing or out of date on disk and must be extracted again.
        /// </summary>
        /// <param name="fileName">The file name of the resource, without its extension.</param>
        /// <param name="extension">The file extension of the resource, without the dot.</param>
        /// <returns><see langword="true"/> when the resource must be extracted; otherwise <see langword="false"/>.</returns>
        bool IsExtractionNeeded(string fileName, string extension);

        /// <summary>
        /// Extracts a resource to <see cref="BaseExtractionDirectory"/> when it is missing or out of date.
        /// </summary>
        /// <param name="fileName">The file name of the resource, without its extension.</param>
        /// <param name="extension">The file extension of the resource, without the dot.</param>
        /// <param name="cancellationToken">A token that cancels the extraction.</param>
        /// <returns><see langword="true"/> when the resource is in place on disk; otherwise <see langword="false"/>.</returns>
        Task<bool> CopyAsync(string fileName, string extension, CancellationToken cancellationToken);

        /// <summary>
        /// Extracts a set of resources to <see cref="BaseExtractionDirectory"/>, without stopping any service first.
        /// </summary>
        /// <param name="resourceItems">The resources to extract.</param>
        /// <param name="cancellationToken">A token that cancels the extraction.</param>
        /// <returns><see langword="true"/> when every resource is in place on disk; otherwise <see langword="false"/>.</returns>
        Task<bool> CopyAllAsync(List<ResourceItem> resourceItems, CancellationToken cancellationToken);
    }
}
