using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// Extracts the binaries embedded in an assembly through <see cref="ResourceHelper"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal sealed class EmbeddedResourceExtractor : IEmbeddedResourceExtractor
    {
        /// <summary>
        /// The helper that compares and writes the resources.
        /// </summary>
        private readonly ResourceHelper _resourceHelper;

        /// <summary>
        /// The assembly the resources are embedded in.
        /// </summary>
        private readonly Assembly _assembly;

        /// <summary>
        /// The namespace the resources are embedded under.
        /// </summary>
        private readonly string _resourcesNamespace;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedResourceExtractor"/> class.
        /// </summary>
        /// <param name="resourceHelper">The helper that compares and writes the resources.</param>
        /// <param name="assembly">The assembly the resources are embedded in.</param>
        /// <param name="resourcesNamespace">The namespace the resources are embedded under.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        public EmbeddedResourceExtractor(ResourceHelper resourceHelper, Assembly assembly, string resourcesNamespace)
        {
            _resourceHelper = resourceHelper ?? throw new ArgumentNullException(nameof(resourceHelper));
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _resourcesNamespace = resourcesNamespace ?? throw new ArgumentNullException(nameof(resourcesNamespace));
        }

        /// <summary>
        /// Gets the folder the resources are extracted to.
        /// </summary>
        public string BaseExtractionDirectory => _resourceHelper.BaseExtractionDirectory;

        /// <summary>
        /// Determines whether a resource is missing or out of date on disk and must be extracted again.
        /// </summary>
        /// <param name="fileName">The file name of the resource, without its extension.</param>
        /// <param name="extension">The file extension of the resource, without the dot.</param>
        /// <returns><see langword="true"/> when the resource must be extracted; otherwise <see langword="false"/>.</returns>
        public bool IsExtractionNeeded(string fileName, string extension)
            => _resourceHelper.IsExtractionNeeded(_assembly, _resourcesNamespace, fileName, extension);

        /// <summary>
        /// Extracts a resource to <see cref="BaseExtractionDirectory"/> when it is missing or out of date.
        /// </summary>
        /// <param name="fileName">The file name of the resource, without its extension.</param>
        /// <param name="extension">The file extension of the resource, without the dot.</param>
        /// <param name="cancellationToken">A token that cancels the extraction.</param>
        /// <returns><see langword="true"/> when the resource is in place on disk; otherwise <see langword="false"/>.</returns>
        public Task<bool> CopyAsync(string fileName, string extension, CancellationToken cancellationToken)
            => _resourceHelper.CopyEmbeddedResourceAsync(_assembly, _resourcesNamespace, fileName, extension, cancellationToken: cancellationToken);

        /// <summary>
        /// Extracts a set of resources to <see cref="BaseExtractionDirectory"/>, without stopping any service first.
        /// </summary>
        /// <param name="resourceItems">The resources to extract.</param>
        /// <param name="cancellationToken">A token that cancels the extraction.</param>
        /// <returns><see langword="true"/> when every resource is in place on disk; otherwise <see langword="false"/>.</returns>
        public Task<bool> CopyAllAsync(List<ResourceItem> resourceItems, CancellationToken cancellationToken)
            => _resourceHelper.CopyResources(_assembly, _resourcesNamespace, resourceItems, stopServices: false, cancellationToken: cancellationToken);
    }
}
