using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Represents an embedded resource and its associated metadata 
    /// used for copying from the assembly to the target directory.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ResourceItem
    {
        /// <summary>
        /// Gets or sets the file name without extension.
        /// </summary>
        public string FileNameWithoutExtension { get; set; }

        /// <summary>
        /// Gets or sets the file extension of the resource.
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// Gets or sets the optional subfolder where the file should be placed.
        /// </summary>
        public string Subfolder { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified embedded resource name inside the assembly.
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        /// Gets or sets the target file name (with extension).
        /// </summary>
        public string TagetFileName { get; set; }

        /// <summary>
        /// Gets or sets the full target path where the file should be copied.
        /// </summary>
        public string TagetPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the resource should be copied 
        /// (true if the embedded resource is newer or the file is missing).
        /// </summary>
        public bool ShouldCopy { get; set; }
    }
}
