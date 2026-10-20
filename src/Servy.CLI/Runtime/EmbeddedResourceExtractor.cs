using Servy.Core.Helpers;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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
        /// Deletes the Sysinternals Handle executables an earlier version extracted to the ProgramData vault.
        /// </summary>
        /// <returns><see langword="true"/> when none of them is left; a failure is logged, never thrown.</returns>
        public bool DeleteLegacyHandleExecutables() => _resourceHelper.DeleteLegacyHandleExecutables();
    }
}
