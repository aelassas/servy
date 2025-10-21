using Servy.Core.Logging;
using System.Diagnostics;

namespace Servy.Service.ProcessManagement
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
        /// <param name="logger">The logger.</param>
        /// <returns>A new <see cref="IProcessWrapper"/> wrapping the created process.</returns>
        IProcessWrapper Create(ProcessStartInfo startInfo, ILogger logger);
    }
}
