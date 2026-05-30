using System;
using System.Collections.Immutable;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides a centralized repository of names that are restricted by the Windows file system.
    /// </summary>
    public static class ReservedNames
    {
        /// <summary>
        /// A complete immutable collection of legacy Windows reserved device names that cannot be used as filenames,
        /// including COM/LPT ports (1-9) and their Unicode superscript variants (¹, ², ³),
        /// plus the console handles CONIN$/CONOUT$.
        /// </summary>
        /// <remarks>
        /// Enforcing an <see cref="ImmutableHashSet{T}"/> guarantees structural integrity across the application 
        /// lifecycle, preventing external consumers from mutating, clearing, or injecting values into the definition block.
        /// </remarks>
        public static readonly ImmutableHashSet<string> ReservedDeviceNames = ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "COM¹", "COM²", "COM³",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            "LPT¹", "LPT²", "LPT³"
        );
    }
}
