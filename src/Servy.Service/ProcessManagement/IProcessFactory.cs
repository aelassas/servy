using System.Diagnostics;

namespace Servy.Service
{
    /// <summary>
    /// Factory interface to create instances of <see cref="IProcessWrapper"/>.
    /// </summary>
    public interface IProcessFactory
    {
        /// <summary>
        /// Creates a new <see cref="IProcessWrapper"/> instance using the specified <see cref="ProcessStartInfo"/>.
        /// </summary>
        /// <param name="startInfo">The process start information.</param>
        /// <returns>A new <see cref="IProcessWrapper"/> wrapping the created process.</returns>
        IProcessWrapper Create(ProcessStartInfo startInfo);
    }
}
