using Servy.Core.Logging;
using System.Diagnostics;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Concrete factory to create <see cref="IProcessWrapper"/> instances.
    /// </summary>
    public class ProcessFactory : IProcessFactory
    {
        /// <inheritdoc/>
        public IProcessWrapper Create(ProcessStartInfo startInfo, ILogger logger)
        {
            return new ProcessWrapper(startInfo, logger);
        }
    }
}
